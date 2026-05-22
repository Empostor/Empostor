using Empostor.Api.Games;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Net.State
{
    internal partial class ClientPlayer
    {
        /// <inheritdoc />
        IClient IClientPlayer.Client => Client;

        /// <inheritdoc />
        IGame IClientPlayer.Game => Game;

        /// <inheritdoc />
        IInnerPlayerControl? IClientPlayer.Character => Character;
    }
}
