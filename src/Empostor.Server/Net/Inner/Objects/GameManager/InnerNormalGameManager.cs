using Empostor.Api.Events.Managers;
using Empostor.Api.Net.Custom;
using Empostor.Api.Net.Inner.Objects.GameManager;
using Empostor.Server.Net.Inner.Objects.GameManager.Logic;
using Empostor.Server.Net.Inner.Objects.GameManager.Logic.Normal;
using Empostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Net.Inner.Objects.GameManager;

internal class InnerNormalGameManager : InnerGameManager, IInnerNormalGameManager
{
    public InnerNormalGameManager(ICustomMessageManager<ICustomRpc> customMessageManager, Game game, ILogger<InnerGameManager> logger, IEventManager eventManager) : base(customMessageManager, game, logger)
    {
        LogicFlow = AddGameLogic(new LogicGameFlowNormal());
        LogicMinigame = AddGameLogic(new LogicMinigame());
        LogicRoleSelection = AddGameLogic(new LogicRoleSelectionNormal());
        LogicUsables = AddGameLogic(new LogicUsablesBasic());
        LogicOptions = AddGameLogic(new LogicOptionsNormal(game, eventManager));
    }
}
