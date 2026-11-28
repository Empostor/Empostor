# Empostor
Empostor is an open-source private server implementation for Among Us.

[![Discord](https://img.shields.io/badge/Discord-%235865F2.svg?style=flat&logo=discord&logoColor=white)](https://dsc.gg/empostor)
[![QQ](https://img.shields.io/badge/QQ-Group-black?style=flat-square)](https://qm.qq.com/q/GeX3Q0Ft0k)
[![GitHub license](https://badgen.net/github/license/Empostor/Empostor)](https://github.com/Empostor/Empostor/blob/main/LICENSE)
[![GitHub latest commit](https://badgen.net/github/last-commit/Empostor/Empostor)](https://github.com/Empostor/Empostor/commit/)
[![GitHub all releases](https://img.shields.io/github/downloads/Empostor/Empostor/total.svg)](https://github.com/Empostor/Empostor/releases/)
[![GitHub contributors](https://badgen.net/github/contributors/Empostor/Empostor)](https://github.com/Empostor/Empostor/graphs/contributors/)
[![GitHub total-pull-requests](https://badgen.net/github/prs/Empostor/Empostor)](https://github.com/Empostor/Empostor/pull/)

## Features
- FriendCode support (authentication)
- Dynamic ports
- Plugin support
- Server-side anticheat

## Getting Started 

To run the server, you need:

- [.NET 10.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Download the build you'd like from the [releases](https://github.com/Empostor/Empostor/releases). You most likely want the x64 build. Use the arm64 build if you are running Empostor on a Raspberry Pi, another ARM-based SBC, or an ARM VPS.

Unzip the file and go to config.json. To make your server playable for other devices, replace the "PublicIp" field with your actual public IP address.

It is also recommended to [set up a Reverse Proxy](https://empostor.github.io/get-started/reverse-proxy#use-a-reverse-proxy) (for HTTPS connections).


## Client Setup

### Windows
1. Go [here](https://empostor.github.io/empostor) and enter your server's IP/domain, port and name. Press "Download server file"
2. Press Win + R and enter this (leave the quotation marks):
```cmd
"%userprofile%\AppData\LocalLow\Innersloth\Among Us"
```
3. Put the new regionInfo.json file you downloaded in the folder. Overwrite the existing file in the folder.
4. Launch Among Us. If everything worked, your server should appear in the regions list!

### Android/iOS
1. Launch Among Us. When you reach the main menu, close the app.
2. Go [here](https://empostor.github.io/empostor) and enter your server's IP/domain, port and name.
3. Scroll until you see Instructions and press the Android or Apple logo.
4. Press "Open in Among Us". Among Us should open.
5. Your server should appear in the regions list!

## Configuration

All settings live in `config.json` next to the server binary (a full sample is [`src/Empostor.Server/config.json`](src/Empostor.Server/config.json)). Unknown keys are ignored, so an old config keeps working after an update.

| Section | Purpose |
| --- | --- |
| `Server` | Public IP/port, listen address and the client verification mode (`VerificationMode`, `DeltaPortStart`..`DeltaPortEnd`) |
| `HttpServer` | Web API, admin panel and matchmaking token endpoint (`Enabled`, `ListenIp`, `ListenPort`) |
| `AntiCheat` | Server-side anticheat, including the [IP request rate limit](#ip-request-rate-limit) |
| `Timeout` | Spawn and connection timeouts |
| `Compatibility` | Game version compatibility rules |
| `Debug` | Game recorder |
| `Admin` | Admin panel password and marketplace URL |
| `HPLP` | HPLP public region listing |

### Verification mode

Empostor has to know which HTTP authentication (`POST /api/user`) belongs to which raw UDP connection, because the game's UDP handshake carries no token, PUID or game code. `Server.VerificationMode` selects how:

| Mode | Behaviour |
| --- | --- |
| `DeltaPort` (default) | Every player gets a unique dynamic UDP port from the `DeltaPortStart`..`DeltaPortEnd` pool. The port itself is the proof of identity. |
| `GameCode` | **No dynamic ports at all** - only the shared listen port (`GameCodePorts[0]`, or `ListenPort`) is used. Lobby listings issue one-use alias codes bound to authenticated sessions; legacy joins use a short-lived ticket matched by real game code and player name. |
| `Disable` | Uses the original fixed-port matchmaking flow. No EOS verification, friend-code lookup, dynamic-port matching, or GameCode challenge is performed. |

| Key | Default | Description |
| --- | --- | --- |
| `VerificationMode` | `DeltaPort` | `DeltaPort`, `GameCode`, or `Disable` |
| `GameCodePorts` | `[]` | Ports for `GameCode` mode; only the first is bound. Empty falls back to `ListenPort`/`PublicPort` |
| `TemporaryGameCodeLifetimeSeconds` | `180` | How long an issued alias or join ticket stays valid |

In `GameCode` and `Disable` modes the port pool is disabled and `DeltaPortStart`/`DeltaPortEnd` are ignored. Friend codes in the two verification modes are resolved through Innersloth only.

The game client validates the game code of every message it receives against the code it joined with and silently drops mismatches, and the UDP handshake carries no token/PUID/game code. `GameCode` mode therefore uses the code itself as the token:

1. A lobby listing returns a server-generated alias game code bound to the authenticated session and real game code.
2. The first UDP `JoinGame(alias)` consumes the alias and binds the connection to its authenticated session. Legacy joins consume a short-lived HTTP ticket matched by real game code and player name.
3. For alias joins, the server responds using the alias expected by the client, then sends the real game code for subsequent traffic.

Hosts are bound to a short-lived `PUT /api/games` ticket by player name. Legacy `POST /api/games` joins are similarly matched by real game code and player name.


### AntiCheat

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Master switch of the anticheat |
| `BanIpFromGame` | `true` | Also block banned IPs on the game (UDP) side |
| `AllowCheatingHosts` | `Never` | What happens to hosts that cheat |
| `EnableGameFlowChecks`, `EnableMustBeHostChecks`, `EnableOwnershipChecks`, `EnableRoleChecks`, `EnableTargetChecks` | `true` | Game flow / state validation |
| `EnableColorLimitChecks`, `EnableNameLimitChecks`, `EnableItemLimitChecks` | `true` | Lobby limits (colors, names, items) |
| `ForbidProtocolExtensions` | `true` | Reject unknown/extension protocol messages |
| `EnablePacketSizeChecks`, `PacketSizeLimit` | `true`, `1203` | Maximum inbound packet size |
| `EnableRateLimits` | `true` | IP request rate limit and game-side rate limits (below) |

## Contributing
Please read the [contributing guidelines](https://github.com/Empostor/Empostor/blob/main/CONTRIBUTING.md).

You're welcome to open a pull request/issue!

## Documentation
The documentation is available [here](https://empostor.github.io)!

## License
This project is licensed under the [GPL-v3.0 License](https://github.com/Empostor/Empostor/blob/main/LICENSE).

## Credits

- [Impostor](https://github.com/Impostor/Impostor)
- [Next-Impostor](https://github.com/BunchHanpiDev/Next-Impostor)
- [NextFast.Hazel](https://github.com/Next-Fast/NextFast.Hazel)
- [Reactor.Impostor](https://github.com/NuclearPowered/Reactor.Impostor)
