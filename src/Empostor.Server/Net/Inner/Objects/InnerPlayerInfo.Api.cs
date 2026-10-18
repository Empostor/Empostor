using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerInfo : InnerNetObject, IInnerPlayerInfo
    {
        IEnumerable<ITaskInfo> IInnerPlayerInfo.Tasks => Tasks;

        string? IInnerPlayerInfo.EosToken => Game.Players.FirstOrDefault(p => p.Client.Id == ClientId)?.Client.EosToken;
    }
}
