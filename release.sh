#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$ROOT_DIR/VSAssetInspector/VSAssetInspector.csproj"
MOD_OUTPUT_DIR="$ROOT_DIR/VSAssetInspector/bin/Release/net10.0/Mods/mod"
VERSION_FILE="$ROOT_DIR/VERSION"
DIST_DIR="$ROOT_DIR/dist"
VS_APP_DIR="/Applications/Vintage Story 1.22.app"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is not installed or not on PATH." >&2
  exit 1
fi

if ! command -v zip >/dev/null 2>&1; then
  echo "zip is not installed or not on PATH." >&2
  exit 1
fi

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "VERSION file not found at $VERSION_FILE" >&2
  exit 1
fi

if [[ ! -d "$VS_APP_DIR" ]]; then
  echo "Vintage Story app not found at $VS_APP_DIR" >&2
  exit 1
fi

VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
if [[ -z "$VERSION" ]]; then
  echo "Could not determine mod version from VERSION file" >&2
  exit 1
fi

echo "Building VSAssetInspector $VERSION"
VINTAGE_STORY="$VS_APP_DIR" dotnet build "$PROJECT_PATH" -c Release -f net10.0 -p:NuGetAudit=false

if [[ ! -f "$MOD_OUTPUT_DIR/VSAssetInspector.dll" ]]; then
  echo "Expected built DLL not found in $MOD_OUTPUT_DIR" >&2
  exit 1
fi

if [[ ! -f "$MOD_OUTPUT_DIR/modinfo.json" ]]; then
  echo "Expected modinfo.json not found in $MOD_OUTPUT_DIR" >&2
  exit 1
fi

mkdir -p "$DIST_DIR"
ZIP_PATH="$DIST_DIR/vsassetinspector-$VERSION.zip"
rm -f "$ZIP_PATH"

(
  cd "$MOD_OUTPUT_DIR"
  zip -r "$ZIP_PATH" . -x '*.pdb'
)

echo "Created release package:"
echo "  $ZIP_PATH"
