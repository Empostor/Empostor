using System;

namespace Empostor.Api
{
    public class EmpostorProtocolException : EmpostorException
    {
        public EmpostorProtocolException()
        {
        }

        public EmpostorProtocolException(string? message) : base(message)
        {
        }

        public EmpostorProtocolException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
