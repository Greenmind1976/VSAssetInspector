#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# Build + Install VSAssetInspector into Vintage Story 1.22.0-rc.8
# Present on 1.21 branch for convenience when forward-porting fixes.
###############################################################################

MOD_ID="vsassetinspector"
PROJECT_DIR="VSAssetInspector"
PROJECT_FILE="$PROJECT_DIR/VSAssetInspector.csproj"
TARGET_FRAMEWORK="net10.0"
MOD_BUILD_DIR="$PROJECT_DIR/bin/Debug/$TARGET_FRAMEWORK/Mods/mod"
VS_APP_DIR="/Applications/Vintage Story 1.22.0-rc.8.app"
VS_MODS_DIR="$VS_APP_DIR/Mods"
VS_LAUNCHER="$HOME/bin/vs-1.22.0-rc.8"

rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"

echo "Deleting installed mod dir: $VS_MODS_DIR/$MOD_ID"
rm -rf "$VS_MODS_DIR/$MOD_ID"

if [[ -e "$VS_MODS_DIR/$MOD_ID" ]]; then
  echo "ERROR: Mod dir still exists: $VS_MODS_DIR/$MOD_ID" >&2
  exit 1
fi

if [[ ! -d "$VS_APP_DIR" ]]; then
  echo "ERROR: Vintage Story app not found: $VS_APP_DIR" >&2
  exit 1
fi

VINTAGE_STORY="$VS_APP_DIR" dotnet build "$PROJECT_FILE" -f "$TARGET_FRAMEWORK" -p:NuGetAudit=false

if [[ ! -d "$MOD_BUILD_DIR" ]]; then
  echo "ERROR: Expected build output folder not found: $MOD_BUILD_DIR" >&2
  exit 1
fi

if [[ ! -w "$VS_MODS_DIR" ]]; then
  echo "Mods folder not writable, using sudo..."
  sudo mkdir -p "$VS_MODS_DIR"
  sudo rm -rf "$VS_MODS_DIR/$MOD_ID"
  sudo cp -R "$MOD_BUILD_DIR" "$VS_MODS_DIR/$MOD_ID"
else
  mkdir -p "$VS_MODS_DIR"
  rm -rf "$VS_MODS_DIR/$MOD_ID"
  cp -R "$MOD_BUILD_DIR" "$VS_MODS_DIR/$MOD_ID"
fi

echo "Installed '$MOD_ID' to:"
echo "  $VS_MODS_DIR/$MOD_ID"

if [[ ! -x "$VS_LAUNCHER" ]]; then
  echo
  echo "RC launcher not found at: $VS_LAUNCHER"
  echo "Use ~/bin/vs-1.22.0-rc.8 to start it with the local x64 .NET runtime once it exists."
  exit 0
fi

echo
echo "Launching Vintage Story 1.22.0-rc.8 via:"
echo "  $VS_LAUNCHER"
"$VS_LAUNCHER" >/dev/null 2>&1 &
