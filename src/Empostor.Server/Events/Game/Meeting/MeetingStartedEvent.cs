using Empostor.Api.Events.Meeting;
using Empostor.Api.Games;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Meeting
{
    public class MeetingStartedEvent : IMeetingStartedEvent
    {
        public MeetingStartedEvent(IGame game, IInnerMeetingHud meetingHud)
        {
            Game = game;
            MeetingHud = meetingHud;
        }

        public IGame Game { get; }

        public IInnerMeetingHud MeetingHud { get; }
    }
}
