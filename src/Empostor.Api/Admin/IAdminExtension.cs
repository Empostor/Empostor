namespace Empostor.Api.Admin;

/// <summary>
///     Implemented by a plugin to contribute a panel to the admin interface
/// </summary>
public interface IAdminExtension
{
    /// <summary>Unique, stable id. Also the last path segment of <c>/api/admin/ext/{Id}</c>.</summary>
    string Id { get; }

    /// <summary>Display title shown in the sidebar and panel header.</summary>
    string Title { get; }

    /// <summary>Icon name (from the built-in icon set), rendered as an inline glyph.</summary>
    string Icon => "puzzle";

    /// <summary>Sidebar grouping label. Defaults to "Plugins".</summary>
    string Section => "Plugins";

    /// <summary>
    ///     Whether the panel should be re-fetched once per second while it is visible. Only panels
    ///     that mirror live server state (e.g. an in-game chat feed) need this; static panels keep
    ///     their DOM stable, which matters for widgets with in-progress typing or row editing.
    /// </summary>
    bool AutoRefresh => false;

    /// <summary>Declare the panel's widgets and callbacks.</summary>
    void Build(AdminPanelBuilder builder);
}
