using System;

namespace Empostor.Api.Net.Messages.C2S
{
    public class Message21SetActivePodType
    {
        public static void Serialize(IMessageWriter writer)
        {
            throw new NotImplementedException();
        }

        public static void Deserialize(IMessageReader reader, out string podType)
        {
            podType = reader.ReadString();
        }
    }
}
