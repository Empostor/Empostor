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

It is also recommended to [set up a Reverse Proxy](https://empostor.github.io/Http-server#use-a-reverse-proxy) (for HTTPS connections).

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
