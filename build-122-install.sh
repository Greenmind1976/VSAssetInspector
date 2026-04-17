#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# Build + Install VSAssetInspector into Vintage Story 1.22.0-rc.8
###############################################################################

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CURRENT_BRANCH="$(git -C "$ROOT_DIR" branch --show-current 2>/dev/null || true)"
TARGET_BRANCH="support/1.22"
CURRENT_HEAD="$(git -C "$ROOT_DIR" rev-parse HEAD 2>/dev/null || true)"
TARGET_HEAD="$(git -C "$ROOT_DIR" rev-parse "$TARGET_BRANCH" 2>/dev/null || true)"

find_worktree_for_branch() {
  local branch_name="$1"

  git -C "$ROOT_DIR" worktree list --porcelain | awk -v target="refs/heads/$branch_name" '
    $1 == "worktree" { wt = $2 }
    $1 == "branch" && $2 == target { print wt; exit }
  '
}

if [[ "$CURRENT_BRANCH" != "$TARGET_BRANCH" ]]; then
  if [[ -n "$CURRENT_HEAD" && -n "$TARGET_HEAD" && "$CURRENT_HEAD" == "$TARGET_HEAD" ]]; then
    echo "Current checkout already matches $TARGET_BRANCH at $CURRENT_HEAD"
  else
  TARGET_WORKTREE="$(find_worktree_for_branch "$TARGET_BRANCH")"

  if [[ -z "$TARGET_WORKTREE" ]]; then
    echo "ERROR: Could not find worktree for $TARGET_BRANCH" >&2
    exit 1
  fi

  echo "Switching to $TARGET_BRANCH worktree:"
  echo "  $TARGET_WORKTREE"
  exec "$TARGET_WORKTREE/build-122-install.sh" "$@"
  fi
fi

cd "$ROOT_DIR"

MOD_ID="vsassetinspector"
PROJECT_DIR="VSAssetInspector"
PROJECT_FILE="$PROJECT_DIR/VSAssetInspector.csproj"
TARGET_FRAMEWORK="net10.0"
MOD_BUILD_DIR="$PROJECT_DIR/bin/Debug/$TARGET_FRAMEWORK/Mods/mod"
VS_APP_DIR="/Applications/Vintage Story 1.22.0-rc.8.app"
VS_MODS_DIR="$VS_APP_DIR/Mods"
VS_LAUNCHER="$HOME/bin/vs-1.22.0-rc.8"
LEGACY_MOD_DIRS=(
  "$VS_MODS_DIR/$MOD_ID"
  "$VS_MODS_DIR/$MOD_ID-1.21.0"
  "$VS_MODS_DIR/$MOD_ID-1.22.0"
)

rm -rf "$PROJECT_DIR/bin" "$PROJECT_DIR/obj"

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
  for mod_dir in "${LEGACY_MOD_DIRS[@]}"; do
    echo "Deleting installed mod dir: $mod_dir"
    sudo rm -rf "$mod_dir"
  done
  sudo cp -R "$MOD_BUILD_DIR" "$VS_MODS_DIR/$MOD_ID"
else
  mkdir -p "$VS_MODS_DIR"
  for mod_dir in "${LEGACY_MOD_DIRS[@]}"; do
    echo "Deleting installed mod dir: $mod_dir"
    rm -rf "$mod_dir"
  done
  cp -R "$MOD_BUILD_DIR" "$VS_MODS_DIR/$MOD_ID"
fi

for mod_dir in "${LEGACY_MOD_DIRS[@]}"; do
  if [[ "$mod_dir" != "$VS_MODS_DIR/$MOD_ID" && -e "$mod_dir" ]]; then
    echo "ERROR: Legacy mod dir still exists: $mod_dir" >&2
    exit 1
  fi
done

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
