using System.Collections.Generic;
using Empostor.Api.Net.Custom;
using Empostor.Api.Net.Inner.Objects.ShipStatus;
using Empostor.Server.Net.Inner.Objects.Systems;
using Empostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.ShipStatus
{
    internal class InnerMiraShipStatus : InnerShipStatus, IInnerMiraShipStatus
    {
        public InnerMiraShipStatus(ICustomMessageManager<ICustomRpc> customMessageManager, Game game) : base(customMessageManager, game, MapTypes.MiraHQ)
        {
        }

        protected override void AddSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            base.AddSystems(systems);

            systems.Add(SystemTypes.Comms, new HudOverrideSystemType());
            systems.Add(SystemTypes.Reactor, new ReactorSystemType());
            systems.Add(SystemTypes.LifeSupp, new LifeSuppSystemType());
        }
    }
}
