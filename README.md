# VSAssetInspector

Utility Vintage Story mod for exporting resolved runtime asset IDs to disk.

## First Command

Run in-game on a world/server with the mod enabled:

```text
/assetinspect dump ids
```

This writes a JSON file containing loaded items, blocks, and entity codes to the game's data path.

## Development

Build against your local game install:

```bash
VINTAGE_STORY="/Applications/Vintage Story 1.22.0-rc.8.app" dotnet build ./VSAssetInspector/VSAssetInspector.csproj
```
