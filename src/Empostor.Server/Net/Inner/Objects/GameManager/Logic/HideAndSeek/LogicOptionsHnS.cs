using Empostor.Api.Events.Managers;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.GameManager.Logic.HideAndSeek;

internal class LogicOptionsHnS : LogicOptions
{
    public LogicOptionsHnS(Game game, IEventManager eventManager) : base(game, eventManager)
    {
    }
}
