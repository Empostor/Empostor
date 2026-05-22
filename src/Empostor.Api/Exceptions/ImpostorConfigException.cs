using System;

namespace Empostor.Api
{
    public class EmpostorConfigException : EmpostorException
    {
        public EmpostorConfigException()
        {
        }

        public EmpostorConfigException(string? message) : base(message)
        {
        }

        public EmpostorConfigException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
