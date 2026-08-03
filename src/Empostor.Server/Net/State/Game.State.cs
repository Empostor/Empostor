using System.Linq;
using System.Threading.Tasks;
using Empostor.Api;
using Empostor.Api.Net;
using Next.Hazel;
using Empostor.Server.Events;
using Empostor.Server.Net.Hazel;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Net.State
{
    internal partial class Game
    {
        private async ValueTask PlayerAdd(ClientPlayer player)
        {
            if (!_players.TryAdd(player.Client.Id, player))
            {
                throw new EmpostorException("Failed to add player to game.");
            }

            if (HostId == -1)
            {
                HostId = player.Client.Id;
            }

            await _eventManager.CallAsync(new GamePlayerJoinedEvent(this, player));
        }

        private async ValueTask<bool> PlayerRemove(int playerId, string reason = "", bool isBan = false)
        {
            if (!_players.TryRemove(playerId, out var player))
            {
                return false;
            }

            _logger.LogInformation("◀ {Name} ({Id}) left │ {Reason} │ {HashPuid}",
                player.Client.Name, playerId, reason,
                player.Client.ProductUserId?.Length >= 9 ? player.Client.ProductUserId[..9] : player.Client.ProductUserId ?? "0");
            if (GameState == GameStates.Starting || GameState == GameStates.Started)
            {
                if (player.Character?.PlayerInfo != null)
                {
                    player.Character.PlayerInfo.Disconnected = true;
                    player.Character.PlayerInfo.LastDeathReason = DeathReason.Disconnect;
                    await DespawnPlayerInfoAsync(player.Character.PlayerInfo);
                }
            }
            else if (GameState == GameStates.NotStarted)
            {
                if (player.Character?.PlayerInfo != null)
                {
                    player.Character.PlayerInfo.Disconnected = true;
                    player.Character.PlayerInfo.LastDeathReason = DeathReason.Disconnect;
                }
            }

            // Always clean up from GameData to prevent duplicates on rejoin
            if (GameNet.GameData.PlayersByClientId.TryGetValue(playerId, out var gameDataInfo))
            {
                await DespawnPlayerInfoAsync(gameDataInfo);
            }

            player.Client.Player = null;
            await player.DisposeAsync();
            if (HostId == playerId)
            {
                await MigrateHost();
                await _eventManager.CallAsync(new GameHostChangedEvent(this, player, Host));
            }

            if (_players.IsEmpty || Host == null)
            {
                GameState = GameStates.Destroyed;
                await _gameManager.RemoveAsync(Code);
                return true;
            }

            if (isBan)
            {
                BanIp(player.Client.Connection.EndPoint.Address);
            }

            await _eventManager.CallAsync(new GamePlayerLeftEvent(this, player, isBan));
            _ = Task.Run(async () =>
            {
                await Task.Delay(_timeoutConfig.ConnectionTimeout);
                if (player.Client.Connection.IsConnected && player.Client.Connection is HazelConnection hazel)
                {
                    _logger.LogInformation("◀ {Name} ({Id}) left │ kept connection, disposing", player.Client.Name, playerId);
                    await player.Client.DisconnectAsync(isBan ? DisconnectReason.Banned : DisconnectReason.Kicked);
                }
            });

            return true;
        }

        private async ValueTask MigrateHost()
        {
            var host = _players.Values
                .OrderBy(p => p.Client.Id)
                .FirstOrDefault();
            if (host == null)
            {
                return;
            }

            foreach (var player in _players.Values)
            {
                player.Character?.RequestedPlayerName.Clear();
                player.Character?.RequestedColorId.Clear();
            }

            HostId = host.Client.Id;
            _logger.LogInformation("★ {Name} ({Id}) became host", host.Client.Name, host.Client.Id);
            if (GameState == GameStates.Ended && host.Limbo == LimboStates.WaitingForHost)
            {
                GameState = GameStates.NotStarted;
                await HandleJoinGameNew(host, false);
                await CheckLimboPlayers();
            }
        }

        private async ValueTask CheckLimboPlayers()
        {
            foreach (var (_, player) in _players.Where(x => x.Value.Limbo == LimboStates.WaitingForHost))
            {
                using var message = MessageWriter.Get(MessageType.Reliable);
                WriteJoinedGameMessage(message, true, player);
                WriteAlterGameMessage(message, false, IsPublic);
                player.Limbo = LimboStates.NotLimbo;
                await SendToAsync(message, player.Client.Id);
            }
        }
    }
}
