using Empostor.Server.Net.Inner.Objects;
using Empostor.Server.Net.Inner.Objects.Components;
using Empostor.Server.Net.Inner.Objects.GameManager;
using Empostor.Server.Net.Inner.Objects.ShipStatus;

namespace Empostor.Server.Net.State
{
    internal partial class GameNet
    {
        public InnerGameManager? GameManager { get; internal set; }

        public InnerLobbyBehaviour? LobbyBehaviour { get; internal set; }

        public InnerGameData GameData { get; internal set; } = new();

        public InnerVoteBanSystem? VoteBan { get; internal set; }

        public InnerShipStatus? ShipStatus { get; internal set; }
    }
}
