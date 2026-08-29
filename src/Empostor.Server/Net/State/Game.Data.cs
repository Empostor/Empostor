using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empostor.Api;
using Empostor.Api.Net.Inner;
using Empostor.Api.Unity;
using Empostor.Server.Events.Game.Player;
using Empostor.Server.Events.Meeting;
using Empostor.Server.Events.Player;
using Empostor.Server.Net.Inner;
using Empostor.Server.Net.Inner.Objects;
using Empostor.Server.Net.Inner.Objects.Components;
using Empostor.Server.Net.Inner.Objects.GameManager;
using Empostor.Server.Net.Inner.Objects.ShipStatus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Net.State
{
    internal partial class Game
    {
        /// <summary>
        ///     Used for global object, spawned by the host.
        /// </summary>
        private const int InvalidClient = -2;

        /// <summary>
        ///     Used internally to set the OwnerId to the current ClientId.
        ///     i.e: <code>ownerId = ownerId == -3 ? this.ClientId : ownerId;</code>
        /// </summary>
        private const int CurrentClient = -3;

        /// <summary>
        ///     Used to list objects that are managed by the game server.
        /// </summary>
        private const int ServerOwned = -4;

        /// <summary>
        ///     The first NetId that is considered as a server owned Network ID that the client will not allocate by default.
        /// </summary>
        private const int MinServerNetId = 100000;

        private static readonly Dictionary<uint, Type> SpawnableObjects = new()
        {
            [0] = typeof(InnerSkeldShipStatus),
            [1] = typeof(InnerMeetingHud),
            [2] = typeof(InnerLobbyBehaviour),
            [4] = typeof(InnerPlayerControl),
            [5] = typeof(InnerMiraShipStatus),
            [6] = typeof(InnerPolusShipStatus),
            [7] = typeof(InnerDleksShipStatus),
            [8] = typeof(InnerAirshipStatus),
            [9] = typeof(InnerHideAndSeekManager),
            [10] = typeof(InnerNormalGameManager),
            [11] = typeof(InnerPlayerInfo),
            [12] = typeof(InnerVoteBanSystem),
            [13] = typeof(InnerFungleShipStatus),
        };

        private static readonly Dictionary<Type, uint> SpawnableObjectIds = SpawnableObjects.ToDictionary((i) => i.Value, (i) => i.Key);

        private readonly ConcurrentDictionary<uint, InnerNetObject> _allObjects = new ConcurrentDictionary<uint, InnerNetObject>();

        private uint _nextNetId = MinServerNetId;

        public T? FindObjectByNetId<T>(uint netId)
            where T : IInnerNetObject
        {
            if (_allObjects.TryGetValue(netId, out var obj))
            {
                return (T)(IInnerNetObject)obj;
            }

            return default;
        }

        private enum GameDataResult
        {
            /// <summary>Keep the current message in the packet and continue with the next one.</summary>
            Continue,

            /// <summary>Remove the current message from the packet and continue with the next one.</summary>
            Remove,

            /// <summary>Drop the whole packet (a cheat was detected and the connection should not relay it).</summary>
            Abort,
        }

        public async ValueTask<bool> HandleGameDataAsync(IMessageReader parent, ClientPlayer sender, bool toPlayer)
        {
            // Find target player.
            ClientPlayer? target = null;

            if (toPlayer)
            {
                var targetId = parent.ReadPackedInt32();
                if (!TryGetPlayer(targetId, out target))
                {
                    _logger.LogWarning("{Code} - Player {Id} sent GameData to unknown player {Target}", Code, sender.Client.Id, targetId);
                    return false;
                }

                _logger.LogTrace("Received GameData for target {0}.", targetId);
            }

            var startPosition = parent.Position;

            while (parent.Position < parent.Length)
            {
                using var reader = parent.ReadMessage();

                if (sender.Client.Player == null)
                {
                    return false;
                }

                if (toPlayer && (target == null || !_players.ContainsKey(target.Client.Id)))
                {
                    return false;
                }

                GameDataResult result;

                try
                {
                    result = await HandleGameDataInnerAsync(reader, sender, target);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Code} - Error while handling a GameData message, skipping it", Code);
                    parent.RemoveMessage(reader);
                    continue;
                }

                if (result == GameDataResult.Abort)
                {
                    // A cheat was detected, drop the whole packet.
                    return false;
                }

                if (result == GameDataResult.Remove)
                {
                    parent.RemoveMessage(reader);
                }
            }

            return parent.Length > startPosition;
        }

        private async ValueTask<GameDataResult> HandleGameDataInnerAsync(IMessageReader reader, ClientPlayer sender, ClientPlayer? target)
        {
            switch (reader.Tag)
            {
                case GameDataTag.DataFlag:
                {
                    var netId = reader.ReadPackedUInt32();
                    if (_allObjects.TryGetValue(netId, out var obj))
                    {
                        await obj.DeserializeAsync(sender, target, reader, false);
                    }
                    else
                    {
                        _logger.LogWarning("{Code} - Received DataFlag for unregistered NetId {NetId}", Code, netId);
                    }

                    return GameDataResult.Continue;
                }

                case GameDataTag.RpcFlag:
                {
                    var netId = reader.ReadPackedUInt32();
                    if (_allObjects.TryGetValue(netId, out var obj))
                    {
                        if (!await obj.HandleRpcAsync(sender, target, (RpcCalls)reader.ReadByte(), reader))
                        {
                            return GameDataResult.Remove;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("{Code} - Received RpcFlag for unregistered NetId {NetId}", Code, netId);
                    }

                    return GameDataResult.Continue;
                }

                case GameDataTag.SpawnFlag:
                {
                    // Only the host is allowed to spawn objects.
                    if (!sender.IsHost)
                    {
                        if (await sender.Client.ReportCheatAsync(new CheatContext(nameof(GameDataTag.SpawnFlag)), CheatCategory.MustBeHost, "Tried to send SpawnFlag as non-host."))
                        {
                            return GameDataResult.Abort;
                        }
                    }

                    var objectId = reader.ReadPackedUInt32();
                    if (SpawnableObjects.TryGetValue(objectId, out var spawnableObjectType))
                    {
                        var innerNetObject = (InnerNetObject)ActivatorUtilities.CreateInstance(_serviceProvider, spawnableObjectType, this);
                        var ownerClientId = reader.ReadPackedInt32();

                        innerNetObject.SpawnFlags = (SpawnFlags)reader.ReadByte();

                        var components = innerNetObject.GetComponentsInChildren<InnerNetObject>();
                        var componentsCount = reader.ReadPackedInt32();

                        if (componentsCount != components.Count)
                        {
                            _logger.LogError(
                                "Children didn't match for spawnable {0}, name {1} ({2} != {3})",
                                objectId,
                                innerNetObject.GetType().Name,
                                componentsCount,
                                components.Count);
                            return GameDataResult.Continue;
                        }

                        _logger.LogDebug(
                            "Spawning {0} components, SpawnFlags {1}",
                            innerNetObject.GetType().Name,
                            innerNetObject.SpawnFlags);

                        for (var i = 0; i < componentsCount; i++)
                        {
                            var obj = components[i];

                            obj.NetId = reader.ReadPackedUInt32();
                            obj.OwnerId = ownerClientId;

                            _logger.LogDebug(
                                "- {0}, NetId {1}, OwnerId {2}",
                                obj.GetType().Name,
                                obj.NetId,
                                obj.OwnerId);

                            if (!AddNetObject(obj))
                            {
                                _logger.LogTrace("Failed to AddNetObject, it already exists.");

                                obj.NetId = uint.MaxValue;
                                break;
                            }

                            using var readerSub = reader.ReadMessage();
                            if (readerSub.Length > 0)
                            {
                                await obj.DeserializeAsync(sender, target, readerSub, true);
                            }

                            await OnSpawnAsync(sender, obj);
                        }

                        return GameDataResult.Continue;
                    }

                    _logger.LogWarning("Couldn't find spawnable object {ObjectId}", objectId);
                    return GameDataResult.Continue;
                }

                // Only the host is allowed to despawn objects.
                case GameDataTag.DespawnFlag:
                {
                    var netId = reader.ReadPackedUInt32();
                    if (_allObjects.TryGetValue(netId, out var obj))
                    {
                        if (sender.Client.Id != obj.OwnerId && !sender.IsHost)
                        {
                            _logger.LogWarning(
                                "Player {0} ({1}) tried to send DespawnFlag for {2} but was denied.",
                                sender.Client.Name,
                                sender.Client.Id,
                                netId);
                            return GameDataResult.Abort;
                        }

                        RemoveNetObject(obj);
                        await OnDestroyAsync(obj);
                        _logger.LogDebug("Destroyed InnerNetObject {0} ({1}), OwnerId {2}", obj.GetType().Name, netId, obj.OwnerId);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Player {0} ({1}) sent DespawnFlag for unregistered NetId {2}.",
                            sender.Client.Name,
                            sender.Client.Id,
                            netId);
                    }

                    return GameDataResult.Continue;
                }

                case GameDataTag.SceneChangeFlag:
                {
                    // Sender is only allowed to change his own scene.
                    var clientId = reader.ReadPackedInt32();
                    var scene = reader.ReadString();

                    if (clientId != sender.Client.Id)
                    {
                        _logger.LogWarning(
                            "Player {0} ({1}) tried to send SceneChangeFlag for another player.",
                            sender.Client.Name,
                            sender.Client.Id);
                        return GameDataResult.Abort;
                    }

                    // According to game assembly, sender is only allowed to send OnlineGame.
                    if (scene != "OnlineGame")
                    {
                        _logger.LogWarning(
                            "Player {PlayerName} ({ClientId}) tried to send SceneChangeFlag with disallowed scene \"{Scene}\".",
                            sender.Client.Name,
                            sender.Client.Id,
                            scene);
                        return GameDataResult.Abort;
                    }

                    sender.Scene = scene;

                    _logger.LogTrace("> Scene {0} to {1}", clientId, sender.Scene);

                    await SyncServerObjectsAsync(sender);
                    await SpawnPlayerInfoAsync(sender);

                    return GameDataResult.Continue;
                }

                case GameDataTag.ReadyFlag:
                {
                    var clientId = reader.ReadPackedInt32();

                    if (clientId != sender.Client.Id)
                    {
                        _logger.LogWarning(
                            "Player {0} ({1}) tried to send ReadyFlag for another player.",
                            sender.Client.Name,
                            sender.Client.Id);
                        return GameDataResult.Abort;
                    }

                    _logger.LogTrace("> IsReady {0}", clientId);
                    return GameDataResult.Continue;
                }

                case GameDataTag.ConsoleDeclareClientPlatformFlag:
                {
                    var clientId = reader.ReadPackedInt32();
                    var platform = (RuntimePlatform)reader.ReadPackedInt32();

                    if (clientId != sender.Client.Id)
                    {
                        if (await sender.Client.ReportCheatAsync(new CheatContext(nameof(GameDataTag.ConsoleDeclareClientPlatformFlag)), CheatCategory.Ownership, "Client sent info with wrong client id"))
                        {
                            return GameDataResult.Abort;
                        }
                    }

                    sender.Platform = platform;

                    return GameDataResult.Continue;
                }

                default:
                {
                    _logger.LogWarning("{Code} - Bad GameData tag {Tag}", Code, reader.Tag);
                    return GameDataResult.Continue;
                }
            }
        }

        private async ValueTask OnSpawnAsync(ClientPlayer sender, InnerNetObject netObj)
        {
            switch (netObj)
            {
                case InnerGameManager innerGameManager:
                {
                    GameNet.GameManager = innerGameManager;
                    break;
                }

                case InnerLobbyBehaviour lobby:
                {
                    GameNet.LobbyBehaviour = lobby;
                    break;
                }

                case InnerPlayerInfo playerInfo:
                {
                    if (!GameNet.GameData.AddPlayer(playerInfo))
                    {
                        _logger.LogWarning(
                            "Could not add PlayerInfo for playerId {PlayerId} with NetId {newId}, already have NetId {oldNetId}",
                            playerInfo.PlayerId,
                            playerInfo.NetId,
                            GameNet.GameData.GetPlayerById(playerInfo.PlayerId)?.NetId);
                    }

                    break;
                }

                case InnerVoteBanSystem voteBan:
                {
                    GameNet.VoteBan = voteBan;
                    break;
                }

                case InnerShipStatus shipStatus:
                {
                    GameNet.ShipStatus = shipStatus;
                    break;
                }

                case InnerPlayerControl control:
                {
                    // Hook up InnerPlayerControl <-> IClientPlayer.
                    if (TryGetPlayer(control.OwnerId, out var player))
                    {
                        player.Character = control;
                        player.DisableSpawnTimeout();
                    }
                    else
                    {
                        await sender.Client.ReportCheatAsync(new CheatContext(nameof(GameDataTag.SpawnFlag)), CheatCategory.GameFlow, "Failed to find player that spawned the InnerPlayerControl");
                    }

                    // Hook up InnerPlayerControl <-> InnerPlayerControl.PlayerInfo.
                    var playerInfo = GameNet.GameData.GetPlayerById(control.PlayerId);

                    if (playerInfo != null)
                    {
                        playerInfo.Controller = control;
                        control.PlayerInfo = playerInfo;
                    }

                    if (player != null)
                    {
                        await _eventManager.CallAsync(new PlayerSpawnedEvent(this, player, control));

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(1500));

                                if (!(player.Client.Connection?.IsConnected ?? false))
                                {
                                    return;
                                }

                                // From Nmpostor
                                await _eventManager.CallAsync(new PlayerReadyEvent(this, player, control));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "PlayerReadyEvent skipped for {Name}", player.Client.Name);
                            }
                        });
                    }

                    break;
                }

                case InnerMeetingHud meetingHud:
                {
                    foreach (var player in _players.Values)
                    {
                        if (GameNet.ShipStatus != null)
                        {
                            await player.Character!.NetworkTransform.SetPositionAsync(player, GameNet.ShipStatus.GetSpawnLocation(player.Character, PlayerCount, false));
                        }
                    }

                    await _eventManager.CallAsync(new MeetingStartedEvent(this, meetingHud));
                    break;
                }
            }

            await netObj.OnSpawnAsync();
        }

        private async ValueTask OnDestroyAsync(InnerNetObject netObj)
        {
            switch (netObj)
            {
                case InnerLobbyBehaviour:
                {
                    GameNet.LobbyBehaviour = null;
                    break;
                }

                case InnerVoteBanSystem:
                {
                    GameNet.VoteBan = null;
                    break;
                }

                case InnerShipStatus:
                {
                    GameNet.ShipStatus = null;
                    break;
                }

                case InnerPlayerInfo playerInfo:
                {
                    if (GameState != GameStates.Started && GameState != GameStates.Starting)
                    {
                        GameNet.GameData.RemovePlayer(playerInfo.PlayerId);
                    }

                    break;
                }

                case InnerPlayerControl control:
                {
                    // Remove InnerPlayerControl <-> IClientPlayer.
                    if (TryGetPlayer(control.OwnerId, out var player))
                    {
                        player.Character = null;
                        await _eventManager.CallAsync(new PlayerDestroyedEvent(this, player, control));
                    }

                    break;
                }
            }
        }

        private async ValueTask SyncServerObjectsAsync(ClientPlayer sender)
        {
            foreach (var obj in _allObjects.Values)
            {
                if (obj.OwnerId == ServerOwned)
                {
                    _logger.LogTrace("Syncing {Type} {NetId}", obj.GetType(), obj.NetId);
                    await SendObjectSpawnAsync(obj, sender.Client.Id);
                }
            }
        }

        private async ValueTask SpawnPlayerInfoAsync(ClientPlayer sender)
        {
            // Hosts spawn PlayerInfo objects if they requested authority
            if (IsHostAuthoritive)
            {
                return;
            }

            // Only spawn a new PlayerInfo if one has not yet been spawned
            if (GameNet.GameData.PlayersByClientId.ContainsKey(sender.Client.Id))
            {
                return;
            }

            var playerInfo = (InnerPlayerInfo)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(InnerPlayerInfo), this);
            playerInfo.SpawnFlags = SpawnFlags.None;
            playerInfo.NetId = _nextNetId++;
            playerInfo.OwnerId = ServerOwned;
            playerInfo.ClientId = sender.Client.Id;
            playerInfo.PlayerId = GameNet.GameData.GetNextAvailablePlayerId();

            // If player played a previous game, restore their color
            var prevColor = sender.Client.PreviousColor;
            if (prevColor.HasValue)
            {
                _logger.LogTrace("Color restored to {Color}", prevColor.Value);
                playerInfo.CurrentOutfit.Color = prevColor.Value;
            }

            if (!AddNetObject(playerInfo))
            {
                _logger.LogError("{Code} - Could not spawn PlayerInfo for {Name} ({ClientId})", Code, sender.Client.Name, sender.Client.Id);
                playerInfo.NetId = uint.MaxValue;
                return;
            }

            _logger.LogTrace("Spawning PlayerInfo (netId {Netid})", playerInfo.NetId);
            await OnSpawnAsync(sender, playerInfo);
            await SendObjectSpawnAsync(playerInfo);
        }

        private async ValueTask DespawnPlayerInfoAsync(InnerPlayerInfo playerInfo)
        {
            if (playerInfo.OwnerId == ServerOwned)
            {
                _logger.LogDebug("Despawning PlayerInfo {nid}", playerInfo.NetId);
                GameNet.GameData.RemovePlayer(playerInfo.PlayerId);
                RemoveNetObject(playerInfo);

                await SendObjectDespawnAsync(playerInfo);
            }
        }

        private bool AddNetObject(InnerNetObject obj)
        {
            return _allObjects.TryAdd(obj.NetId, obj);
        }

        private void RemoveNetObject(InnerNetObject obj)
        {
            _allObjects.TryRemove(obj.NetId, out _);
        }
    }
}
