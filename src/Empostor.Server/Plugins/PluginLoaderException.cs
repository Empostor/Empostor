using System;
using Empostor.Api;

namespace Empostor.Server.Plugins
{
    public class PluginLoaderException : EmpostorException
    {
        public PluginLoaderException()
        {
        }

        public PluginLoaderException(string? message) : base(message)
        {
        }

        public PluginLoaderException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
