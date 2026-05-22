using Empostor.Api.Games;
using Empostor.Api.Innersloth.Customization;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Net.Messages.Rpcs
{
    public static class Rpc45ProtectPlayer
    {
        public static void Serialize(IMessageWriter writer, IInnerPlayerControl playerControl, ColorType color)
        {
            writer.Write(playerControl);
            writer.Write((byte)color);
        }

        public static void Deserialize(IMessageReader reader, IGame game, out IInnerPlayerControl? playerControl, out ColorType color)
        {
            playerControl = reader.ReadNetObject<IInnerPlayerControl>(game);
            color = (ColorType)reader.ReadByte();
        }
    }
}
