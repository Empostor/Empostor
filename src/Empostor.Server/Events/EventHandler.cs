using Empostor.Api.Events;
using Empostor.Server.Events.Register;

namespace Empostor.Server.Events
{
    internal readonly struct EventHandler
    {
        public EventHandler(IEventListener? o, IRegisteredEventListener listener)
        {
            Object = o;
            Listener = listener;
        }

        public IEventListener? Object { get; }

        public IRegisteredEventListener Listener { get; }

        public void Deconstruct(out IEventListener? o, out IRegisteredEventListener listener)
        {
            o = Object;
            listener = Listener;
        }
    }
}
