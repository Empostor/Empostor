using System.Collections;

namespace Empostor.Plugin.Code;

public static class Extensions
{
    public static void Shuffle<T>(this T list) where T : IList
    {
        var count = list.Count;
        for (var i = 0; i < count - 2; i++)
        {
            var index = StrongRandom.Next(i, count);
            var value = list[i];
            list[i] = list[index];
            list[index] = value;
        }
    }
}
