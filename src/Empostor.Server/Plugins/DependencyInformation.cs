using Empostor.Api.Plugins;

namespace Empostor.Server.Plugins
{
    public class DependencyInformation
    {
        private readonly EmpostorDependencyAttribute _attribute;

        public DependencyInformation(EmpostorDependencyAttribute attribute)
        {
            _attribute = attribute;
        }

        public string Id => _attribute.Id;

        public DependencyType DependencyType => _attribute.DependencyType;
    }
}
