#!/usr/bin/env bash
set -euo pipefail

MODID="vs-dope"
DEST="${HOME}/.config/VintagestoryData/Mods/${MODID}"

cd "$(dirname "$0")"

dotnet build

rm -rf "$DEST"
mkdir -p "$DEST/assets"
cp modinfo.json "$DEST/"
cp "bin/Debug/${MODID}.dll" "$DEST/"
cp -r "assets/${MODID}" "$DEST/assets/"

echo "Deployed to $DEST (restart game for DLL changes)"
