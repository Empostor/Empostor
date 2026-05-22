using System.Threading.Tasks;
using Empostor.Api;
using Empostor.Api.Events.Managers;
using Empostor.Api.Innersloth.Maps;
using Empostor.Api.Net.Inner;
using Empostor.Api.Net.Inner.Objects;
using Empostor.Api.Net.Messages.Rpcs;
using Empostor.Server.Events.Player;

namespace Empostor.Server.Net.Inner.Objects
{
    internal class TaskInfo : ITaskInfo
    {
        private readonly InnerPlayerInfo _playerInfo;
        private readonly IEventManager _eventManager;

        public TaskInfo(InnerPlayerInfo playerInfo, IEventManager eventManager, uint id, TaskData? task)
        {
            _playerInfo = playerInfo;
            _eventManager = eventManager;
            Id = id;
            Task = task;
        }

        public uint Id { get; internal set; }

        public TaskData? Task { get; internal set; }

        public bool Complete { get; internal set; }

        public void Serialize(IMessageWriter writer)
        {
            writer.WritePacked(Id);
            writer.Write(Complete);
        }

        public void Deserialize(IMessageReader reader)
        {
            Id = reader.ReadPackedUInt32();
            Complete = reader.ReadBoolean();
        }

        public async ValueTask CompleteAsync()
        {
            if (_playerInfo.Controller == null)
            {
                throw new EmpostorException("Can't complete a task that doesn't have a player assigned");
            }

            var player = _playerInfo.Controller;

            if (Complete)
            {
                throw new EmpostorException("Can't complete a task that is already completed");
            }

            Complete = true;

            // Send RPC.
            using var writer = player.Game.StartRpc(player.NetId, RpcCalls.CompleteTask);
            Rpc01CompleteTask.Serialize(writer, Id);
            await player.Game.FinishRpcAsync(writer);

            // Notify plugins.
            await _eventManager.CallAsync(new PlayerCompletedTaskEvent(player.Game, player.Game.GetClientPlayer(player.OwnerId)!, player, this));
        }
    }
}
