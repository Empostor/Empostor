using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Next.Hazel;
using Impostor.Server.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.State
{
    internal partial class Game
    {
        private readonly SemaphoreSlim _clientAddLock = new SemaphoreSlim(1, 1);

        public async ValueTask HandleStartGame(IMessageReader message)
        {
            GameState = GameStates.Starting;
            using var packet = MessageWriter.Get(MessageType.Reliable);
            message.CopyTo(packet);
            await SendToAllAsync(packet);
            await _eventManager.CallAsync(new GameStartingEvent(this));
        }

        public async ValueTask HandleEndGame(IMessageReader message, GameOverReason gameOverReason)
        {
            GameState = GameStates.Ended;
            using (var packet = MessageWriter.Get(MessageType.Reliable))
            {
                message.CopyTo(packet);
                await SendToAllAsync(packet);
            }

            foreach (var player in _players)
            {
                player.Value.Limbo = LimboStates.PreSpawn;
            }

            foreach (var playerInfo in GameNet.GameData.Players.Values.ToArray())
            {
                await DespawnPlayerInfoAsync(playerInfo);
            }

            await _eventManager.CallAsync(new GameEndedEvent(this, gameOverReason));
        }

        public async ValueTask HandleAlterGame(IMessageReader message, IClientPlayer sender, bool isPublic)
        {
            IsPublic = isPublic;
            using var packet = MessageWriter.Get(MessageType.Reliable);
            message.CopyTo(packet);
            await SendToAllExceptAsync(packet, sender.Client.Id);
            await _eventManager.CallAsync(new GameAlterEvent(this, isPublic));
        }

        public async ValueTask HandleRemovePlayer(int playerId, DisconnectReason reason)
        {
            await PlayerRemove(playerId);
            if (GameState == GameStates.Destroyed)
            {
                return;
            }

            using var packet = MessageWriter.Get(MessageType.Reliable);
            WriteRemovePlayerMessage(packet, false, playerId, reason);
            await SendToAllExceptAsync(packet, playerId);
        }

        public async ValueTask HandleKickPlayer(int playerId, bool isBan)
        {
            _logger.LogInformation("{0} - Player {1} has left.", Code, playerId);
            using (var kickMsg = MessageWriter.Get(MessageType.Reliable))
            {
                WriteKickPlayerMessage(kickMsg, false, playerId, isBan);
                await SendToAllAsync(kickMsg);
            }

            await PlayerRemove(playerId, isBan);
            if (GameState == GameStates.Destroyed)
            {
                return;
            }

            using (var removeMsg = MessageWriter.Get(MessageType.Reliable))
            {
                WriteRemovePlayerMessage(
                    removeMsg,
                    false,
                    playerId,
                    isBan ? DisconnectReason.Banned : DisconnectReason.Kicked);
                await SendToAllExceptAsync(removeMsg, playerId);
            }
        }

        public async ValueTask<GameJoinResult> AddClientAsync(ClientBase client)
        {
            var hasLock = false;
            try
            {
                hasLock = await _clientAddLock.WaitAsync(TimeSpan.FromMinutes(1));
                if (hasLock)
                {
                    return await AddClientSafeAsync(client);
                }
            }
            finally
            {
                if (hasLock)
                {
                    _clientAddLock.Release();
                }
            }

            return GameJoinResult.FromError(GameJoinError.InvalidClient);
        }

        private async ValueTask HandleJoinGameNew(ClientPlayer sender, bool isNew)
        {
            _logger.LogInformation("{0} - Player {1} ({2}) is joining.", Code, sender.Client.Name, sender.Client.Id);
            if (isNew)
            {
                await PlayerAdd(sender);
            }

            sender.InitializeSpawnTimeout();
            using (var message = MessageWriter.Get(MessageType.Reliable))
            {
                WriteJoinedGameMessage(message, false, sender);
                WriteAlterGameMessage(message, false, IsPublic);
                sender.Limbo = LimboStates.NotLimbo;
                await SendToAsync(message, sender.Client.Id);
                await BroadcastJoinMessage(message, true, sender);
            }
        }

        private async ValueTask<GameJoinResult> AddClientSafeAsync(ClientBase client)
        {
            if (_bannedIps.Contains(client.Connection.EndPoint.Address))
            {
                return GameJoinResult.FromError(GameJoinError.Banned);
            }

            var player = client.Player;
            if (_compatibilityConfig.AllowVersionMixing == false &&
                this.Host != null && client.GameVersion != this.Host.Client.GameVersion)
            {
                var versionCheckResult = _compatibilityManager.CanJoinGame(Host.Client.GameVersion, client.GameVersion);
                if (versionCheckResult != GameJoinError.None)
                {
                    return GameJoinResult.FromError(versionCheckResult);
                }
            }

            if (GameState == GameStates.Starting || GameState == GameStates.Started)
            {
                return GameJoinResult.FromError(GameJoinError.GameStarted);
            }

            if (GameState == GameStates.Destroyed)
            {
                return GameJoinResult.FromError(GameJoinError.GameDestroyed);
            }

            if (player?.Game != this && _players.Count >= Options.MaxPlayers)
            {
                return GameJoinResult.FromError(GameJoinError.GameFull);
            }

            var isNew = false;
            if (player == null || player.Game != this)
            {
                var clientPlayer = new ClientPlayer(_serviceProvider.GetRequiredService<ILogger<ClientPlayer>>(), client, this, _timeoutConfig.SpawnTimeout);
                if (!_clientManager.Validate(client))
                {
                    return GameJoinResult.FromError(GameJoinError.InvalidClient);
                }

                isNew = true;
                player = clientPlayer;
                client.Player = clientPlayer;
            }

            if (player.Limbo == LimboStates.NotLimbo)
            {
                return GameJoinResult.FromError(GameJoinError.InvalidLimbo);
            }

            if (GameState == GameStates.Ended)
            {
                await HandleJoinGameNext(player, isNew);
                return GameJoinResult.CreateSuccess(player);
            }

            var @event = new GamePlayerJoiningEvent(this, player);
            await _eventManager.CallAsync(@event);
            if (@event.JoinResult != null && !@event.JoinResult.Value.IsSuccess)
            {
                return @event.JoinResult.Value;
            }

            await HandleJoinGameNew(player, isNew);
            return GameJoinResult.CreateSuccess(player);
        }

        private async ValueTask HandleJoinGameNext(ClientPlayer sender, bool isNew)
        {
            _logger.LogInformation("{0} - Player {1} ({2}) is rejoining.", Code, sender.Client.Name, sender.Client.Id);
            if (isNew)
            {
                await PlayerAdd(sender);
            }

            if (sender.Client.Id == HostId)
            {
                GameState = GameStates.NotStarted;
                await HandleJoinGameNew(sender, false);
                await CheckLimboPlayers();
                return;
            }

            sender.Limbo = LimboStates.WaitingForHost;
            using (var packet = MessageWriter.Get(MessageType.Reliable))
            {
                WriteWaitForHostMessage(packet, false, sender);
                await SendToAsync(packet, sender.Client.Id);
                await BroadcastJoinMessage(packet, true, sender);
            }
        }
    }
}
