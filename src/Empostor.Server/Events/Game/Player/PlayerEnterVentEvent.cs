using Empostor.Api.Events.Player;
using Empostor.Api.Games;
using Empostor.Api.Innersloth.Maps;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Player
{
    public class PlayerEnterVentEvent : IPlayerEnterVentEvent
    {
        public PlayerEnterVentEvent(IGame game, IClientPlayer sender, IInnerPlayerControl innerPlayerPhysics, VentData vent)
        {
            Game = game;
            ClientPlayer = sender;
            PlayerControl = innerPlayerPhysics;
            Vent = vent;
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public VentData Vent { get; }
    }
}
