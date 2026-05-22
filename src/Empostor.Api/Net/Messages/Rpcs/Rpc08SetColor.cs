using Empostor.Api.Innersloth.Customization;

namespace Empostor.Api.Net.Messages.Rpcs
{
    public static class Rpc08SetColor
    {
        public static void Serialize(IMessageWriter writer, uint netId, ColorType color)
        {
            writer.Write(netId);
            writer.Write((byte)color);
        }

        public static void Deserialize(IMessageReader reader, out uint netId, out ColorType color)
        {
            netId = reader.ReadUInt32();
            color = (ColorType)reader.ReadByte();
        }
    }
}
