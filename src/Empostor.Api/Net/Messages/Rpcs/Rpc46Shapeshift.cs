using Empostor.Api.Games;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Net.Messages.Rpcs
{
    public static class Rpc46Shapeshift
    {
        public static void Serialize(IMessageWriter writer, IInnerPlayerControl playerControl, bool shouldAnimate)
        {
            writer.Write(playerControl);
            writer.Write(shouldAnimate);
        }

        public static void Deserialize(IMessageReader reader, IGame game, out IInnerPlayerControl? playerControl, out bool shouldAnimate)
        {
            playerControl = reader.ReadNetObject<IInnerPlayerControl>(game);
            shouldAnimate = reader.ReadBoolean();
        }
    }
}
