using Empostor.Api.Innersloth;

namespace Empostor.Server.Utils
{
    /// <summary>
    /// Covers both the base Among Us SupportLangs (0-15) + LocalizeUs / ExtendedLangs (16-22).
    /// </summary>
    internal static class LanguageHelper
    {
        private static readonly string[] ExtendedLangs = { "Polish", "Turkish", "Swedish", "Lithuanian", "Czech", "華夏（文言文）", "Greek" };

        public static string GetDisplayName(Language lang)
        {
            var val = (int)lang;
            if (val >= 0 && val <= 15)
            {
                return $"{lang}({val})";
            }

            if (val >= 16 && val < 16 + ExtendedLangs.Length)
            {
                return $"{ExtendedLangs[val - 16]}({val})";
            }

            return $"Unknown({val})";
        }
    }
}
