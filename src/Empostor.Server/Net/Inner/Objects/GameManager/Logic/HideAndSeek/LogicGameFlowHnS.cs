using System.Threading.Tasks;
using Empostor.Api.Net.Inner.Objects.GameManager.Logic.HideAndSeek;

namespace Empostor.Server.Net.Inner.Objects.GameManager.Logic.HideAndSeek;

internal class LogicGameFlowHnS : LogicGameFlow, ILogicGameFlowHnS
{
    public float CurrentFinalHideTime { get; private set; }

    public float CurrentHideTime { get; private set; }

    public override ValueTask DeserializeAsync(IMessageReader reader, bool initialState)
    {
        var num = reader.ReadSingle();

        CurrentFinalHideTime = reader.ReadSingle();
        CurrentHideTime = num;
        return default;
    }
}
