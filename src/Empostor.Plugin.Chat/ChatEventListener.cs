using Empostor.Api.Events;
using Empostor.Api.Events.Player;

namespace Empostor.Plugin.Chat;

public sealed class ChatEventListener : IEventListener
{
    private readonly ChatService _chatService;

    public ChatEventListener(ChatService chatService)
    {
        _chatService = chatService;
    }

    [EventListener]
    public void OnPlayerChat(IPlayerChatEvent e)
    {
        _chatService.HandleChatMessage(e);
    }
}
