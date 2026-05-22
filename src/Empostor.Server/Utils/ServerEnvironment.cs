using Empostor.Api.Utils;

namespace Empostor.Server.Utils
{
    public class ServerEnvironment : IServerEnvironment
    {
        public string Version { get; } = DotnetUtils.Version;

        public bool IsReplay { get; init; }
    }
}
