using Empostor.Api.Events.Managers;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects.GameManager.Logic.Normal;

internal class LogicOptionsNormal : LogicOptions
{
    public LogicOptionsNormal(Game game, IEventManager eventManager) : base(game, eventManager)
    {
    }
}
