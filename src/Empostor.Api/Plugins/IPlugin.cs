using System.Threading.Tasks;
using Empostor.Api.Events;

namespace Empostor.Api.Plugins
{
    public interface IPlugin : IEventListener
    {
        ValueTask EnableAsync();

        ValueTask DisableAsync();

        ValueTask ReloadAsync();
    }
}
