using System;
using Empostor.Api.Games;

namespace Empostor.Api.Net.Messages.C2S
{
    public class Message22QueryPlatformIdsC2S
    {
        public static void Serialize(IMessageWriter writer)
        {
            throw new NotImplementedException();
        }

        public static void Deserialize(IMessageReader reader, out GameCode gameCode)
        {
            gameCode = reader.ReadInt32();
        }
    }
}
