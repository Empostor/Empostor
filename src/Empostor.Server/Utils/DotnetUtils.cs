namespace Empostor.Server.Utils
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
                    _version = "2.1.0";
                }

                return _version;
            }
        }
    }
}
