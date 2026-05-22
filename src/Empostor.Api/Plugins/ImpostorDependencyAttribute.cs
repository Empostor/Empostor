using System;

namespace Empostor.Api.Plugins
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EmpostorDependencyAttribute : Attribute
    {
        public EmpostorDependencyAttribute(string id, DependencyType type)
        {
            Id = id;
            DependencyType = type;
        }

        public string Id { get; }

        public DependencyType DependencyType { get; }
    }
}
