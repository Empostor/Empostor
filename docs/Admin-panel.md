# Admin Panel

Empostor includes a built-in web-based admin panel available at `http://your-server:22023/admin`.

## Authentication

The panel is protected by a password set in `config.json`:

```json
"Admin": {
  "Password": "changeme",
  "MarketplaceUrl": "https://raw.githubusercontent.com/your-org/your-repo/main/marketplace/plugins.json"
}
```

On first visit the login page is shown. After a successful login a session cookie valid for 8 hours is set. Clicking **Sign out** clears it immediately.

All API endpoints under `/api/admin/` return `401 Unauthorized` if the cookie is absent or incorrect.

---

## Overview

The main dashboard shows at a glance:

- Total and public game count
- Active games (in progress)
- Connected player count
- Total ban count (IP + friend code)
- Server uptime

A live game list is displayed below the stat cards, refreshing every 3 seconds.

---

## Games

Lists all active rooms with:

- Room code
- State (NotStarted / Starting / Started / Ended)
- Visibility (Public / Private)
- Map name
- Player count vs. max players
- Host name and friend code
- All players in the room (hover for friend code and IP)

---

## Clients

Lists all connected clients with:

- Client ID
- Name
- Friend code
- IP address
- Among Us client version
- Platform
- Whether they are in a game, and which room code
- Reactor mod count badge (if the client runs Reactor)

Clicking **Detail** on any client row opens a side panel showing:

- Full player identity (name, friend code, PUID, IP, client version, language, platform)
- Current game code
- Reactor protocol version and full mod list (mod ID, version, required flag)

---

## Broadcast

Sends a chat message to every active game on behalf of the host of that game. The message is prefixed with `[Server]`.

---

## Kick

Removes a player from the server without banning. The player can reconnect immediately.

Players can be kicked by entering a Client ID, or by using the **Quick Kick** table which lists all connected clients with a one-click button.

---

## Ban

### Ban by IP

Bans an IP address and immediately disconnects all clients connected from that address. The reason is saved to `bans.json` and persists across server restarts.

### Ban by Friend Code

Bans a specific friend code and immediately disconnects any client currently connected with it.

Both ban types can be removed from the **Ban List** tab at any time.

---

## Ban List

Shows all active IP bans and friend code bans with:

- Banned value (IP or friend code)
- Reason
- Timestamp

Each entry has an **Unban** button that takes effect immediately.

---

## Message

Sends a targeted chat message to a specific game room by code. Useful for communicating with a single room without broadcasting server-wide.

---

## End Game

Force-terminates a game by kicking all players in it. The room is destroyed immediately. A confirmation dialog is shown before the action is executed.

The tab also lists all active games with a one-click **End** button per row.

---

## Privacy

Toggles a game between Public and Private. This affects whether the room appears in the public lobby list.

---

## Plugin Marketplace

Fetches the plugin list from the URL configured in `Admin.MarketplaceUrl` (a raw GitHub JSON file). For each plugin, the latest version, supported Impostor version, and description are shown.

Clicking **Install** downloads the `.dll` to the `plugins/` folder. The server must be restarted to load the new plugin.

If the plugin was built for a different Impostor version, a warning is displayed but installation is not blocked.

---

## Updates

Queries the GitHub Releases API for the latest Empostor release and compares it with the running version. If a newer version is available, a link to the release page is shown. The server does not update itself automatically.

The GitHub repository checked is `Empostor/Empostor` by default. If you maintain a fork, this is hardcoded in `MarketplaceController.cs`.

---

## Reports

Shows the last 200 player reports submitted in-game (via the Among Us report button):

| Column | Description |
|--------|-------------|
| Time | UTC timestamp of the report |
| Game | Room code |
| Reporter | Name and friend code of the reporting player |
| Reported | Name and friend code of the reported player |
| Reason | `InappropriateName`, `InappropriateChat`, `Cheating_Hacking`, `Harassment_Misconduct` |
| Outcome | `NotReportedUnknown`, `NotReportedNoAccount`, `Reported`, etc. |

Reports are held in memory and cleared on server restart. Plugins can listen to `IPlayerReportEvent` to persist reports or trigger further actions.

---

## Server Info

Displays runtime details:

- Server start time and uptime
- Process ID
- .NET runtime version and OS
- Current ban and auth session counts
