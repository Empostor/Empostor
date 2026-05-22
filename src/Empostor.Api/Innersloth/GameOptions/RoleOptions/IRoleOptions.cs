namespace Empostor.Api.Innersloth.GameOptions.RoleOptions;

public interface IRoleOptions
{
    RoleTypes Type { get; }

    void Serialize(IMessageWriter writer);
}
