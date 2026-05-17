using System;

namespace Impostor.Api.Languages;

public sealed class LanguageString
{
    private string _value;

    internal LanguageString(string value)
    {
        _value = value;
    }

    public LanguageString Format(params object[] args)
    {
        _value = string.Format(_value, args);
        return this;
    }

    public LanguageString Replace(string oldValue, string newValue)
    {
        _value = _value.Replace(oldValue, newValue, StringComparison.Ordinal);
        return this;
    }

    public LanguageString Replace(string oldValue, object newValue)
    {
        _value = _value.Replace(oldValue, newValue?.ToString() ?? string.Empty, StringComparison.Ordinal);
        return this;
    }

    public LanguageString ReplaceAll(params (string placeholder, object value)[] replacements)
    {
        foreach (var (placeholder, value) in replacements)
            _value = _value.Replace(placeholder, value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        return this;
    }

    public string Get() => _value;

    public override string ToString() => _value;

    public static implicit operator string(LanguageString ls) => ls._value;
}
