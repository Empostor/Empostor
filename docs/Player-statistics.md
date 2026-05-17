# Player Statistics

Empostor can track per-player game statistics including wins, kills, tasks completed, and more. Statistics are viewable through the admin panel and via the `/stat` in-game command.

This feature is **disabled by default**. Enable it in `config.json` to begin tracking.

## Configuration

Add the `PlayerStats` section to your `config.json`:

```json
{
  "PlayerStats": {
    "Enabled": false,
    "PersistToFile": true
  }
}
```

| Key | Default | Description |
| :--- | :--- | :--- |
| **Enabled** | `false` | Whether player statistics tracking is enabled. |
| **PersistToFile** | `true` | Whether to save statistics to `player_stats.json` on disk. If disabled, stats are kept in memory only and lost on server restart. |

## Tracked Statistics

The following metrics are recorded per player (keyed by Friend Code):

| Stat | Description |
| :--- | :--- |
| Games Played | Total number of games the player participated in |
| Wins | Number of games won as a crewmate |
| Losses | Number of games lost as a crewmate |
| Impostor Wins | Number of games won as an impostor |
| Kills | Total kills performed |
| Deaths | Total times killed |
| Tasks Completed | Total tasks completed |
| Times Exiled | Total times voted out |

## Admin Panel

When enabled, a **Statistics** tab appears in the admin panel under the Monitor section. It displays a ranked table sorted by games played, with columns for all tracked stats. Use the **Refresh** button to reload data and **Reset All** to clear all statistics (requires confirmation).

## In-Game Command

Players can check their own stats by typing `/stat` (or `/stats`, `/mystats`) in the in-game chat. The response is sent privately and is localized based on the player's language.

## Localization

The `/stat` command response supports all languages configured in Empostor's language system. If no translation exists for the player's language, English is used as a fallback.

## API Endpoints

The admin panel uses these API endpoints:

| Method | Path | Description |
| :--- | :--- | :--- |
| `GET` | `/api/admin/player/stats` | Returns all player statistics sorted by games played. Response: `{"enabled": true, "players": [...]}` |
| `GET` | `/api/admin/player/stats/{friendCode}` | Returns statistics for a single player by Friend Code. |
| `POST` | `/api/admin/player/stats/reset` | Clears all player statistics. Requires admin authentication. |

## File Persistence

When `PersistToFile` is enabled, statistics are saved to `player_stats.json` in the server directory. The file is updated after each game ends, following the same pattern as the ban and report stores.
