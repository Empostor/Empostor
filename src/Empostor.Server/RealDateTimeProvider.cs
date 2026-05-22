using System;
using Empostor.Api.Utils;

namespace Empostor.Server
{
    public class RealDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
