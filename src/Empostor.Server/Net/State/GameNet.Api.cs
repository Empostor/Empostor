using Empostor.Api.Net.Inner;
using Empostor.Api.Net.Inner.Objects;
using Empostor.Api.Net.Inner.Objects.GameManager;
using Empostor.Api.Net.Inner.Objects.ShipStatus;

namespace Empostor.Server.Net.State
{
    /// <inheritdoc />
    internal partial class GameNet : IGameNet
    {
        IInnerGameManager? IGameNet.GameManager => GameManager;

        IInnerLobbyBehaviour? IGameNet.LobbyBehaviour => LobbyBehaviour;

        IInnerGameData IGameNet.GameData => GameData;

        IInnerVoteBanSystem? IGameNet.VoteBan => VoteBan;

        IInnerShipStatus? IGameNet.ShipStatus => ShipStatus;
    }
}
