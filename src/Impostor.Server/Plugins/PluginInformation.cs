using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Impostor.Api.Plugins;

namespace Impostor.Server.Plugins
{
    public class PluginInformation
    {
        private readonly ImpostorPluginAttribute _attribute;

        public PluginInformation(IPluginStartup? startup, Type pluginType)
        {
            _attribute = pluginType.GetCustomAttribute<ImpostorPluginAttribute>()!;

            var assembly = pluginType.Assembly;
            Name = _attribute.Name ?? assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? assembly.GetName().Name!;
            Author = _attribute.Author ?? assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            Version = _attribute.Version ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version!.ToString();

            Dependencies = pluginType.GetCustomAttributes<ImpostorDependencyAttribute>().Select(t => new DependencyInformation(t)).ToList();
            Startup = startup;
            PluginType = pluginType;

            var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? Directory.GetCurrentDirectory();
            ConfigPath = Path.Combine(assemblyDir, $"[{Name}]Config.json");

            startup?.SetConfigPath(ConfigPath);
        }

        public string Id => _attribute.Id;

        public string Name { get; }

        public string Author { get; }

        public string Version { get; }

        public string ConfigPath { get; }

        public List<DependencyInformation> Dependencies { get; }

        public IPluginStartup? Startup { get; }

        public Type PluginType { get; }

        public IPlugin? Instance { get; set; }

        public override string ToString() => $"{Id} {Name} ({Version}) by {Author}";
    }
}
