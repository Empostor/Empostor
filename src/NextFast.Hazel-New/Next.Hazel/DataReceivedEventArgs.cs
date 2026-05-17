using Next.Hazel.Abstractions;

namespace Next.Hazel;

public struct DataReceivedEventArgs(Connection sender, IMessageReader msg, MessageType type)
{
    public readonly Connection Sender = sender;

    /// <summary>
    ///     The bytes received from the client.
    /// </summary>
    public readonly IMessageReader Message = msg;


    public readonly MessageType Type = type;
}