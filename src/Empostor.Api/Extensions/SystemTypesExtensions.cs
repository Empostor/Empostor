using Empostor.Api.Innersloth;

namespace Empostor.Api
{
    public static class SystemTypesExtensions
    {
        public static string GetFriendlyName(this SystemTypes type)
        {
            return SystemTypeHelpers.Names[(int)type];
        }
    }
}
