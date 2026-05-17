using System.IO;
using System.Threading.Tasks;

namespace Impostor.Api.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        internal string? ConfigPath { get; set; }

        public virtual ValueTask EnableAsync()
        {
            return default;
        }

        public virtual ValueTask DisableAsync()
        {
            return default;
        }

        public ValueTask ReloadAsync()
        {
            return default;
        }

        protected T LoadConfig<T>(string configPath) where T : new()
            => PluginConfigLoader.Load<T>(configPath);

        protected T LoadConfig<T>() where T : new()
        {
            var path = ConfigPath ?? Path.Combine(
                System.IO.Path.GetDirectoryName(GetType().Assembly.Location) ?? ".",
                $"[{GetType().Assembly.GetName().Name}]Config.json");
            return PluginConfigLoader.Load<T>(path);
        }

        protected void SaveConfig<T>(T config)
        {
            var path = ConfigPath ?? Path.Combine(
                System.IO.Path.GetDirectoryName(GetType().Assembly.Location) ?? ".",
                $"[{GetType().Assembly.GetName().Name}]Config.json");
            PluginConfigLoader.Save(path, config);
        }
    }
}
