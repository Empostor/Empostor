using System.Collections.Generic;
using Empostor.Api.Net;
using Empostor.Api.Net.Manager;

namespace Empostor.Server.Net.Manager
{
    internal partial class ClientManager : IClientManager
    {
        IEnumerable<IClient> IClientManager.Clients => _clients.Values;
    }
}
