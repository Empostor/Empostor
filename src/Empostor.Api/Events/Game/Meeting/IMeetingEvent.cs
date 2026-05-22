using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Api.Events.Meeting
{
    public interface IMeetingEvent : IGameEvent
    {
        IInnerMeetingHud MeetingHud { get; }
    }
}
