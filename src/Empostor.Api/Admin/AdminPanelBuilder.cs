using System;
using System.Collections.Generic;

namespace Empostor.Api.Admin;

/// <summary>
///     Fluent builder used by an <see cref="IAdminExtension"/> to declare its admin-panel content.
///     Register* methods add declarative widgets; widgets that carry a callback get a stable,
///     auto-assigned action id that the client sends back on interaction.
/// </summary>
public sealed class AdminPanelBuilder
{
    private readonly Dictionary<string, AdminAction> _actions = new(StringComparer.Ordinal);
    private int _actionCounter;

    public AdminPanelBuilder(string extensionId, string title, string icon, string? section)
    {
        ExtensionId = extensionId;
        Title = title;
        Icon = icon;
        Section = string.IsNullOrWhiteSpace(section) ? "Plugins" : section;
    }

    public string ExtensionId { get; }

    public string Title { get; }

    public string Icon { get; }

    public string Section { get; }

    public List<AdminWidget> Widgets { get; } = new();

    public IReadOnlyDictionary<string, AdminAction> Actions => _actions;

    public AdminPanelBuilder RegisterButton(Action<AdminButton> configure) => Register(configure);

    public AdminPanelBuilder RegisterText(string content, string tone = "default", bool monospace = false)
    {
        Widgets.Add(new AdminText { Content = content, Tone = tone, Monospace = monospace });
        return this;
    }

    public AdminPanelBuilder RegisterText(Action<AdminText> configure) => Register(configure);

    public AdminPanelBuilder RegisterBlock(Action<AdminBlock> configure) => Register(configure);

    public AdminPanelBuilder RegisterTextbox(Action<AdminTextbox> configure) => Register(configure);

    public AdminPanelBuilder RegisterToggle(Action<AdminToggle> configure) => Register(configure);

    public AdminPanelBuilder RegisterSelect(Action<AdminSelect> configure) => Register(configure);

    public AdminPanelBuilder RegisterNumber(Action<AdminNumber> configure) => Register(configure);

    public AdminPanelBuilder RegisterTable(Action<AdminTable> configure) => Register(configure);

    public AdminPanelBuilder RegisterChips(Action<AdminChips> configure) => Register(configure);

    public AdminPanelBuilder RegisterDivider()
    {
        Widgets.Add(new AdminDivider());
        return this;
    }

    private AdminPanelBuilder Register<T>(Action<T> configure)
        where T : AdminWidget, new()
    {
        var widget = new T();
        configure(widget);
        Bind(widget);
        Widgets.Add(widget);
        return this;
    }

    private void Bind(AdminWidget widget)
    {
        if (widget.Handler != null)
        {
            widget.Action = NextAction();
            _actions[widget.Action] = widget.Handler;
        }

        if (widget is AdminChips chips)
        {
            if (chips.AddHandler != null)
            {
                chips.AddAction = NextAction();
                _actions[chips.AddAction] = chips.AddHandler;
            }

            if (chips.RemoveHandler != null)
            {
                chips.RemoveAction = NextAction();
                _actions[chips.RemoveAction] = chips.RemoveHandler;
            }
        }

        if (widget is AdminBlock block)
        {
            foreach (var child in block.Children)
            {
                Bind(child);
            }
        }
    }

    private string NextAction() => $"{ExtensionId}.a{_actionCounter++}";
}
