using Empostor.Api.Events.Meeting;
using Empostor.Api.Games;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Events.Meeting
{
    public class MeetingEndedEvent : IMeetingEndedEvent
    {
        public MeetingEndedEvent(IGame game, IInnerMeetingHud meetingHud, IInnerPlayerControl? exiled, bool isTie)
        {
            Game = game;
            MeetingHud = meetingHud;
            Exiled = exiled;
            IsTie = isTie;
        }

        public IGame Game { get; }

        public IInnerMeetingHud MeetingHud { get; }

        public IInnerPlayerControl? Exiled { get; }

        public bool IsTie { get; }
    }
}
