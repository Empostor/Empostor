namespace Impostor.Server.Utils
{
    public static class DotnetUtils
    {
        private static string? _version;

        public static string Version
        {
            get
            {
                if (_version == null)
                {
                    _version = "2.0.0";
                }

                return _version;
            }
        }
    }
}
