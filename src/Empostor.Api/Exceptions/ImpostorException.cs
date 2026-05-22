using System;

namespace Empostor.Api
{
    public class EmpostorException : Exception
    {
        public EmpostorException()
        {
        }

        public EmpostorException(string? message) : base(message)
        {
        }

        public EmpostorException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
