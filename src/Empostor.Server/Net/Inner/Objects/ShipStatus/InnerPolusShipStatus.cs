using System.Collections.Generic;
using System.Numerics;
using Empostor.Api.Net.Custom;
using Empostor.Api.Net.Inner.Objects.ShipStatus;
using Empostor.Server.Net.Inner.Objects.Systems;
using Empostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.ShipStatus
{
    internal class InnerPolusShipStatus : InnerShipStatus, IInnerPolusShipStatus
    {
        public InnerPolusShipStatus(ICustomMessageManager<ICustomRpc> customMessageManager, Game game) : base(customMessageManager, game, MapTypes.Polus)
        {
        }

        public override Vector2 GetSpawnLocation(InnerPlayerControl player, int numPlayers, bool initialSpawn)
        {
            if (initialSpawn)
            {
                return base.GetSpawnLocation(player, numPlayers, initialSpawn);
            }

            var halfPlayers = numPlayers / 2; // floored intentionally
            var spawnId = player.PlayerId % 15;
            if (player.PlayerId < halfPlayers)
            {
                return Data.MeetingSpawnCenter + (new Vector2(0.6f, 0) * spawnId);
            }
            else
            {
                return Data.MeetingSpawnCenter2 + (new Vector2(0.6f, 0) * (spawnId - halfPlayers));
            }
        }

        protected override void AddSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            base.AddSystems(systems);

            systems.Add(SystemTypes.Doors, new DoorsSystemType(Doors));
            systems.Add(SystemTypes.Comms, new HudOverrideSystemType());
            systems.Add(SystemTypes.Security, new SecurityCameraSystemType());
            systems.Add(SystemTypes.Laboratory, new ReactorSystemType());
        }
    }
}
