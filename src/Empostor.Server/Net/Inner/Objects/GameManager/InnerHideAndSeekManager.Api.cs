using Empostor.Api.Net.Inner.Objects.GameManager;
using Empostor.Api.Net.Inner.Objects.GameManager.Logic.HideAndSeek;

namespace Empostor.Server.Net.Inner.Objects.GameManager;

internal partial class InnerHideAndSeekManager
{
    ILogicGameFlowHnS IInnerHideAndSeekManager.LogicFlowHnS => LogicFlowHnS;
}
