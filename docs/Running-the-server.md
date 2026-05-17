# Running the server

There are currently two install methods for Empostor: You can install it normally or inside of a Docker container. If you do not have a particular preference, we recommend the normal installation over the Docker container method.

## General remarks for both install methods

This section applies to both the normal installation as well as the Docker (Compose) installation

To connect to the server, you need to configure and install a region file on https://impostor.github.io/Impostor/

Among Us connects to the server using two network services: the (TCP) HTTP service points Among Us to the UDP service, then the UDP service hosts the actual game traffic. Because of this, Empostor uses port 22023 using **both** the TCP and UDP protocols.

To connect to your Among Us version from another PC, you need to set up a HTTP reverse proxy to support HTTPS. If you're just running a version of Empostor for testing Setup instructions for this are in the [Http Server documentation](Http-server.md).

If you want to test if your Among Us HTTP server is working, open `http://localhost:22023` (assuming you're using the default settings, change the IP and port respectively) in a browser

Depending on your host you may also need to port forward Empostor to the internet or pass Empostor traffic by your firewall. Port 22023 UDP needs to be accessible for everyone that wants to play on the server, then you also need to portforward your HTTP reverse proxy or port 22023 TCP if you don't use a reverse proxy. As port forwarding changes per host or router configuration, port forwarding is not covered by this guide.

## Normal installation

1. Install [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0). We recommend either the ASP.NET Core Runtime or the SDK. The SDK is necessary in case you want to develop Empostor or Empostor plugins.
2. Download the [latest release](https://github.com/HayashiUme/Empostor/releases) or the [latest CI build](https://nightly.link/HayashiUme/Empostor/workflows/ci/master). Note that Empostor is built for multiple CPU-architectures and operating systems, you most likely want the x64 version, unless you are running on a Raspberry Pi or another device/VPS with an Arm processor.
3. Extract the zip.
4. Modify `config.json` to your liking. Documentation can be found [here](Server-configuration.md). You need to at least change `PublicIp` to the address people will connect to your server to.
5. Run `Impostor.Server` (Linux/macOS) or `Impostor.Server.exe` (Windows)
6. Set up a reverse proxy to support HTTPS, so you can connect to your server from another device. See [reverse proxy configuration](Http-server.md)
7. (OPTIONAL - Linux) Configure a systemd definition file and enable the service to start on boot, see [systemd configuration](Server-configuration.md#systemd)

## Ues Dockerfile with compose.yml
Place the `Docker.Postfix.zip` file from the distribution into the pre-built files provided by the distribution, then run
```bash
# Enter File Path
cd /yourdir/empostor/
# Build and Run
docker-compose up -d --build
```
Then the docker will run successfully.