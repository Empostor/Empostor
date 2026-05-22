using Empostor.Api.Games;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Net.Messages.Rpcs
{
    public static class Rpc47CheckMurder
    {
        public static void Serialize(IMessageWriter writer, IInnerPlayerControl playerControl)
        {
            writer.Write(playerControl);
        }

        public static void Deserialize(IMessageReader reader, IGame game, out IInnerPlayerControl? playerControl)
        {
            playerControl = reader.ReadNetObject<IInnerPlayerControl>(game);
        }
    }
}
