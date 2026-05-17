using System.IO;
using System.Text.Json;

namespace Impostor.Api.Plugins
{
    public static class PluginConfigLoader
    {
        private static readonly JsonSerializerOptions Opts =
            new() { WriteIndented = true };

        public static T Load<T>(string configPath) where T : new()
        {
            if (!File.Exists(configPath))
            {
                var def = new T();
                Save(configPath, def);
                return def;
            }

            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<T>(json, Opts) ?? new T();
        }

        public static void Save<T>(string configPath, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? ".");
            File.WriteAllText(configPath, JsonSerializer.Serialize(value, Opts));
        }
    }
}
