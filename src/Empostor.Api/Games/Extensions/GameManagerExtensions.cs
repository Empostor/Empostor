using System.Linq;
using Empostor.Api.Games.Managers;
using Empostor.Api.Innersloth;

namespace Empostor.Api.Games
{
    public static class GameManagerExtensions
    {
        public static int GetGameCount(this IGameManager manager, MapFlags map)
        {
            return manager.Games.Count(game => map.HasFlag((MapFlags)(1 << (byte)game.Options.Map)));
        }
    }
}
