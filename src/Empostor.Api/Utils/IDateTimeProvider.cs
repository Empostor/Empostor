using System;

namespace Empostor.Api.Utils
{
    public interface IDateTimeProvider
    {
        DateTimeOffset UtcNow { get; }
    }
}
