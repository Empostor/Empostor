using System.Collections.Generic;
using Empostor.Api.Net.Custom;
using Empostor.Api.Net.Inner.Objects.ShipStatus;
using Empostor.Server.Net.Inner.Objects.Systems;
using Empostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.ShipStatus
{
    internal class InnerSkeldShipStatus : InnerShipStatus, IInnerSkeldShipStatus
    {
        public InnerSkeldShipStatus(ICustomMessageManager<ICustomRpc> customMessageManager, ICustomMessageManager<ICustomSystemType> customSystemManager, Game game) : base(customMessageManager, customSystemManager, game, MapTypes.Skeld)
        {
            InitializeSystems();
        }

        protected override void AddSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            base.AddSystems(systems);

            systems.Add(SystemTypes.Doors, new AutoDoorsSystemType(Doors, Data.Doors));
            systems.Add(SystemTypes.Comms, new HudOverrideSystemType());
            systems.Add(SystemTypes.Security, new SecurityCameraSystemType());
            systems.Add(SystemTypes.Reactor, new ReactorSystemType(30f, SystemTypes.Reactor));
            systems.Add(SystemTypes.LifeSupp, new LifeSuppSystemType(30f));
            systems.Add(SystemTypes.Ventilation, new VentilationSystemType());
        }
    }
}
