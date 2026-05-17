using Next.Hazel.Abstractions;

namespace Next.Hazel;

public struct NewConnectionEventArgs
{
    /// <summary>
    ///     The data received from the client in the handshake.
    ///     This data is yours. Remember to recycle it.
    /// </summary>
    public readonly IMessageReader HandshakeData;

    /// <summary>
    ///     The <see cref="Connection" /> to the new client.
    /// </summary>
    public readonly Connection Connection;

    public NewConnectionEventArgs(IMessageReader handshakeData, Connection connection)
    {
        HandshakeData = handshakeData;
        Connection = connection;
    }
}