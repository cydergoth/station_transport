#!/usr/bin/env bash
# Builds the plugin and copies it into the game's BepInEx plugins folder.
set -euo pipefail
GAME_DIR="${GAME_DIR:-$HOME/.local/share/Steam/steamapps/common/Star Valor}"
cd "$(dirname "$0")"
dotnet build -c Release -p:GameDir="$GAME_DIR"
if [ ! -d "$GAME_DIR/BepInEx" ]; then
  echo "BepInEx is not installed in $GAME_DIR - see README.md" >&2
  exit 1
fi

# The mod needs the Base Building DLC (Steam app 2755940). The DLC only unlocks content that ships
# with the base game, so there are no DLC files to look for. Instead, look for the DLC's ownership
# ticket in each Steam account's local config. That's undocumented Steam internals, so only warn.
STEAM_DIR="${STEAM_DIR:-$HOME/.local/share/Steam}"
DLC_APPID=2755940
has_dlc_ticket() {
  local config
  for config in "$STEAM_DIR"/userdata/*/config/localconfig.vdf; do
    [ -f "$config" ] || continue
    if awk -v id="\"$DLC_APPID\"" '
      /^\t"apptickets"$/ { inside = 1; next }
      inside && /^\t}/    { inside = 0 }
      inside && $1 == id  { found = 1 }
      END { exit !found }' "$config"; then
      return 0
    fi
  done
  return 1
}
if ! has_dlc_ticket; then
  echo "Warning: couldn't confirm that a Steam account in $STEAM_DIR owns the Base Building DLC." >&2
  echo "Station Transport requires it: trade routes run between your own stations, which the DLC adds." >&2
  echo "Installing anyway. Set STEAM_DIR if Steam lives somewhere else." >&2
fi

mkdir -p "$GAME_DIR/BepInEx/plugins/StationTransport"
cp -r bin/Release/StationTransport.dll bin/Release/Language "$GAME_DIR/BepInEx/plugins/StationTransport/"
echo "Installed to $GAME_DIR/BepInEx/plugins/StationTransport/"
