# VSAssetInspector

`VSAssetInspector` is a utility mod for Vintage Story that exports the game's loaded runtime registries to JSON and validates recipe references against those live registries.

It is designed for mod authors who want to answer questions like:

- Which `item`, `block`, and `entity` codes are actually loaded right now?
- Which domains are providing those assets?
- Do my recipe references resolve at runtime?
- Is a recipe using an `item` code where only a `block` exists?

## What It Does

The mod currently provides three main workflows:

1. Dump loaded runtime ids to JSON.
2. List the loaded non-vanilla mod domains.
3. Validate recipe references for a specific domain against the live game registries.

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

### List loaded mod domains

```text
/assetinspect list moddomains
```

This writes a JSON file containing the currently loaded non-`game` domains and also prints the domains in chat for quick inspection.

### Validate recipes for a specific domain

```text
/assetinspect validate recipes vsdemolitionist
```

This command scans recipe assets for the requested domain and validates the references they use against the live runtime registries. It is intended to help find broken recipe references, wildcard mistakes, variant-template mismatches, item-vs-block mismatches, and cross-version asset issues.

The validation report shows:

- resolved references
- unresolved references
- wildcard matches
- template matches such as `{rock}` or `{metal}`
- declared reference type
- resolution scope and match count

The validator is wildcard-aware, template-aware, and type-aware:

- `item` references are checked against runtime items
- `block` references are checked against runtime blocks
- `entity` references are checked against runtime entities
- `*` wildcard patterns are matched against the live registry
- `{variant}` placeholder patterns are matched against the live registry

If a recipe asset cannot be parsed cleanly, the validator continues processing the rest of the domain and records the asset-level failure in the report instead of aborting the whole run.

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

When a command completes, the mod sends chat messages showing whether the export or validation succeeded and where the file was saved.

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
