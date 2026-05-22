using System.Threading.Tasks;

namespace Empostor.Api.Events
{
    public interface IManualEventListener : IEventListener
    {
        EventPriority Priority { get; set; }

        public bool CanExecute<T>();

        public ValueTask Execute(IEvent @event);
    }
}
