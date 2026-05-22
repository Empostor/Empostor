using System.Threading.Tasks;

namespace Empostor.Api.Net.Custom
{
    public interface ICustomRootMessage : ICustomMessage
    {
        ValueTask HandleMessageAsync(IClient client, IMessageReader reader, MessageType messageType);
    }
}
