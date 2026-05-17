namespace Next.Hazel;

/// <summary>
///     Represents the IP version that a connection or listener will use.
/// </summary>
/// <remarks>
///     this sets the underlying sockets to use IPv6 but still allow IPv4 sockets to connect for backwards compatability
///     and hence it is the default IPMode in most cases.
/// </remarks>
public enum IPMode
{
    /// <summary>
    ///     Instruction to use IPv4 only, IPv6 connections will not be able to connect.
    /// </summary>
    IPv4,

    /// <summary>
    ///     Instruction to use IPv6 only, IPv4 connections will not be able to connect. IPv4 addresses can be connected
    ///     by converting to IPv6 addresses.
    /// </summary>
    IPv6
}