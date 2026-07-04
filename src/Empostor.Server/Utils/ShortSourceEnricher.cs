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

                var module = GetModuleTag(fullName);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Module", module));
            }
            else
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Source", "Empostor"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Module", "Empostor"));
            }
        }

        private static string GetModuleTag(string sourceContext)
        {
            if (sourceContext.StartsWith("Empostor.Server.Net.Manager")) return "Auth";
            if (sourceContext.StartsWith("Empostor.Server.Service.Auth")) return "Auth";

            if (sourceContext.StartsWith("Empostor.Server.Net.State")) return "Game";
            if (sourceContext.StartsWith("Empostor.Server.Net.Inner")) return "Game";
            if (sourceContext.StartsWith("Empostor.Server.Net.Messages")) return "Game";
            if (sourceContext.StartsWith("Empostor.Server.Net.Factories")) return "Game";

            if (sourceContext.StartsWith("Empostor.Server.Service.Admin.Reactor")) return "Reactor";
            if (sourceContext.StartsWith("Empostor.Server.Service.Admin.Ban")) return "Ban";
            if (sourceContext.StartsWith("Empostor.Server.Service.Admin.Chat")) return "Chat";
            if (sourceContext.StartsWith("Empostor.Server.Service.Admin.Report")) return "Admin";
            if (sourceContext.StartsWith("Empostor.Server.Service.Admin")) return "Admin";

            if (sourceContext.StartsWith("Empostor.Server.Service.Api")) return "Discord";
            if (sourceContext.StartsWith("Empostor.Server.Service.Stat")) return "Stat";

            if (sourceContext.StartsWith("Empostor.Server.Http")) return "Admin";

            if (sourceContext.StartsWith("Empostor.Server.Commands")) return "Cmd";

            if (sourceContext.StartsWith("Empostor.Server.Plugins")) return "Plugin";

            if (sourceContext.StartsWith("Empostor.Plugin."))
            {
                var (tag, _) = ModuleTagRegistry.Instance.Resolve(sourceContext);
                return tag;
            }

            return "Empostor";
        }
    }
}
