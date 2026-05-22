namespace Empostor.Api.Events
{
    public interface IGameAlterEvent : IGameEvent
    {
        bool IsPublic { get; }
    }
}
