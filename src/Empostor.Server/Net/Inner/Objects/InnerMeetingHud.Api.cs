using System;
using System.Collections.Generic;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Net.Inner.Objects
{
    internal partial class InnerMeetingHud : IInnerMeetingHud
    {
        IReadOnlyCollection<IInnerMeetingHud.IPlayerVoteArea> IInnerMeetingHud.PlayerStates => Array.AsReadOnly(_playerStates);

        IInnerPlayerInfo? IInnerMeetingHud.Reporter => Reporter;
    }
}
