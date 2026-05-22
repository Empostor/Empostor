using System.Threading.Tasks;
using Empostor.Api.Net.Inner;

namespace Empostor.Api.Net.Custom
{
    public interface ICustomRpc : ICustomMessage
    {
        ValueTask<bool> HandleRpcAsync(IInnerNetObject innerNetObject, IClientPlayer sender, IClientPlayer? target, IMessageReader reader);
    }
}
