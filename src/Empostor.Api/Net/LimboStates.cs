using System;

namespace Empostor.Api.Net
{
    [Flags]
    public enum LimboStates
    {
        PreSpawn = 1,
        NotLimbo = 2,
        WaitingForHost = 4,
        All = PreSpawn | NotLimbo | WaitingForHost,
    }
}
