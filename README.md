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
- Admin Panel (dashboard)
- Plugin support
- Server-side anticheat

## Getting Started 

To run the server, you need:

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

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
| `Server` | Public IP/port, listen address and the dynamic (delta) UDP port range `DeltaPortStart`..`DeltaPortEnd` |
| `HttpServer` | Web API, admin panel and matchmaking token endpoint (`Enabled`, `ListenIp`, `ListenPort`) |
| `AntiCheat` | Server-side anticheat, including the [IP request rate limit](#ip-request-rate-limit) |
| `Timeout` | Spawn and connection timeouts |
| `Compatibility` | Game version compatibility rules |
| `Debug` | Game recorder |
| `AuthApi` | Friend code resolution (`Mode`: `GameServices`, `Ume`, `Both` or `Off`) |
| `Admin` | Admin panel password and marketplace URL |
| `HPLP` | HPLP public region listing |

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
| `EnableIpRateLimit` | `true` | IP request rate limit (below) |
| `IpRateLimitRequests` | `30` | Max TCP (HTTP) requests per IP per window, `0` disables the limit |
| `IpRateLimitWindowMinutes` | `5` | Length of the rate limit window in minutes |

#### Ip request rate limit

Within one window (5 minutes by default) the same IP may only make `IpRateLimitRequests` (default `30`) TCP (HTTP) requests — this protects the HTTP API against connection/request flooding. When the limit is exceeded:

- Every normal endpoint answers with `429 Too Many Requests` and a localized hint, chosen from the request's `Accept-Language` header, e.g. `请求过于频繁请 5 分钟后重试`. The `Retry-After` header says how many seconds are left.
- `POST /api/user` **and** `PUT`/`GET /api/games` (the whole join flow of the game client: matchmaking token + FindHost) are **never rejected over HTTP** — failing them there would only leave the client on a black screen, which just logs `CoSendRequest failed` and hangs. Both still count against the quota, and while an IP is over quota the server also skips the outbound friend code API calls (a cached/fallback code is used instead) so the endpoint cannot be abused for amplification. To keep a flooding IP from draining the dynamic port range, such joins are **not given a pool port**: they all receive the same shared *reject port*, a single pre-bound UDP socket whose only job is to answer the handshake with the localized hint in the client's own language (`请求过于频繁请 5 分钟后重试`) and close it again — the player is told why they cannot join instead of seeing a black screen, and only one socket is used no matter how many requests arrive. `GamesController` echoes the token's port back, so the client is naturally sent to that reject port. In fixed-port mode (`DeltaPortStart=0`) the main port is used instead.
- `/admin` and `/api/admin` are neither counted nor limited, so the dashboard can never lock the operator out.

The message lives under the key `ratelimit.too_frequent` in `Languages/*.json` (and in the built-in defaults in `LanguageService.cs`), so you can reword or translate it there. Set `IpRateLimitRequests` to `0` to turn the whole feature off.

Empostor usually runs behind a CDN or reverse proxy, so the real client IP is taken from the `X-Real-IP` header, then from the first entry of `X-Forwarded-For`, and only afterwards from the socket address. Make sure your CDN/proxy forwards one of these headers — otherwise every player would share the proxy IP's quota. Note that the game client polls some endpoints (e.g. `/api/games`) while you play: if `30` per 5 minutes is too tight for your players, raise `IpRateLimitRequests` (for example to `120`).

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
