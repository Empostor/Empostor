using Empostor.Api.Games;

namespace Empostor.Server.Net
{
    public class GameCodeFactory : IGameCodeFactory
    {
        public GameCode Create()
        {
            return GameCode.Create();
        }
    }
}
