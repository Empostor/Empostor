using System;
using System.Collections.Generic;
using System.Linq;
using Empostor.Api.Admin;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Http.Admin;

/// <summary>
///     Holds every registered <see cref="IAdminExtension"/> (contributed by plugins via DI)
///     and builds their widget panels on demand.
/// </summary>
public sealed class AdminExtensionRegistry
{
    private readonly ILogger<AdminExtensionRegistry> _logger;
    private readonly IReadOnlyList<IAdminExtension> _extensions;

    public AdminExtensionRegistry(IEnumerable<IAdminExtension> extensions, ILogger<AdminExtensionRegistry> logger)
    {
        _logger = logger;
        _extensions = extensions
            .OrderBy(e => e.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<IAdminExtension> Extensions => _extensions;

    public IAdminExtension? Find(string id)
        => _extensions.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public AdminPanelBuilder Build(IAdminExtension extension)
    {
        var builder = new AdminPanelBuilder(extension.Id, extension.Title, extension.Icon, extension.Section);
        try
        {
            extension.Build(builder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin extension '{Id}' failed to build its panel", extension.Id);
        }

        return builder;
    }

    public AdminPanelBuilder? Build(string id)
    {
        var extension = Find(id);
        return extension == null ? null : Build(extension);
    }
}
