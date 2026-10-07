using Empostor.Api.Net.Inner.Objects;

namespace Empostor.Server.Net.Inner.Objects.Systems
{
    public interface ISystemType
    {
        void Serialize(IMessageWriter writer, bool initialState);

        void Deserialize(IMessageReader reader, bool initialState);

        void UpdateSystem(IInnerPlayerControl? playerControl, IMessageReader reader);
    }
}
