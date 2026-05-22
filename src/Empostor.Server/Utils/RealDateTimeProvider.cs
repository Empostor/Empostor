using System;
using Empostor.Api.Utils;

namespace Empostor.Server.Utils
{
    public class RealDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
