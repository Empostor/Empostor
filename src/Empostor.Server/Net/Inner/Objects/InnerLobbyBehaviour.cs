using System;
using System.Threading.Tasks;
using Empostor.Api.Net;
using Empostor.Api.Net.Custom;
using Empostor.Api.Net.Inner.Objects;
using Empostor.Server.Net.State;

namespace Empostor.Server.Net.Inner.Objects
{
    internal class InnerLobbyBehaviour : InnerNetObject, IInnerLobbyBehaviour
    {
        public InnerLobbyBehaviour(ICustomMessageManager<ICustomRpc> customMessageManager, Game game) : base(customMessageManager, game)
        {
            Components.Add(this);
        }

        public override ValueTask<bool> SerializeAsync(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override ValueTask DeserializeAsync(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            throw new NotImplementedException();
        }
    }
}
