#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# Build + Install VSAssetInspector into /Applications/Vintage Story.app/Mods
###############################################################################

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CURRENT_BRANCH="$(git -C "$ROOT_DIR" branch --show-current 2>/dev/null || true)"
TARGET_BRANCH="support/1.21"

find_worktree_for_branch() {
  local branch_name="$1"

  git -C "$ROOT_DIR" worktree list --porcelain | awk -v target="refs/heads/$branch_name" '
    $1 == "worktree" { wt = $2 }
    $1 == "branch" && $2 == target { print wt; exit }
  '
}

if [[ "$CURRENT_BRANCH" != "$TARGET_BRANCH" ]]; then
  TARGET_WORKTREE="$(find_worktree_for_branch "$TARGET_BRANCH")"

  if [[ -z "$TARGET_WORKTREE" ]]; then
    echo "ERROR: Could not find worktree for $TARGET_BRANCH" >&2
    exit 1
  fi

  echo "Switching to $TARGET_BRANCH worktree:"
  echo "  $TARGET_WORKTREE"
  exec "$TARGET_WORKTREE/build-install.sh" "$@"
fi

cd "$ROOT_DIR"

MOD_ID="vsassetinspector"
PROJECT_DIR="VSAssetInspector"
PROJECT_FILE="$PROJECT_DIR/VSAssetInspector.csproj"
TARGET_FRAMEWORK="net8.0"
VS_APP_DIR="/Applications/Vintage Story.app"
VS_MODS_DIR="$VS_APP_DIR/Mods"
MOD_BUILD_DIR="$PROJECT_DIR/bin/Debug/$TARGET_FRAMEWORK/Mods/mod"

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

if [[ -n "$VS_APP_DIR" ]]; then
  open "$VS_APP_DIR"
fi
