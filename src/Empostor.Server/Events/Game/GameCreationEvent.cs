using Empostor.Api;
using Empostor.Api.Events;
using Empostor.Api.Games;
using Empostor.Api.Games.Managers;
using Empostor.Api.Net;

namespace Empostor.Server.Events
{
    public class GameCreationEvent : IGameCreationEvent
    {
        private readonly IGameManager _gameManager;
        private GameCode? _gameCode;

        public GameCreationEvent(IGameManager gameManager, IClient? client)
        {
            _gameManager = gameManager;
            Client = client;
        }

        public IClient? Client { get; }

        public GameCode? GameCode
        {
            get => _gameCode;
            set
            {
                if (value.HasValue)
                {
                    if (value.Value.IsInvalid)
                    {
                        throw new EmpostorException("GameCode is invalid.");
                    }

                    if (_gameManager.Find(value.Value) != null)
                    {
                        throw new EmpostorException($"GameCode [{value.Value.Code}] is already used.");
                    }
                }

                _gameCode = value;
            }
        }

        public bool IsCancelled { get; set; }
    }
}
