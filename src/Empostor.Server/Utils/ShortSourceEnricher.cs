using Serilog.Core;
using Serilog.Events;

namespace Empostor.Server.Utils
{
    internal class ShortSourceEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue) &&
                sourceContextValue is ScalarValue scalar &&
                scalar.Value is string fullName)
            {
                var shortName = fullName[(fullName.LastIndexOf('.') + 1)..];
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Source", shortName));
            }
            else
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Source", "Empostor"));
            }
        }
    }
}
