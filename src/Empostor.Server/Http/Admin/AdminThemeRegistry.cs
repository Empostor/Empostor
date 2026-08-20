using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Empostor.Api.Admin;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Http.Admin;

public sealed record AdminThemeInfo(string Id, string Name);

/// <summary>
///     Resolves admin-panel themes. Themes come from compiled <see cref="IAdminTheme"/> plugins
///     and from folders under <c>Pages/themes/{Id}/theme.json</c>. A theme may extend another via
///     <see cref="IAdminTheme.Extends"/>; tokens/css are inherited and overridden layer by layer.
/// </summary>
public sealed class AdminThemeRegistry
{
    private readonly ILogger<AdminThemeRegistry> _logger;
    private readonly Dictionary<string, ThemeEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AdminThemeDefinition> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public AdminThemeRegistry(IEnumerable<IAdminTheme> compiled, ILogger<AdminThemeRegistry> logger)
    {
        _logger = logger;

        Add("default", "Default", null, new AdminThemeDefinition());

        foreach (var theme in compiled)
        {
            try
            {
                Add(theme.Id, theme.Name, theme.Extends, theme.Define());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin theme '{Id}' failed to define", theme.Id);
            }
        }

        LoadDiskThemes();
        Resolve();
    }

    public IReadOnlyList<AdminThemeInfo> Themes
        => _entries.Values
            .OrderBy(e => e.Id == "default" ? 0 : 1)
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Select(e => new AdminThemeInfo(e.Id, e.Name))
            .ToList();

    public bool Exists(string id) => _entries.ContainsKey(id);

    public AdminThemeDefinition? Resolve(string id)
        => _resolved.TryGetValue(id, out var definition) ? definition : null;

    public string BuildCss(string id)
    {
        var definition = Resolve(id) ?? new AdminThemeDefinition();
        var sb = new StringBuilder();

        if (definition.Tokens.Count > 0)
        {
            sb.Append(":root{");
            foreach (var (key, value) in definition.Tokens)
            {
                sb.Append(key).Append(':').Append(value).Append(';');
            }

            sb.Append('}');
        }

        if (definition.DarkTokens is { Count: > 0 })
        {
            sb.Append("[data-theme=\"dark\"]{");
            foreach (var (key, value) in definition.DarkTokens)
            {
                sb.Append(key).Append(':').Append(value).Append(';');
            }

            sb.Append('}');
        }

        if (!string.IsNullOrEmpty(definition.CustomCss))
        {
            sb.Append(definition.CustomCss);
        }

        return sb.ToString();
    }

    private void Add(string id, string name, string? extends, AdminThemeDefinition definition)
    {
        _entries[id] = new ThemeEntry { Id = id, Name = name, Extends = extends, Definition = definition };
    }

    private void LoadDiskThemes()
    {
        var themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "themes");
        if (!Directory.Exists(themesDir))
        {
            return;
        }

        foreach (var dir in Directory.EnumerateDirectories(themesDir))
        {
            var path = Path.Combine(dir, "theme.json");
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var disk = JsonSerializer.Deserialize<DiskTheme>(json);
                if (disk == null || string.IsNullOrWhiteSpace(disk.Id))
                {
                    continue;
                }

                var definition = new AdminThemeDefinition
                {
                    Tokens = disk.Tokens ?? new Dictionary<string, string>(),
                    DarkTokens = disk.DarkTokens,
                    CustomCss = disk.CustomCss,
                    Layout = disk.Layout,
                    Components = disk.Components ?? new Dictionary<string, string>(),
                };

                Add(disk.Id, disk.Name ?? disk.Id, disk.Extends, definition);
                _logger.LogInformation("Loaded admin theme '{Id}' from {Path}", disk.Id, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load admin theme from {Path}", path);
            }
        }
    }

    private void Resolve()
    {
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AdminThemeDefinition ResolveOne(string id)
        {
            if (_resolved.TryGetValue(id, out var cached))
            {
                return cached;
            }

            if (!_entries.TryGetValue(id, out var entry))
            {
                return new AdminThemeDefinition();
            }

            if (!resolving.Add(id))
            {
                _logger.LogWarning("Admin theme inheritance cycle detected at '{Id}'", id);
                return entry.Definition;
            }

            var merged = entry.Definition;
            if (!string.IsNullOrWhiteSpace(entry.Extends))
            {
                var parent = ResolveOne(entry.Extends);
                merged = Merge(parent, entry.Definition);
            }

            resolving.Remove(id);
            _resolved[id] = merged;
            return merged;
        }

        foreach (var id in _entries.Keys.ToList())
        {
            ResolveOne(id);
        }
    }

    private static AdminThemeDefinition Merge(AdminThemeDefinition parent, AdminThemeDefinition child)
    {
        var tokens = new Dictionary<string, string>(parent.Tokens);
        foreach (var (key, value) in child.Tokens)
        {
            tokens[key] = value;
        }

        var darkTokens = new Dictionary<string, string>(parent.DarkTokens ?? new Dictionary<string, string>());
        if (child.DarkTokens != null)
        {
            foreach (var (key, value) in child.DarkTokens)
            {
                darkTokens[key] = value;
            }
        }

        var components = new Dictionary<string, string>(parent.Components);
        foreach (var (key, value) in child.Components)
        {
            components[key] = value;
        }

        var customCss = string.IsNullOrEmpty(parent.CustomCss)
            ? child.CustomCss
            : parent.CustomCss + "\n" + child.CustomCss;

        return new AdminThemeDefinition
        {
            Tokens = tokens,
            DarkTokens = darkTokens,
            CustomCss = customCss,
            Layout = child.Layout ?? parent.Layout,
            Components = components,
        };
    }

    private sealed class ThemeEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Extends { get; set; }

        public AdminThemeDefinition Definition { get; set; } = new();
    }

    private sealed class DiskTheme
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Extends { get; set; }

        public Dictionary<string, string>? Tokens { get; set; }

        public Dictionary<string, string>? DarkTokens { get; set; }

        public string? CustomCss { get; set; }

        public string? Layout { get; set; }

        public Dictionary<string, string>? Components { get; set; }
    }
}
