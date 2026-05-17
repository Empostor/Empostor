# Discord Webhook Integration

Empostor can send game events to a Discord channel via [webhooks](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks). Each event is sent as a rich embed with relevant details.

This feature is **disabled by default**. Enable it in `config.json` and provide a webhook URL to activate it.

## Configuration

Add the `DiscordWebhook` section to your `config.json`:

```json
{
  "DiscordWebhook": {
    "Enabled": false,
    "WebhookUrl": "",
    "NotifyOnGameCreated": true,
    "NotifyOnBan": true,
    "NotifyOnReport": true,
    "NotifyOnPlayerJoin": false,
    "NotifyOnGameEnded": false
  }
}
```

| Key | Default | Description |
| :--- | :--- | :--- |
| **Enabled** | `false` | Whether the Discord webhook integration is enabled. |
| **WebhookUrl** | `""` | The Discord webhook URL to POST messages to. Create one in Discord under Channel Settings → Integrations → Webhooks. |
| **NotifyOnGameCreated** | `true` | Send a notification when a new game lobby is created. |
| **NotifyOnBan** | `true` | Send a notification when a player is banned from the server. |
| **NotifyOnReport** | `true` | Send a notification when a player reports a dead body in-game. |
| **NotifyOnPlayerJoin** | `false` | Send a notification when a player joins a game lobby. |
| **NotifyOnGameEnded** | `false` | Send a notification when a game ends. |

## Events

Each notification is sent as a Discord embed with a colored sidebar:

| Event | Embed Color | Fields |
| :--- | :--- | :--- |
| Game Created | Green | Game code, host name, host friend code |
| Player Banned | Red | Player name, friend code, game code |
| Player Reported | Yellow | Reporter, reported player, friend codes, game code, reason |
| Player Joined | Green | Player name, friend code, game code, player count |
| Game Ended | Purple | Game code, result, player count |

## Rate Limiting

To avoid hitting Discord's rate limits, duplicate events for the same game within 2 seconds are suppressed.

## Troubleshooting

- **No messages appear in Discord**: Verify `WebhookUrl` is correct and the webhook is active in your Discord channel. Check the server logs for `[Discord]` warnings.
- **Webhook returns HTTP errors**: Discord may rate-limit or the webhook URL may be invalid. The listener logs the HTTP status code on failure.
- **Events trigger but some fields are blank**: Friendly names and friend codes are read from client data — some fields may be empty (`—`) if the client hasn't fully connected yet.
