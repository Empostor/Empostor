using System;

namespace Empostor.Api.Net.Messages.S2C
{
    public static class Message07JoinedGameS2C
    {
        public static void Serialize(IMessageWriter writer, bool clear, int gameCode, int playerId, int hostId, IClientPlayer[] otherPlayers)
        {
            if (clear)
            {
                writer.Clear(MessageType.Reliable);
            }

            writer.StartMessage(MessageFlags.JoinedGame);
            writer.Write(gameCode);
            writer.Write(playerId);
            writer.Write(hostId);
            writer.WritePacked(otherPlayers.Length);

            foreach (var ply in otherPlayers)
            {
                writer.WritePacked(ply.Client.Id);
                writer.Write(ply.Client.Name);
                ply.Client.PlatformSpecificData.Serialize(writer);
                writer.WritePacked(ply.Character?.PlayerInfo?.PlayerLevel ?? 1);

                // The client reads these two strings as ProductUserId and FriendCode.
                // Empostor knows them from the auth cache, so send the real values so
                // the client can resolve friend codes / PUIDs of players in the lobby.
                writer.Write(ply.Client.ProductUserId ?? string.Empty);
                writer.Write(ply.Client.FriendCode ?? string.Empty);
            }

            writer.EndMessage();
        }

        public static void Deserialize(IMessageReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
