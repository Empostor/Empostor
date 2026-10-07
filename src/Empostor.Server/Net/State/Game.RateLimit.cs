using System.Diagnostics;

namespace Empostor.Server.Net.State
{
    internal partial class Game
    {
        private const int RateLimitSlots = 256;
        private const int UnknownPlayerSlot = RateLimitSlots - 1;

        private readonly RateLimitWindow[] _rpcWindows = CreateWindows();
        private readonly RateLimitWindow[] _taskWindows = CreateWindows();

        /// <summary>Monotonic clock in seconds, shared by every rate-limit window.</summary>
        internal static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        /// <summary>
        ///     Counts one RPC against the per-second allowance of the player.
        ///     Returns true when this RPC is the one that goes over the limit.
        /// </summary>
        internal bool CountRpc(byte playerId)
            => _rpcWindows[Slot(playerId)].Hit(Now, AntiCheatConfig.RpcRateLimitPerSecond, 1);

        /// <summary>
        ///     Counts one completed task against the allowance of the player.
        ///     Returns true when this task is the one that goes over the limit.
        /// </summary>
        internal bool CountTask(byte playerId)
            => _taskWindows[Slot(playerId)].Hit(
                Now,
                AntiCheatConfig.TaskRateLimitCount,
                AntiCheatConfig.TaskRateLimitWindowSeconds);

        private static int Slot(byte playerId)
            => playerId < RateLimitSlots ? playerId : UnknownPlayerSlot;

        private static RateLimitWindow[] CreateWindows()
        {
            var windows = new RateLimitWindow[RateLimitSlots];
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i] = new RateLimitWindow();
            }

            return windows;
        }

        /// <summary>
        ///     Ring buffer of the most recent hit timestamps, used to spot a burst of
        ///     messages inside a window without ever growing.
        /// </summary>
        private struct RateLimitWindow
        {
            private const int WindowSize = 64;

            private readonly double[] _stamps;

            private int _next;

            public RateLimitWindow()
            {
                _stamps = new double[WindowSize];
            }

            public bool Hit(double now, int maxCount, double windowSeconds)
            {
                if (maxCount <= 0 || windowSeconds <= 0)
                {
                    return false;
                }

                var used = 0;
                for (var i = 0; i < WindowSize; i++)
                {
                    if (_stamps[i] > 0 && now - _stamps[i] < windowSeconds && ++used >= maxCount)
                    {
                        return true;
                    }
                }

                _stamps[_next] = now;
                _next = (_next + 1) % WindowSize;
                return false;
            }
        }
    }
}
