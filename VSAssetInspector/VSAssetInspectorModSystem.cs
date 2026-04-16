using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VSAssetInspector;

public class VSAssetInspectorModSystem : ModSystem
{
    private const string CommandName = "assetinspect";
    private const string DataFolderName = "VSAssetInspector";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

#pragma warning disable CS0618
        api.RegisterCommand(
            CommandName,
            "Export loaded item, block, and entity ids to a JSON file",
            "/assetinspect dump ids [all|items|blocks|entities]",
            OnAssetInspectCommand,
            Privilege.chat
        );
#pragma warning restore CS0618
    }

    private void OnAssetInspectCommand(IServerPlayer byPlayer, int groupId, CmdArgs args)
    {
        string action = (args.PopWord() ?? string.Empty).ToLowerInvariant();
        string subject = (args.PopWord() ?? string.Empty).ToLowerInvariant();
        string scope = (args.PopWord() ?? "all").ToLowerInvariant();

        if (action != "dump" || subject != "ids")
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Usage: /assetinspect dump ids [all|items|blocks|entities]", EnumChatType.CommandError);
            return;
        }

        if (scope is not ("all" or "items" or "blocks" or "entities"))
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Scope must be one of: all, items, blocks, entities", EnumChatType.CommandError);
            return;
        }

        ICoreAPI api = byPlayer.Entity.Api;
        AssetDump dump = BuildDump(api, scope);
        string outputDirectory = api.GetOrCreateDataPath(DataFolderName);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string fileName = $"assetinspect-ids-{scope}-{timestamp}.json";
        string outputPath = Path.Combine(outputDirectory, fileName);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(dump, JsonOptions));

        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Asset gathering complete. Exported {dump.Summary.TotalRecords} records.",
            EnumChatType.Notification
        );

        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Saved to: {outputPath}",
            EnumChatType.Notification
        );
    }

    private static AssetDump BuildDump(ICoreAPI api, string scope)
    {
        List<CollectibleRecord> items = new();
        List<CollectibleRecord> blocks = new();
        List<EntityRecord> entities = new();

        foreach (CollectibleObject collectible in api.World.Collectibles.Where(collectible => collectible is not null))
        {
            CollectibleRecord record = new(
                collectible.Id,
                collectible.Code?.Domain ?? string.Empty,
                collectible.Code?.Path ?? string.Empty,
                collectible.Code?.ToString() ?? string.Empty,
                collectible.GetType().FullName ?? collectible.GetType().Name,
                collectible.IsMissing
            );

            if (collectible.ItemClass == EnumItemClass.Item)
            {
                items.Add(record);
            }
            else if (collectible.ItemClass == EnumItemClass.Block)
            {
                blocks.Add(record);
            }
        }

        foreach (var entityType in api.World.EntityTypes.Where(entityType => entityType is not null))
        {
            entities.Add(new EntityRecord(
                entityType.Id,
                entityType.Code?.Domain ?? string.Empty,
                entityType.Code?.Path ?? string.Empty,
                entityType.Code?.ToString() ?? string.Empty,
                entityType.Class ?? string.Empty
            ));
        }

        items = items.OrderBy(record => record.Code, StringComparer.Ordinal).ToList();
        blocks = blocks.OrderBy(record => record.Code, StringComparer.Ordinal).ToList();
        entities = entities.OrderBy(record => record.Code, StringComparer.Ordinal).ToList();

        bool includeItems = scope is "all" or "items";
        bool includeBlocks = scope is "all" or "blocks";
        bool includeEntities = scope is "all" or "entities";

        List<CollectibleRecord> dumpItems = includeItems ? items : new List<CollectibleRecord>();
        List<CollectibleRecord> dumpBlocks = includeBlocks ? blocks : new List<CollectibleRecord>();
        List<EntityRecord> dumpEntities = includeEntities ? entities : new List<EntityRecord>();

        return new AssetDump(
            DateTime.UtcNow,
            scope,
            new AssetDumpSummary(
                dumpItems.Count,
                dumpBlocks.Count,
                dumpEntities.Count,
                dumpItems.Count + dumpBlocks.Count + dumpEntities.Count
            ),
            dumpItems,
            dumpBlocks,
            dumpEntities
        );
    }

    private sealed record AssetDump(
        DateTime GeneratedAtUtc,
        string Scope,
        AssetDumpSummary Summary,
        List<CollectibleRecord> Items,
        List<CollectibleRecord> Blocks,
        List<EntityRecord> Entities
    );

    private sealed record AssetDumpSummary(
        int ItemCount,
        int BlockCount,
        int EntityCount,
        int TotalRecords
    );

    private sealed record CollectibleRecord(
        int Id,
        string Domain,
        string Path,
        string Code,
        string ClassName,
        bool IsMissing
    );

    private sealed record EntityRecord(
        int Id,
        string Domain,
        string Path,
        string Code,
        string ClassName
    );
}
