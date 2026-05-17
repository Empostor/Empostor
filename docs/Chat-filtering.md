# Chat Filtering

Empostor can filter in-game chat messages by blocking specific words and limiting message spam. This helps server administrators maintain a healthier chat environment.

This feature is **disabled by default**. Enable it in `config.json` or from the admin panel to activate filtering.

## Admin Panel

A **Chat Filter** tab is available in the admin panel under the Monitor section. It allows you to manage all settings at runtime without editing `config.json` or restarting the server:

- **Enable/Disable** filtering with a checkbox
- **Add/Remove blocked words** — type a word and click Add, or click the × on any tag to remove it
- **Spam settings** — adjust the message threshold and time window
- **Block/log toggle** — choose whether to block messages or only log warnings

Settings changed via the admin panel take effect immediately but are runtime-only — they are lost on server restart. For permanent settings, edit `config.json`.

## Configuration

Add the `ChatFilter` section to your `config.json`:

```json
{
  "ChatFilter": {
    "Enabled": false,
    "BlockedWords": [],
    "BlockMessage": true,
    "SpamThreshold": 5,
    "SpamWindowSeconds": 10
  }
}
```

| Key | Default | Description |
| :--- | :--- | :--- |
| **Enabled** | `false` | Whether chat filtering is enabled. |
| **BlockedWords** | `[]` | A list of words to block. Matching is **case-insensitive** and uses substring matching (e.g., blocking `"bad"` also blocks `"badword"`). |
| **BlockMessage** | `true` | Whether to actually block filtered messages. When `true`, the message is cancelled and never reaches other players. When `false`, the message is allowed through but a warning is logged. |
| **SpamThreshold** | `5` | Number of messages allowed within the spam window before rate limiting kicks in. |
| **SpamWindowSeconds** | `10` | The sliding time window in seconds for spam detection. |

## How Filtering Works

### Word Filter

When a player sends a chat message, it is checked against the `BlockedWords` list:

1. The message text is compared against each blocked word (case-insensitive substring match).
2. If a match is found and `BlockMessage` is `true`, the message is blocked — other players will not see it.
3. If `BlockMessage` is `false`, the message goes through but a warning is logged.

### Spam Filter

Each player has a per-player sliding window to detect rapid messaging:

1. Every chat message timestamp is recorded per player.
2. If a player sends `SpamThreshold` or more messages within the last `SpamWindowSeconds` seconds, the message is handled according to `BlockMessage`.
3. Old timestamps outside the window are automatically discarded.

Both filters run independently — a message can be blocked by either the word filter or the spam filter.

## Example

To block profanity and prevent players from sending more than 3 messages in 5 seconds:

```json
{
  "ChatFilter": {
    "Enabled": true,
    "BlockedWords": ["badword1", "badword2"],
    "BlockMessage": true,
    "SpamThreshold": 3,
    "SpamWindowSeconds": 5
  }
}
```

## Troubleshooting

- **Messages are not being blocked**: Ensure `Enabled` is `true` and `BlockedWords` contains entries. Remember that matching is substring-based — short words may match unexpectedly.
- **False positives**: Use specific, longer words in your `BlockedWords` list. Substring matching means `"ass"` would match `"pass"` and `"classic"`.
- **Legitimate players get spam-blocked**: Increase `SpamThreshold` or decrease `SpamWindowSeconds`. The defaults (5 messages / 10 seconds) are generous for normal conversation.
