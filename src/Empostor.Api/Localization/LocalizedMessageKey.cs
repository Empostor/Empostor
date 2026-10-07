namespace Empostor.Api.Localization;

public readonly record struct LocalizedMessageKey(string Owner, string Key)
{
    public override string ToString() => $"{Owner}:{Key}";
}
