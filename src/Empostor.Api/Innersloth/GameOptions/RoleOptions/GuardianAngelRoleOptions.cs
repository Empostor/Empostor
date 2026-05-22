namespace Empostor.Api.Innersloth.GameOptions.RoleOptions;

public class GuardianAngelRoleOptions : IRoleOptions
{
    public GuardianAngelRoleOptions(byte version)
    {
        Version = version;
    }

    public byte Version { get; }

    public RoleTypes Type => RoleTypes.GuardianAngel;

    public byte Cooldown { get; set; } = 60;

    public byte ProtectionDurationSeconds { get; set; } = 10;

    public bool EmpostorsCanSeeProtect { get; set; }

    public static GuardianAngelRoleOptions Deserialize(IMessageReader reader, byte version)
    {
        var options = new GuardianAngelRoleOptions(version);

        options.Cooldown = reader.ReadByte();
        options.ProtectionDurationSeconds = reader.ReadByte();
        options.EmpostorsCanSeeProtect = reader.ReadBoolean();

        return options;
    }

    public void Serialize(IMessageWriter writer)
    {
        writer.Write(Cooldown);
        writer.Write(ProtectionDurationSeconds);
        writer.Write(EmpostorsCanSeeProtect);
    }
}
