using Next.Hazel.Abstractions;

namespace Next.Hazel.Extensions;

public class DefaultMessageWriterProvider : IMessageWriterProvider
{
    public IMessageWriter Get(MessageType sendOption = MessageType.Unreliable)
    {
        return MessageWriter.Get(sendOption);
    }
}