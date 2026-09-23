#!/usr/bin/env bash
# Cross-platform (Linux/macOS) deploy for vs-dope.
# Auto-finds the Vintage Story install dir, sets VINTAGE_STORY, builds, and copies
# mod files into the game's Mods data folder. No user interaction required.
set -euo pipefail

MODID="vs-dope"
CONFIG="${VS_BUILD_CONFIG:-Debug}"

cd "$(dirname "$0")"

# --- Locate the install dir (folder containing VintagestoryAPI.dll) -----------
api_marker() { [ -f "${1%/}/VintagestoryAPI.dll" ]; }

find_install_dir() {
  # 1. Respect an already-valid VINTAGE_STORY env var.
  if [ -n "${VINTAGE_STORY:-}" ] && api_marker "$VINTAGE_STORY"; then
    printf '%s\n' "${VINTAGE_STORY%/}"; return 0
  fi

  local cands=(
    /opt/vintagestory
    /usr/local/vintagestory
    "${HOME}/.local/share/Steam/steamapps/common/Vintage Story"
    "${HOME}/GOG Games/Vintage Story"
    "${HOME}/Games/Vintage Story"
  )

  # 2. Enumerate every Steam library via libraryfolders.vdf (handles extra drives).
  local vdf line p
  for vdf in \
    "${HOME}/.steam/root/libraryfolders.vdf" \
    "${HOME}/.local/share/Steam/libraryfolders.vdf"; do
    [ -f "$vdf" ] || continue
    while IFS= read -r line; do
      case "$line" in *'"path"'*) ;; *) continue ;; esac
      p="$(printf '%s' "$line" | sed -E 's/.*"path"[[:space:]]*"([^"]+)".*/\1/')"
      [ -n "$p" ] && cands+=("${p%/}/steamapps/common/Vintage Story")
    done < "$vdf"
  done

  local c
  for c in "${cands[@]}"; do
    if api_marker "$c"; then printf '%s\n' "${c%/}"; return 0; fi
  done

  # 3. Bounded fallback search under common roots.
  local root hit
  for root in "${HOME}/.local/share/Steam" /opt /usr/local; do
    [ -d "$root" ] || continue
    hit="$(find "$root" -maxdepth 7 -name VintagestoryAPI.dll 2>/dev/null | head -n1)"
    if [ -n "$hit" ]; then printf '%s\n' "$(dirname "$hit")"; return 0; fi
  done

  return 1
}

INSTALL_DIR="$(find_install_dir || true)"
if [ -z "${INSTALL_DIR:-}" ]; then
  echo "ERROR: Could not locate Vintage Story install dir (VintagestoryAPI.dll)." >&2
  echo "Set VINTAGE_STORY=/path/to/vintagestory and re-run." >&2
  exit 1
fi

export VINTAGE_STORY="$INSTALL_DIR"
echo "Install dir : $VINTAGE_STORY"

# --- Data Mods destination ----------------------------------------------------
DEST="${XDG_CONFIG_HOME:-$HOME/.config}/VintagestoryData/Mods/${MODID}"
echo "Deploy dest : $DEST"

# --- Build --------------------------------------------------------------------
dotnet build -c "$CONFIG"

DLL="bin/${CONFIG}/${MODID}.dll"
if [ ! -f "$DLL" ]; then
  echo "ERROR: build did not produce $DLL" >&2
  exit 1
fi

# --- Copy ---------------------------------------------------------------------
rm -rf "$DEST"
mkdir -p "$DEST/assets"
cp modinfo.json "$DEST/"
cp "$DLL" "$DEST/"
cp -r "assets/${MODID}" "$DEST/assets/"

echo "Deployed to $DEST (restart game for DLL changes; assets hot-reload with F3+R)"
