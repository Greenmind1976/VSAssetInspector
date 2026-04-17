using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    private static readonly JsonDocumentOptions JsonDocOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.ChatCommands
            .GetOrCreate(CommandName)
            .WithDescription("Dump loaded ids, list mod domains, or validate recipe references by domain")
            .WithAdditionalInformation("Type /assetinspect help for a command overview. Dump exports loaded ids, list shows loaded mod domains, and validate checks recipe references for one domain or all domains with *.")
            .WithRootAlias("/" + CommandName)
            .WithExamples(new[]
            {
                "/assetinspect help",
                "/assetinspect dumpids",
                "/assetinspect dumpids all",
                "/assetinspect dumpids items",
                "/assetinspect dumpids blocks",
                "/assetinspect dumpids entities",
                "/assetinspect listmoddomains",
                "/assetinspect validaterecipes game",
                "/assetinspect validaterecipes *"
            })
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("help")
                .WithDescription("Show AssetInspector command help")
                .WithExamples(new[]
                {
                    "/assetinspect help"
                })
                .HandleWith(HandleHelpChatCommand)
            .EndSubCommand()
            .BeginSubCommand("dumpids")
                .WithDescription("Dump loaded ids")
                .WithAdditionalInformation("Exports loaded ids to JSON. /assetinspect dumpids defaults to all. You can also choose items, blocks, or entities.")
                .WithExamples(new[]
                {
                    "/assetinspect dumpids",
                    "/assetinspect dumpids all",
                    "/assetinspect dumpids items",
                    "/assetinspect dumpids blocks",
                    "/assetinspect dumpids entities"
                })
                .WithArgs(api.ChatCommands.Parsers.OptionalWordRange("scope", new[] { "all", "items", "blocks", "entities" }))
                .HandleWith(HandleDumpIdsChatCommand)
            .EndSubCommand()
            .BeginSubCommand("validaterecipes")
                .WithDescription("Validate recipe references by domain")
                .WithAdditionalInformation("Validates recipe asset references for one loaded domain, or for all loaded domains with *. Run /assetinspect listmoddomains to see available domains.")
                .WithExamples(new[]
                {
                    "/assetinspect validaterecipes game",
                    "/assetinspect validaterecipes vsassetinspector",
                    "/assetinspect validaterecipes *"
                })
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("domain"))
                .HandleWith(HandleValidateRecipesChatCommand)
            .EndSubCommand()
            .BeginSubCommand("listmoddomains")
                .WithDescription("List loaded mod domains")
                .WithAdditionalInformation("Lists loaded non-game mod domains and saves them to JSON.")
                .WithExamples(new[]
                {
                    "/assetinspect listmoddomains"
                })
                .HandleWith(HandleListModDomainsChatCommand)
            .EndSubCommand();
    }

    private TextCommandResult HandleHelpChatCommand(TextCommandCallingArgs args)
    {
        return TextCommandResult.Success(
            "[AssetInspector] Commands:\n" +
            "/assetinspect help - Show this help.\n" +
            "/assetinspect dumpids - Export loaded ids to JSON. Defaults to all.\n" +
            "/assetinspect dumpids all - Export all loaded ids to JSON.\n" +
            "/assetinspect dumpids items, blocks, or entities - Export only that category to JSON.\n" +
            "/assetinspect listmoddomains - List loaded mod domains and save them to JSON.\n" +
            "/assetinspect validaterecipes <domain> - Validate recipe references for one loaded domain.\n" +
            "/assetinspect validaterecipes * - Validate recipe references for all loaded domains plus game.\n" +
            "If you need a domain name, run /assetinspect listmoddomains first.\n" +
            "Tip: if a bare subcommand chip only inserts plain text, use the full slash-prefixed examples shown in the command help.",
            null
        );
    }

    private TextCommandResult HandleDumpIdsChatCommand(TextCommandCallingArgs args)
    {
        string scope = args.ArgCount > 0 && args[0] is string argScope && !string.IsNullOrWhiteSpace(argScope)
            ? argScope.ToLowerInvariant()
            : "all";

        IServerPlayer? byPlayer = args.Caller.Player as IServerPlayer;
        if (byPlayer == null)
        {
            return TextCommandResult.Error("This command requires a player context.");
        }

        HandleDumpCommand(byPlayer, GlobalConstants.CurrentChatGroup, new CmdArgs($"ids {scope}"));
        return TextCommandResult.Success(string.Empty, null);
    }

    private TextCommandResult HandleValidateRecipesChatCommand(TextCommandCallingArgs args)
    {
        string domain = args.ArgCount > 0 && args[0] is string argDomain
            ? argDomain
            : string.Empty;

        if (string.IsNullOrWhiteSpace(domain))
        {
            return TextCommandResult.Error("[AssetInspector] You must specify a domain, or use * for all loaded domains. Run /assetinspect listmoddomains to see available domains.");
        }

        IServerPlayer? byPlayer = args.Caller.Player as IServerPlayer;
        if (byPlayer == null)
        {
            return TextCommandResult.Error("This command requires a player context.");
        }

        HandleValidateCommand(byPlayer, GlobalConstants.CurrentChatGroup, new CmdArgs($"recipes {domain}"));
        return TextCommandResult.Success(string.Empty, null);
    }

    private TextCommandResult HandleListModDomainsChatCommand(TextCommandCallingArgs args)
    {
        IServerPlayer? byPlayer = args.Caller.Player as IServerPlayer;
        if (byPlayer == null)
        {
            return TextCommandResult.Error("This command requires a player context.");
        }

        HandleListCommand(byPlayer, GlobalConstants.CurrentChatGroup, new CmdArgs("moddomains"));
        return TextCommandResult.Success(string.Empty, null);
    }

    private void HandleDumpCommand(IServerPlayer byPlayer, int groupId, CmdArgs args)
    {
        string subject = (args.PopWord() ?? string.Empty).ToLowerInvariant();
        string scope = (args.PopWord() ?? "all").ToLowerInvariant();

        if (subject != "ids")
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Usage: /assetinspect dumpids [scope]. Scope defaults to all. Options: all, items, blocks, entities.", EnumChatType.CommandError);
            return;
        }

        if (scope is not ("all" or "items" or "blocks" or "entities"))
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Scope must be one of: all, items, blocks, entities", EnumChatType.CommandError);
            return;
        }

        ICoreAPI api = byPlayer.Entity.Api;
        byPlayer.SendMessage(groupId, $"[AssetInspector] Gathering {scope} ids. This may take a moment...", EnumChatType.Notification);
        AssetDump dump = BuildDump(api, scope);
        string outputDirectory = api.GetOrCreateDataPath(DataFolderName);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string fileName = $"assetinspect-ids-{scope}-{timestamp}.json";
        string outputPath = Path.Combine(outputDirectory, fileName);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(dump, JsonOptions));
        int domainFileCount = WriteDomainDumps(outputDirectory, scope, timestamp, dump);

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

        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Wrote {domainFileCount} domain-specific files to: {outputDirectory}",
            EnumChatType.Notification
        );
    }

    private void HandleValidateCommand(IServerPlayer byPlayer, int groupId, CmdArgs args)
    {
        string subject = (args.PopWord() ?? string.Empty).ToLowerInvariant();
        string domain = (args.PopWord() ?? string.Empty).ToLowerInvariant();

        if (subject != "recipes" || string.IsNullOrWhiteSpace(domain))
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Usage: /assetinspect validaterecipes <domain|*>", EnumChatType.CommandError);
            return;
        }

        ICoreAPI api = byPlayer.Entity.Api;
        bool validateAllDomains = domain == "*";
        string targetDescription = validateAllDomains ? "all loaded domains plus game" : $"domain {domain}";
        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Starting recipe validation for {targetDescription}. Large domains may take a while.",
            EnumChatType.Notification
        );
        ValidationRegistry registry = BuildValidationRegistry(api);
        RecipeValidationReport report = validateAllDomains
            ? ValidateRecipeAssetsForAllDomains(api, registry)
            : ValidateRecipeAssets(api, registry, domain);
        string outputDirectory = api.GetOrCreateDataPath(DataFolderName);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string outputPath = Path.Combine(
            outputDirectory,
            $"assetinspect-validate-recipes-{(validateAllDomains ? "all" : domain)}-{timestamp}.json"
        );

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));

        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Validated {report.AssetCount} recipe assets for {targetDescription}. Unresolved references: {report.UnresolvedCount}. Asset errors: {report.AssetErrorCount}.",
            EnumChatType.Notification
        );

        if (validateAllDomains && report.DomainSummaries.Count > 0)
        {
            byPlayer.SendMessage(
                groupId,
                "[AssetInspector] Per-domain summary:",
                EnumChatType.Notification
            );

            foreach (RecipeValidationDomainSummary summary in report.DomainSummaries.OrderBy(summary => summary.Domain, StringComparer.Ordinal))
            {
                byPlayer.SendMessage(
                    groupId,
                    $"[AssetInspector] {summary.Domain}: {summary.AssetCount} assets, {summary.TotalReferences} references, {summary.UnresolvedCount} unresolved, {summary.AssetErrorCount} errors.",
                    EnumChatType.Notification
                );
            }
        }

        byPlayer.SendMessage(
            groupId,
            $"[AssetInspector] Validation report saved to: {outputPath}",
            EnumChatType.Notification
        );
    }

    private void HandleListCommand(IServerPlayer byPlayer, int groupId, CmdArgs args)
    {
        string subject = (args.PopWord() ?? "moddomains").ToLowerInvariant();

        if (subject != "moddomains")
        {
            byPlayer.SendMessage(groupId, "[AssetInspector] Usage: /assetinspect listmoddomains", EnumChatType.CommandError);
            return;
        }

        ICoreAPI api = byPlayer.Entity.Api;
        byPlayer.SendMessage(groupId, "[AssetInspector] Gathering loaded mod domains...", EnumChatType.Notification);
        List<string> domains = BuildModDomainList(api);
        string outputDirectory = api.GetOrCreateDataPath(DataFolderName);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string outputPath = Path.Combine(outputDirectory, $"assetinspect-moddomains-{timestamp}.json");
        ModDomainListReport report = new(DateTime.UtcNow, domains.Count, domains);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));

        byPlayer.SendMessage(groupId, $"[AssetInspector] Found {domains.Count} loaded mod domains.", EnumChatType.Notification);

        if (domains.Count > 0)
        {
            foreach (string chunk in ChunkStrings(domains, 12))
            {
                byPlayer.SendMessage(groupId, $"[AssetInspector] {chunk}", EnumChatType.Notification);
            }
        }

        byPlayer.SendMessage(groupId, $"[AssetInspector] Mod domain list saved to: {outputPath}", EnumChatType.Notification);
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

    private static int WriteDomainDumps(string outputDirectory, string scope, string timestamp, AssetDump dump)
    {
        Dictionary<string, DomainAssetDumpBuilder> builders = new(StringComparer.Ordinal);

        foreach (CollectibleRecord item in dump.Items)
        {
            DomainAssetDumpBuilder builder = GetOrCreateBuilder(builders, item.Domain);
            builder.Items.Add(item);
        }

        foreach (CollectibleRecord block in dump.Blocks)
        {
            DomainAssetDumpBuilder builder = GetOrCreateBuilder(builders, block.Domain);
            builder.Blocks.Add(block);
        }

        foreach (EntityRecord entity in dump.Entities)
        {
            DomainAssetDumpBuilder builder = GetOrCreateBuilder(builders, entity.Domain);
            builder.Entities.Add(entity);
        }

        foreach ((string domain, DomainAssetDumpBuilder builder) in builders.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            AssetDump domainDump = new(
                dump.GeneratedAtUtc,
                scope,
                new AssetDumpSummary(
                    builder.Items.Count,
                    builder.Blocks.Count,
                    builder.Entities.Count,
                    builder.Items.Count + builder.Blocks.Count + builder.Entities.Count
                ),
                builder.Items.OrderBy(record => record.Code, StringComparer.Ordinal).ToList(),
                builder.Blocks.OrderBy(record => record.Code, StringComparer.Ordinal).ToList(),
                builder.Entities.OrderBy(record => record.Code, StringComparer.Ordinal).ToList()
            );

            string safeDomain = string.IsNullOrWhiteSpace(domain) ? "unknown" : domain;
            string domainPath = Path.Combine(outputDirectory, $"assetinspect-ids-{scope}-{safeDomain}-{timestamp}.json");
            File.WriteAllText(domainPath, JsonSerializer.Serialize(domainDump, JsonOptions));
        }

        return builders.Count;
    }

    private static ValidationRegistry BuildValidationRegistry(ICoreAPI api)
    {
        HashSet<string> itemCodes = new(StringComparer.Ordinal);
        HashSet<string> blockCodes = new(StringComparer.Ordinal);
        HashSet<string> entityCodes = new(StringComparer.Ordinal);

        foreach (CollectibleObject collectible in api.World.Collectibles.Where(collectible => collectible is not null))
        {
            string? code = collectible.Code?.ToString();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (collectible.ItemClass == EnumItemClass.Item)
            {
                itemCodes.Add(code);
            }
            else if (collectible.ItemClass == EnumItemClass.Block)
            {
                blockCodes.Add(code);
            }
        }

        foreach (var entityType in api.World.EntityTypes.Where(entityType => entityType is not null))
        {
            string? code = entityType.Code?.ToString();
            if (!string.IsNullOrWhiteSpace(code))
            {
                entityCodes.Add(code);
            }
        }

        return new ValidationRegistry(itemCodes, blockCodes, entityCodes);
    }

    private static List<string> BuildModDomainList(ICoreAPI api)
    {
        HashSet<string> domains = new(StringComparer.Ordinal);

        foreach (CollectibleObject collectible in api.World.Collectibles.Where(collectible => collectible is not null))
        {
            string? domain = collectible.Code?.Domain;
            if (!string.IsNullOrWhiteSpace(domain) && !domain.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                domains.Add(domain);
            }
        }

        foreach (var entityType in api.World.EntityTypes.Where(entityType => entityType is not null))
        {
            string? domain = entityType.Code?.Domain;
            if (!string.IsNullOrWhiteSpace(domain) && !domain.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                domains.Add(domain);
            }
        }

        return domains.OrderBy(domain => domain, StringComparer.Ordinal).ToList();
    }

    private static RecipeValidationReport ValidateRecipeAssets(ICoreAPI api, ValidationRegistry registry, string domain)
    {
        List<RecipeAssetValidation> assets = new();
        List<RecipeAssetError> assetErrors = new();
        int totalReferences = 0;
        int unresolvedCount = 0;

        foreach (IAsset asset in api.Assets.GetManyInCategory("recipes", string.Empty, domain, loadAsset: true))
        {
            byte[]? data = asset.Data;
            if (data == null || data.Length == 0)
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(data, JsonDocOptions);
                Dictionary<string, RecipeReferenceCandidate> refs = new(StringComparer.Ordinal);
                CollectRecipeReferences(document.RootElement, refs);

                List<RecipeReferenceValidation> validations = refs.Values
                    .OrderBy(value => value.Code, StringComparer.Ordinal)
                    .ThenBy(value => value.DeclaredType, StringComparer.Ordinal)
                    .Select(value => ValidateReference(value, registry))
                    .ToList();

                totalReferences += validations.Count;
                unresolvedCount += validations.Count(validation => !validation.IsResolved);

                assets.Add(new RecipeAssetValidation(
                    asset.Location.ToString(),
                    validations.Count,
                    validations.Count(validation => !validation.IsResolved),
                    validations
                ));
            }
            catch (Exception ex)
            {
                assetErrors.Add(new RecipeAssetError(
                    asset.Location.ToString(),
                    ex.GetType().Name,
                    ex.Message
                ));
            }
        }

        return new RecipeValidationReport(
            DateTime.UtcNow,
            domain,
            assets.Count,
            totalReferences,
            unresolvedCount,
            assetErrors.Count,
            new List<RecipeValidationDomainSummary>
            {
                new(
                    domain,
                    assets.Count,
                    totalReferences,
                    unresolvedCount,
                    assetErrors.Count
                )
            },
            assets.OrderBy(asset => asset.AssetLocation, StringComparer.Ordinal).ToList(),
            assetErrors.OrderBy(error => error.AssetLocation, StringComparer.Ordinal).ToList()
        );
    }

    private static RecipeValidationReport ValidateRecipeAssetsForAllDomains(ICoreAPI api, ValidationRegistry registry)
    {
        List<RecipeAssetValidation> assets = new();
        List<RecipeAssetError> assetErrors = new();
        List<RecipeValidationDomainSummary> domainSummaries = new();
        List<string> domains = new() { "game" };
        domains.AddRange(BuildModDomainList(api));

        foreach (string domain in domains.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            RecipeValidationReport report = ValidateRecipeAssets(api, registry, domain);
            assets.AddRange(report.Assets);
            assetErrors.AddRange(report.AssetErrors);
            domainSummaries.AddRange(report.DomainSummaries);
        }

        return new RecipeValidationReport(
            DateTime.UtcNow,
            "*",
            assets.Count,
            assets.Sum(asset => asset.ReferenceCount),
            assets.Sum(asset => asset.UnresolvedCount),
            assetErrors.Count,
            domainSummaries.OrderBy(summary => summary.Domain, StringComparer.Ordinal).ToList(),
            assets.OrderBy(asset => asset.AssetLocation, StringComparer.Ordinal).ToList(),
            assetErrors.OrderBy(error => error.AssetLocation, StringComparer.Ordinal).ToList()
        );
    }

    private static void CollectRecipeReferences(JsonElement element, Dictionary<string, RecipeReferenceCandidate> refs)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? declaredType = null;
                string? code = null;
                string? name = null;

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("type") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        declaredType = property.Value.GetString();
                    }
                    else if (property.NameEquals("code") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        code = property.Value.GetString();
                    }
                    else if (property.NameEquals("name") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        name = property.Value.GetString();
                    }
                }

                AddReferenceCandidate(refs, code, declaredType);
                AddReferenceCandidate(refs, name, declaredType);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectRecipeReferences(property.Value, refs);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                {
                    CollectRecipeReferences(child, refs);
                }
                break;
        }
    }

    private static void AddReferenceCandidate(Dictionary<string, RecipeReferenceCandidate> refs, string? value, string? declaredType)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains(':'))
        {
            return;
        }

        string normalizedType = NormalizeDeclaredType(declaredType);
        string key = $"{normalizedType}|{value}";

        if (!refs.ContainsKey(key))
        {
            refs[key] = new RecipeReferenceCandidate(value, normalizedType);
        }
    }

    private static RecipeReferenceValidation ValidateReference(RecipeReferenceCandidate candidate, ValidationRegistry registry)
    {
        HashSet<string> pool = GetPoolForCandidate(candidate, registry);
        string resolutionScope = candidate.DeclaredType;
        bool containsWildcard = candidate.Code.Contains('*');
        bool containsTemplate = ContainsTemplateToken(candidate.Code);

        if (containsWildcard || containsTemplate)
        {
            int matchCount = CountPatternMatches(candidate.Code, pool);
            string resolutionKind = matchCount > 0
                ? GetPatternResolutionKind(containsWildcard, containsTemplate)
                : GetMissingPatternResolutionKind(containsWildcard, containsTemplate);

            return new RecipeReferenceValidation(
                candidate.Code,
                candidate.DeclaredType,
                matchCount > 0,
                containsWildcard,
                containsTemplate,
                matchCount,
                resolutionKind,
                resolutionScope
            );
        }

        bool isResolved = pool.Contains(candidate.Code);
        return new RecipeReferenceValidation(
            candidate.Code,
            candidate.DeclaredType,
            isResolved,
            false,
            false,
            isResolved ? 1 : 0,
            isResolved ? "exact-match" : "missing",
            resolutionScope
        );
    }

    private static HashSet<string> GetPoolForCandidate(RecipeReferenceCandidate candidate, ValidationRegistry registry)
    {
        return candidate.DeclaredType switch
        {
            "item" => registry.ItemCodes,
            "block" => registry.BlockCodes,
            "entity" => registry.EntityCodes,
            _ => registry.AllCodes
        };
    }

    private static string NormalizeDeclaredType(string? declaredType)
    {
        return declaredType?.Trim().ToLowerInvariant() switch
        {
            "item" => "item",
            "block" => "block",
            "entity" => "entity",
            _ => "unknown"
        };
    }

    private static int CountPatternMatches(string pattern, IEnumerable<string> pool)
    {
        string regexPattern = BuildPatternRegex(pattern);
        System.Text.RegularExpressions.Regex regex = new(regexPattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        int count = 0;

        foreach (string code in pool)
        {
            if (regex.IsMatch(code))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ContainsTemplateToken(string code)
    {
        int openBrace = code.IndexOf('{');
        if (openBrace < 0)
        {
            return false;
        }

        int closeBrace = code.IndexOf('}', openBrace + 1);
        return closeBrace > openBrace + 1;
    }

    private static string GetPatternResolutionKind(bool containsWildcard, bool containsTemplate)
    {
        if (containsWildcard && containsTemplate)
        {
            return "wildcard-template-match";
        }

        if (containsTemplate)
        {
            return "template-match";
        }

        return "wildcard-match";
    }

    private static string GetMissingPatternResolutionKind(bool containsWildcard, bool containsTemplate)
    {
        if (containsWildcard && containsTemplate)
        {
            return "wildcard-template-no-match";
        }

        if (containsTemplate)
        {
            return "template-no-match";
        }

        return "wildcard-no-match";
    }

    private static string BuildPatternRegex(string pattern)
    {
        StringBuilder regexBuilder = new("^");

        for (int i = 0; i < pattern.Length; i++)
        {
            char current = pattern[i];

            if (current == '*')
            {
                regexBuilder.Append("[^:]*");
                continue;
            }

            if (current == '{')
            {
                int closingBrace = pattern.IndexOf('}', i + 1);
                if (closingBrace > i + 1)
                {
                    regexBuilder.Append("[^:]+");
                    i = closingBrace;
                    continue;
                }
            }

            regexBuilder.Append(System.Text.RegularExpressions.Regex.Escape(current.ToString()));
        }

        regexBuilder.Append('$');
        return regexBuilder.ToString();
    }

    private static IEnumerable<string> ChunkStrings(List<string> values, int chunkSize)
    {
        for (int i = 0; i < values.Count; i += chunkSize)
        {
            yield return string.Join(", ", values.Skip(i).Take(chunkSize));
        }
    }

    private static DomainAssetDumpBuilder GetOrCreateBuilder(Dictionary<string, DomainAssetDumpBuilder> builders, string domain)
    {
        string key = string.IsNullOrWhiteSpace(domain) ? "unknown" : domain;

        if (!builders.TryGetValue(key, out DomainAssetDumpBuilder? builder))
        {
            builder = new DomainAssetDumpBuilder();
            builders[key] = builder;
        }

        return builder;
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

    private sealed record ValidationRegistry(
        HashSet<string> ItemCodes,
        HashSet<string> BlockCodes,
        HashSet<string> EntityCodes
    )
    {
        public HashSet<string> AllCodes => new(ItemCodes.Concat(BlockCodes).Concat(EntityCodes), StringComparer.Ordinal);
    }

    private sealed record RecipeValidationReport(
        DateTime GeneratedAtUtc,
        string Domain,
        int AssetCount,
        int TotalReferences,
        int UnresolvedCount,
        int AssetErrorCount,
        List<RecipeValidationDomainSummary> DomainSummaries,
        List<RecipeAssetValidation> Assets,
        List<RecipeAssetError> AssetErrors
    );

    private sealed record RecipeValidationDomainSummary(
        string Domain,
        int AssetCount,
        int TotalReferences,
        int UnresolvedCount,
        int AssetErrorCount
    );

    private sealed record RecipeAssetValidation(
        string AssetLocation,
        int ReferenceCount,
        int UnresolvedCount,
        List<RecipeReferenceValidation> References
    );

    private sealed record RecipeReferenceValidation(
        string Code,
        string DeclaredType,
        bool IsResolved,
        bool IsWildcard,
        bool IsTemplate,
        int MatchCount,
        string ResolutionKind,
        string ResolutionScope
    );

    private sealed record RecipeReferenceCandidate(
        string Code,
        string DeclaredType
    );

    private sealed record RecipeAssetError(
        string AssetLocation,
        string ErrorType,
        string Message
    );

    private sealed record ModDomainListReport(
        DateTime GeneratedAtUtc,
        int DomainCount,
        List<string> Domains
    );

    private sealed class DomainAssetDumpBuilder
    {
        public List<CollectibleRecord> Items { get; } = new();
        public List<CollectibleRecord> Blocks { get; } = new();
        public List<EntityRecord> Entities { get; } = new();
    }
}
