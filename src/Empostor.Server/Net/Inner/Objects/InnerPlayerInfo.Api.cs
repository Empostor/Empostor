using System.Collections.Generic;
using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerInfo : InnerNetObject, IInnerPlayerInfo
    {
        IEnumerable<ITaskInfo> IInnerPlayerInfo.Tasks => Tasks;
    }
}
