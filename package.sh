#!/usr/bin/env bash
# Builds the plugin and creates the release zip in dist/.
# The zip unpacks straight into the Star Valor game folder.
set -euo pipefail
GAME_DIR="${GAME_DIR:-$HOME/.local/share/Steam/steamapps/common/Star Valor}"
cd "$(dirname "$0")"
VERSION=$(sed -n 's/.*public const string Version = "\(.*\)";.*/\1/p' src/Plugin.cs)
dotnet build -c Release -p:GameDir="$GAME_DIR" -p:Version="$VERSION"

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
PLUGIN_DIR="$STAGE/BepInEx/plugins/StationTransport"
mkdir -p "$PLUGIN_DIR"
cp -r bin/Release/StationTransport.dll bin/Release/Language README.md LICENSE "$PLUGIN_DIR/"

mkdir -p dist
ZIP="$PWD/dist/StationTransport-$VERSION.zip"
rm -f "$ZIP"
(cd "$STAGE" && zip -qr "$ZIP" BepInEx)
echo "$ZIP"
