using System.Collections.Generic;
using Empostor.Api.Net.Custom;
using Empostor.Server.Net.Inner.Objects.Systems;
using Empostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.ShipStatus
{
    internal class InnerFungleShipStatus : InnerShipStatus
    {
        public InnerFungleShipStatus(ICustomMessageManager<ICustomRpc> customMessageManager, ICustomMessageManager<ICustomSystemType> customSystemManager, Game game) : base(customMessageManager, customSystemManager, game, MapTypes.Fungle)
        {
            InitializeSystems();
        }

        protected override void AddSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            systems.Add(SystemTypes.Ventilation, new VentilationSystemType());
            systems.Add(SystemTypes.Comms, new HqHudSystemType());
            systems.Add(SystemTypes.Reactor, new ReactorSystemType(60f, SystemTypes.Reactor));
            systems.Add(SystemTypes.Doors, new DoorsSystemType(Doors, Data.Doors));
            systems.Add(SystemTypes.MushroomMixupSabotage, new MushroomMixupSabotageSystemType());
        }
    }
}
