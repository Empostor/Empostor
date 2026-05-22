using System.Collections.Generic;

namespace Empostor.Api.Net.Manager
{
    public interface IClientManager
    {
        IEnumerable<IClient> Clients { get; }
    }
}
