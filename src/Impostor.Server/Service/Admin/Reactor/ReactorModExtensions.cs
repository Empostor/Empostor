using System.Runtime.CompilerServices;
using Impostor.Api.Net;

namespace Impostor.Server.Service.Admin.Reactor
{

    public static class ReactorModExtensions
    {
        private static readonly ConditionalWeakTable<IHazelConnection, ReactorModInfo> Table = new();

        public static ReactorModInfo? GetReactorMods(this IClient client)
            => client.Connection?.GetReactorMods();

        public static ReactorModInfo? GetReactorMods(this IHazelConnection connection)
            => Table.TryGetValue(connection, out var info) ? info : null;

        internal static void SetReactorMods(this IHazelConnection connection, ReactorModInfo info)
            => Table.AddOrUpdate(connection, info);
    }
}
