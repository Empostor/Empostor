namespace Empostor.Server.Net.Inner.Objects.Systems.ShipStatus
{
    public interface IDoorSystem : IActivatable
    {
        void CloseDoorsOfType(SystemTypes room);
    }
}
