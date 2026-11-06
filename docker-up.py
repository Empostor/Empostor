#!/usr/bin/env python3
"""Generate Docker Compose port mappings from Empostor config.json and start it."""

import json
import os
import pathlib
import subprocess
import sys
import tempfile


ROOT = pathlib.Path(__file__).resolve().parent
CONFIG = pathlib.Path(os.environ.get("EMPOSTOR_CONFIG", ROOT / "config.json")).resolve()
COMPOSE = ROOT / "compose.yml"


def port(value, label):
    if isinstance(value, bool) or not isinstance(value, int) or not 1 <= value <= 65535:
        raise ValueError(f"{label} must be an integer from 1 to 65535")
    return value


def delta_port(value, label):
    if isinstance(value, int) and not isinstance(value, bool) and value == 0:
        return 0
    return port(value, label)


def port_mappings(config):
    if not isinstance(config, dict):
        raise ValueError("config.json must contain a JSON object")
    config = {key.lower(): value for key, value in config.items()}
    server = config.get("server", {})
    http = config.get("httpserver", {})
    if not isinstance(server, dict) or not isinstance(http, dict):
        raise ValueError("Server and HttpServer config entries must be JSON objects")
    server = {key.lower(): value for key, value in server.items()}
    http = {key.lower(): value for key, value in http.items()}

    mappings = set()
    http_enabled = http.get("enabled", True)
    if not isinstance(http_enabled, bool):
        raise ValueError("HttpServer.Enabled must be a JSON boolean")
    if http_enabled:
        http_port = port(http.get("listenport", 22023), "HttpServer.ListenPort")
        mappings.add(f"{http_port}:{http_port}/tcp")

    listen_port = port(server.get("listenport", 22023), "Server.ListenPort")
    public_port = port(server.get("publicport", 22023), "Server.PublicPort")
    mode = str(server.get("verificationmode", "DeltaPort")).lower()

    if mode in ("gamecode", "1"):
        game_ports = server.get("gamecodeports", [])
        if not isinstance(game_ports, list):
            raise ValueError("Server.GameCodePorts must be an array")
        internal_port = port(game_ports[0], "Server.GameCodePorts[0]") if game_ports else listen_port
        external_port = internal_port if game_ports else public_port
        mappings.add(f"{external_port}:{internal_port}/udp")
    elif mode in ("deltaport", "0"):
        start = delta_port(server.get("deltaportstart", 0), "Server.DeltaPortStart")
        end = delta_port(server.get("deltaportend", 0), "Server.DeltaPortEnd")
        if start > 0 and end >= start:
            mappings.add(f"{start}-{end}:{start}-{end}/udp")
        else:
            mappings.add(f"{public_port}:{listen_port}/udp")
    else:
        raise ValueError("Server.VerificationMode must be DeltaPort or GameCode")

    return sorted(mappings)


def main():
    if not CONFIG.is_file():
        default_config = ROOT / "src" / "Empostor.Server" / "config.json"
        CONFIG.parent.mkdir(parents=True, exist_ok=True)
        CONFIG.write_bytes(default_config.read_bytes())
        print(f"Created {CONFIG} from the repository default config.")

    with CONFIG.open(encoding="utf-8-sig") as stream:
        config = json.load(stream)

    mappings = port_mappings(config)
    override = "services:\n  empostor:\n    ports:\n"
    override += "".join(f'      - "{item}"\n' for item in mappings)

    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".yml", delete=False) as stream:
        stream.write(override)
        override_path = pathlib.Path(stream.name)

    try:
        command = ["docker", "compose", "-f", str(COMPOSE), "-f", str(override_path)]
        command.extend(sys.argv[1:] or ["up", "-d", "--build"])
        environment = dict(os.environ, EMPOSTOR_DOCKER_CONFIG=str(CONFIG))
        print("Publishing:")
        for item in mappings:
            print(f"  {item}")
        raise SystemExit(subprocess.call(command, cwd=ROOT, env=environment))
    finally:
        override_path.unlink(missing_ok=True)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"docker-up: {error}", file=sys.stderr)
        raise SystemExit(2)
