using Empostor.Api.Net.Inner.Objects.GameManager.Logic.HideAndSeek;

namespace Empostor.Api.Net.Inner.Objects.GameManager;

public interface IInnerHideAndSeekManager : IInnerGameManager
{
    ILogicGameFlowHnS LogicFlowHnS { get; }
}
