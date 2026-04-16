# VSAssetInspector

`VSAssetInspector` is a utility mod for Vintage Story that exports the game's loaded runtime registries to JSON and validates recipe references against those live registries.

It is designed for mod authors who want to answer questions like:

- Which `item`, `block`, and `entity` codes are actually loaded right now?
- Which domains are providing those assets?
- Do my recipe references resolve at runtime?
- Is a recipe using an `item` code where only a `block` exists?

## What It Does

The mod currently provides two main workflows:

1. Dump loaded runtime ids to JSON.
2. Validate recipe references for a specific domain against the live game registries.

Exports are written to the Vintage Story data path under `VSAssetInspector`.

## Commands

Run these in-game after loading into a world with the mod enabled.

### Dump loaded ids

```text
/assetinspect dump ids
```

Optional scopes:

```text
/assetinspect dump ids all
/assetinspect dump ids items
/assetinspect dump ids blocks
/assetinspect dump ids entities
```

This writes:

- one combined JSON export
- one JSON export per asset domain

The dump is based on the live runtime registries, so it includes vanilla assets (`game:`) and loaded mod assets.

### Validate recipes for a specific domain

```text
/assetinspect validate recipes vsdemolitionist
```

This scans recipe assets for the requested domain and writes a validation report showing:

- resolved references
- unresolved references
- wildcard matches
- declared reference type
- resolution scope and match count

The validator is type-aware:

- `item` references are checked against runtime items
- `block` references are checked against runtime blocks
- `entity` references are checked against runtime entities

## Output

Files are written to the game's data folder under:

```text
VSAssetInspector
```

Typical filenames look like:

```text
assetinspect-ids-all-20260416-192740-123.json
assetinspect-ids-all-game-20260416-192740-123.json
assetinspect-validate-recipes-vsdemolitionist-20260416-200046-960.json
```

When a command completes, the mod sends a chat message showing whether the export succeeded and where the file was saved.

## Supported Branches

- `support/1.22` targets Vintage Story `1.22.0-rc.8`
- `support/1.21` targets Vintage Story `1.21.x`

## Development

Build and install for `1.21`:

```bash
./build-install.sh
```

Build and install for `1.22`:

```bash
./build-122-install.sh
```

Create a release zip from the current branch:

```bash
./release.sh
```
