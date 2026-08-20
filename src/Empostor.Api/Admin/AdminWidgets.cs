using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Empostor.Api.Admin;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AdminButton), "button")]
[JsonDerivedType(typeof(AdminText), "text")]
[JsonDerivedType(typeof(AdminBlock), "block")]
[JsonDerivedType(typeof(AdminTextbox), "textbox")]
[JsonDerivedType(typeof(AdminToggle), "toggle")]
[JsonDerivedType(typeof(AdminSelect), "select")]
[JsonDerivedType(typeof(AdminNumber), "number")]
[JsonDerivedType(typeof(AdminTable), "table")]
[JsonDerivedType(typeof(AdminChips), "chips")]
[JsonDerivedType(typeof(AdminDivider), "divider")]
public abstract class AdminWidget
{
    public string? Action { get; set; }

    [JsonIgnore]
    public AdminAction? Handler { get; set; }
}

public sealed class AdminButton : AdminWidget
{
    public string Label { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string Style { get; set; } = "primary";

    public AdminButton OnClick(AdminAction handler)
    {
        Handler = handler;
        return this;
    }
}

public sealed class AdminText : AdminWidget
{
    public string Content { get; set; } = string.Empty;

    public string Tone { get; set; } = "default";

    public bool Monospace { get; set; }
}

public sealed class AdminBlock : AdminWidget
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public List<AdminWidget> Children { get; set; } = new();
}

public sealed class AdminTextbox : AdminWidget
{
    public string Label { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Placeholder { get; set; }

    public bool Secret { get; set; }

    public bool Multiline { get; set; }

    public bool SubmitOnChange { get; set; }

    public AdminTextbox OnSubmit(AdminAction handler)
    {
        Handler = handler;
        return this;
    }
}

public sealed class AdminToggle : AdminWidget
{
    public string Label { get; set; } = string.Empty;

    public bool Value { get; set; }

    public AdminToggle OnChange(AdminAction handler)
    {
        Handler = handler;
        return this;
    }
}

public sealed class AdminSelect : AdminWidget
{
    public string Label { get; set; } = string.Empty;

    public List<AdminOption> Options { get; set; } = new();

    public string? Value { get; set; }

    public AdminSelect OnChange(AdminAction handler)
    {
        Handler = handler;
        return this;
    }
}

public sealed class AdminNumber : AdminWidget
{
    public string Label { get; set; } = string.Empty;

    public int Value { get; set; }

    public int? Min { get; set; }

    public int? Max { get; set; }

    public AdminNumber OnChange(AdminAction handler)
    {
        Handler = handler;
        return this;
    }
}

public sealed class AdminTable : AdminWidget
{
    public List<string> Columns { get; set; } = new();

    public List<List<AdminTableCell>> Rows { get; set; } = new();
}

public sealed class AdminChips : AdminWidget
{
    public List<AdminChip> Items { get; set; } = new();

    public string? Placeholder { get; set; }

    public string? AddLabel { get; set; }

    public string? AddAction { get; set; }

    public string? RemoveAction { get; set; }

    [JsonIgnore]
    public AdminAction? AddHandler { get; set; }

    [JsonIgnore]
    public AdminAction? RemoveHandler { get; set; }

    public AdminChips OnAdd(AdminAction handler)
    {
        AddHandler = handler;
        return this;
    }

    public AdminChips OnRemove(AdminAction handler)
    {
        RemoveHandler = handler;
        return this;
    }
}

public sealed class AdminDivider : AdminWidget
{
}

public sealed record AdminOption(string Label, string Value);

public sealed class AdminTableCell
{
    public string Text { get; set; } = string.Empty;

    public string? Tone { get; set; }

    public bool Monospace { get; set; }
}

public sealed record AdminChip(string Value, string Label);
