#!/usr/bin/env bash
# Builds every server RID, packs the NuGet packages and collects the results in ./release.
#
# Server archives follow the build.cake naming so the artifacts match what CI publishes:
#   Empostor-Server_<version>_<rid>.zip        (win-x64)
#   Empostor-Server_<version>_<rid>.tar.gz     (linux-*, osx-x64)
#
# Note: Git Bash here has no `zip` binary, only `unzip`, so the win-x64 zip is produced by the
# small dotnet helper in tools/zipdir. tar handles the .tar.gz side natively.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$ROOT/release"
SERVER_PROJ="$ROOT/src/Empostor.Server/Empostor.Server.csproj"
API_PROJ="$ROOT/src/Empostor.Api/Empostor.Api.csproj"
SERVER_BIN="$ROOT/src/Empostor.Server/bin/Release/net8.0"
API_BIN="$ROOT/src/Empostor.Api/bin/Release"

# Version comes from the props file, the same source build.cake reads.
VERSION="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$ROOT/src/Directory.Build.props" | head -1)"
if [ -z "$VERSION" ]; then
  echo "error: could not read VersionPrefix from src/Directory.Build.props" >&2
  exit 1
fi

# Windows is the only zip target; everything else is tar.gz.
RIDS_ZIP="win-x64"
RIDS_TGZ="linux-x64 linux-arm64 linux-arm osx-x64"

echo "==> Version $VERSION"
echo "==> Output $OUT"
rm -rf "$OUT"
mkdir -p "$OUT"

# ---------------------------------------------------------------- server
for rid in $RIDS_ZIP $RIDS_TGZ; do
  echo "==> Publishing server ($rid)"
  dotnet publish "$SERVER_PROJ" -c Release -r "$rid" --nologo -v minimal
done

# ---------------------------------------------------------------- api
echo "==> Packing Empostor.Api"
dotnet pack "$API_PROJ" -c Release --nologo -v minimal

# ---------------------------------------------------------------- collect
# Build the zip helper once up front: `dotnet run` swallows --nologo/-v as its own options, so
# the app arguments only survive when they follow a bare `--`.
echo "==> Building zip helper"
dotnet build "$ROOT/tools/zipdir/zipdir.csproj" -c Release --nologo -v quiet

# The publish step leaves a zip behind from an earlier build.cake run; use only the RID folders.
for rid in $RIDS_ZIP; do
  dir="$SERVER_BIN/$rid/publish"
  if [ ! -d "$dir" ]; then
    echo "error: missing publish output $dir" >&2
    exit 1
  fi
  echo "==> Zipping server ($rid)"
  dotnet "$ROOT/tools/zipdir/bin/Release/net8.0/zipdir.dll" \
    "$dir" "$OUT/Empostor-Server_${VERSION}_${rid}.zip"
done

for rid in $RIDS_TGZ; do
  dir="$SERVER_BIN/$rid/publish"
  if [ ! -d "$dir" ]; then
    echo "error: missing publish output $dir" >&2
    exit 1
  fi
  echo "==> Tarball server ($rid)"
  # -C publishes the folder's contents, so the archive has no wrapping directory.
  tar -czf "$OUT/Empostor-Server_${VERSION}_${rid}.tar.gz" -C "$dir" .
done

# Only the release build (no -dev / -ci suffix) is collected; the others are stale local output.
echo "==> Collecting NuGet packages"
shopt -s nullglob
found=0
for pkg in "$API_BIN"/Empostor.Api.*.nupkg "$API_BIN"/Empostor.Api.*.snupkg; do
  name="$(basename "$pkg")"
  case "$name" in
    *-dev.nupkg|*-dev.snupkg|*-ci.*.nupkg|*-ci.*.snupkg) continue ;;
  esac
  cp -f "$pkg" "$OUT/"
  found=1
done
shopt -u nullglob
if [ "$found" -eq 0 ]; then
  echo "error: no release NuGet package found in $API_BIN" >&2
  exit 1
fi

echo
echo "==> Done. Contents of release/:"
ls -lh "$OUT"
