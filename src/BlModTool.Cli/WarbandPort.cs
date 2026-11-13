using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace BlModTool.Cli;

public sealed class WarbandPortOptions
{
    public bool IncludeHeroes { get; set; }
    public bool EmitKingdoms { get; set; }
    public bool KeepMeshes { get; set; }
    public bool GenerateReadme { get; set; }
    public bool GenerateSubModule { get; set; }
    public bool Install { get; set; }
    public string? BannerlordDir { get; set; }
    public string? ModuleName { get; set; }
    public bool GeneratePartyTemplates { get; set; }
    public bool GenerateSettlements { get; set; }
    public bool GenerateQuests { get; set; }
    public bool GenerateMenus { get; set; }
    public bool GenerateDialogStates { get; set; }
    public bool GenerateScripts { get; set; }
}

public sealed class WarbandPortResult
{
    public string OutputRoot { get; set; } = "";
    public string ItemsFile { get; set; } = "";
    public string NpcFile { get; set; } = "";
    public string CulturesFile { get; set; } = "";
    public string? KingdomsFile { get; set; }
    public string? SkillsFile { get; set; }
    public string? PartyTemplatesFile { get; set; }
    public string? SettlementsFile { get; set; }
    public string? QuestsFile { get; set; }
    public string? MenusFile { get; set; }
    public string? DialogStatesFile { get; set; }
    public string? ScriptsFile { get; set; }
    public string ReportFile { get; set; } = "";
    public string? ModuleInfoFile { get; set; }
    public string? LanguagesDir { get; set; }
    public string? InstalledModuleDir { get; set; }
    public ConversionReport Report { get; set; } = new();
}

public sealed class ConversionReport
{
    public int SchemaVersion { get; set; } = 1;
    public string Tool { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public SourcePaths Source { get; set; } = new();
    public ConversionCounts Counts { get; set; } = new();
    public List<ConversionSkip> SkippedItems { get; set; } = new();
    public List<ConversionSkip> SkippedTroops { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingReferences { get; set; } = new();
    public List<string> UnmappedFields { get; set; } = new();
}

public sealed class SourcePaths
{
    public string Items { get; set; } = "";
    public string Troops { get; set; } = "";
    public string Factions { get; set; } = "";
    public string? Skills { get; set; }
    public string? Quests { get; set; }
    public string? Menus { get; set; }
    public string? DialogStates { get; set; }
    public string? Scripts { get; set; }
}

public sealed class ConversionCounts
{
    public int ItemsRead { get; set; }
    public int ItemsConverted { get; set; }
    public int ItemsSkipped { get; set; }
    public int TroopsRead { get; set; }
    public int TroopsConverted { get; set; }
    public int TroopsSkipped { get; set; }
    public int CulturesGenerated { get; set; }
    public int KingdomsGenerated { get; set; }
    public int SkillsRead { get; set; }
    public int SkillsConverted { get; set; }
    public int SkillsSkipped { get; set; }
    public int PartyTemplatesRead { get; set; }
    public int PartyTemplatesConverted { get; set; }
    public int PartyTemplatesSkipped { get; set; }
    public int SettlementsRead { get; set; }
    public int SettlementsConverted { get; set; }
    public int SettlementsSkipped { get; set; }
    public int QuestsRead { get; set; }
    public int QuestsConverted { get; set; }
    public int QuestsSkipped { get; set; }
    public int MenusRead { get; set; }
    public int MenusConverted { get; set; }
    public int MenusSkipped { get; set; }
    public int DialogStatesRead { get; set; }
    public int DialogStatesConverted { get; set; }
    public int DialogStatesSkipped { get; set; }
    public int ScriptsRead { get; set; }
    public int ScriptsConverted { get; set; }
    public int ScriptsSkipped { get; set; }
    public int NpcRolesGenerated { get; set; }
    public int EquipmentEntries { get; set; }
    public int EquipmentSkipped { get; set; }
    public int UpgradeLinks { get; set; }
    public int UpgradeLinksSkipped { get; set; }
    public int MissingReferences { get; set; }
    public int Warnings { get; set; }
    public Dictionary<string, int> ItemsPerCategory { get; set; } = new();
}

public sealed class ConversionSkip
{
    public string Id { get; set; } = "";
    public string Reason { get; set; } = "";
}

public static class WarbandPort
{
    private const string ToolVersion = "0.4.0-m3";
    private const int MaxDetailedMissingReferences = 500;

    private static readonly Dictionary<string, string> FallbackMeshes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneHandedSword"] = "horse_whip",
        ["OneHandedAxe"] = "torch_g",
        ["Mace"] = "flail_a",
        ["Dagger"] = "horse_whip",
        ["TwoHandedSword"] = "horse_whip",
        ["TwoHandedAxe"] = "torch_g",
        ["TwoHandedMace"] = "flail_a",
        ["OneHandedPolearm"] = "axe_craft_32_handle",
        ["TwoHandedPolearm"] = "axe_craft_32_handle",
        ["Javelin"] = "throwing_stone",
        ["ThrowingKnife"] = "throwing_stone",
        ["ThrowingAxe"] = "throwing_stone",
        ["Stone"] = "throwing_stone",
        ["Bow"] = "shortbow_b",
        ["Crossbow"] = "crossbow_a",
        ["Arrow"] = "arrow_bl_a",
        ["Bolt"] = "bolt_bl_a",
        ["LargeShield"] = "sturgia_shield_a",
        ["Horse"] = "horse_brown",
        ["Animal"] = "sheep_mesh_new",
        ["Goods"] = "merchandise_wool",
        ["HeadArmor"] = "headscarf_d",
        ["BodyArmor"] = "aserai_tunic_long",
        ["LegArmor"] = "aserai_female_costume_shoe",
        ["HandArmor"] = "padded_vambrace_a_handguard",
        ["Thrown"] = "throwing_stone",
        ["Polearm"] = "axe_craft_32_handle"
    };

    private static readonly (string Prefix, string FactionId)[] TroopCulturePrefixes =
    {
        ("black_khergit", "fac_black_khergits"),
        ("robber_knight", "fac_robber_knights"),
        ("forest_bandit", "fac_forest_bandits"),
        ("mountain_bandit", "fac_mountain_bandits"),
        ("peasant_rebel", "fac_peasant_rebels"),
        ("noble_refugee", "fac_noble_refugees"),
        ("dark_knight", "fac_dark_knights"),
        ("manhunter", "fac_manhunters"),
        ("deserter", "fac_deserters"),
        ("slaver", "fac_slavers"),
        ("kingdom_1", "fac_kingdom_1"),
        ("kingdom_2", "fac_kingdom_2"),
        ("kingdom_3", "fac_kingdom_3"),
        ("kingdom_4", "fac_kingdom_4"),
        ("kingdom_5", "fac_kingdom_5"),
        ("swadian", "fac_kingdom_1"),
        ("vaegir", "fac_kingdom_2"),
        ("khergit", "fac_kingdom_3"),
        ("nord", "fac_kingdom_4"),
        ("rhodok", "fac_kingdom_5")
    };

    private static readonly HashSet<string> ExcludedCultureFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "fac_no_faction",
        "fac_commoners",
        "fac_neutral",
        "fac_innocents",
        "fac_merchants",
        "fac_culture_1",
        "fac_culture_2",
        "fac_culture_3",
        "fac_culture_4",
        "fac_culture_5",
        "fac_player_faction",
        "fac_player_supporters_faction",
        "fac_kingdoms_end"
    };

    private static readonly HashSet<string> MainKingdomFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "fac_kingdom_1",
        "fac_kingdom_2",
        "fac_kingdom_3",
        "fac_kingdom_4",
        "fac_kingdom_5"
    };

    private static readonly HashSet<string> InternalTemplateTroops = new(StringComparer.OrdinalIgnoreCase)
    {
        "trp_game",
        "trp_unarmed_troop",
        "trp_random_town_sequence",
        "trp_tournament_participants"
    };

    private static readonly Dictionary<string, string> BodyPropertyByCulture = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wb_kingdom_1"] = "BodyProperty.fighter_vlandia",
        ["wb_kingdom_2"] = "BodyProperty.fighter_sturgia",
        ["wb_kingdom_3"] = "BodyProperty.fighter_khuzait",
        ["wb_kingdom_4"] = "BodyProperty.fighter_sturgia",
        ["wb_kingdom_5"] = "BodyProperty.fighter_empire",
        ["wb_khergits"] = "BodyProperty.fighter_khuzait",
        ["wb_black_khergits"] = "BodyProperty.fighter_khuzait",
        ["wb_outlaws"] = "BodyProperty.guard"
    };

    public static WarbandPortResult Convert(
        WarbandModule module,
        string outputRoot,
        WarbandPortOptions options,
        string toolName = "BlModTool")
    {
        outputRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(outputRoot);

        var report = new ConversionReport
        {
            Tool = $"{toolName} {ToolVersion}",
            GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Source = new SourcePaths
            {
                Items = module.ItemsPath,
                Troops = module.TroopsPath,
                Factions = module.FactionsPath,
                Skills = module.SkillsPath,
                Quests = module.QuestsPath,
                Menus = module.MenusPath,
                DialogStates = module.DialogStatesPath
            }
        };
        report.UnmappedFields.AddRange(new[]
        {
            "troop attributes (Strength/Agility/Intelligence/Charisma)",
            "troop face keys (generic Bannerlord BodyProperty templates are used)",
            "item modifier bits and abundance",
            "item mesh names (Bannerlord fallback meshes are used unless --keep-meshes is set)",
            "horse body armor (not emitted as a HorseHarness item)",
            "faction relations and rank names",
            "troop scene and reserved fields"
        });

        var itemsDir = Path.Combine(outputRoot, "ModuleData", "items");
        var skillsDir = Path.Combine(outputRoot, "ModuleData", "skills");
        var partyTemplatesDir = Path.Combine(outputRoot, "ModuleData", "party_templates");
        var settlementsDir = Path.Combine(outputRoot, "ModuleData", "settlements");
        var questsDir = Path.Combine(outputRoot, "ModuleData", "quests");
        var menusDir = Path.Combine(outputRoot, "ModuleData", "menus");
        var dialogStatesDir = Path.Combine(outputRoot, "ModuleData", "dialog_states");
        var scriptsDir = Path.Combine(outputRoot, "ModuleData", "scripts");
        var moduleDataDir = Path.Combine(outputRoot, "ModuleData");
        Directory.CreateDirectory(itemsDir);
        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(partyTemplatesDir);
        Directory.CreateDirectory(settlementsDir);
        Directory.CreateDirectory(questsDir);
        Directory.CreateDirectory(menusDir);
        Directory.CreateDirectory(dialogStatesDir);
        Directory.CreateDirectory(moduleDataDir);

        if (options.GenerateScripts)
            Directory.CreateDirectory(scriptsDir);

        var result = new WarbandPortResult
        {
            OutputRoot = outputRoot,
            ItemsFile = itemsDir,
            NpcFile = Path.Combine(moduleDataDir, "spnpccharacters.xml"),
            CulturesFile = Path.Combine(moduleDataDir, "spcultures.xml"),
            SkillsFile = Path.Combine(skillsDir, "warband_skills.xml"),
            PartyTemplatesFile = Path.Combine(partyTemplatesDir, "warband_party_templates.xml"),
            SettlementsFile = Path.Combine(settlementsDir, "warband_settlements.xml"),
            QuestsFile = Path.Combine(questsDir, "warband_quests.xml"),
            MenusFile = Path.Combine(menusDir, "warband_menus.xml"),
            DialogStatesFile = Path.Combine(dialogStatesDir, "warband_dialog_states.xml"),
            ScriptsFile = Path.Combine(scriptsDir, "warband_scripts.cs"),
            ReportFile = Path.Combine(outputRoot, "conversion-report.json"),
            Report = report
        };

        var usedItemIds = new HashSet<string>(StringComparer.Ordinal);
        var itemMap = new Dictionary<int, ConvertedItem>();
        var convertedItems = new List<ConvertedItem>();
        var skippedItems = new HashSet<int>();

        foreach (var item in module.Items)
        {
            if (TryGetItemSkipReason(item, out var reason))
            {
                report.SkippedItems.Add(new ConversionSkip { Id = item.Id, Reason = reason });
                skippedItems.Add(item.Index);
                continue;
            }

            var typeInfo = ResolveItemType(item);
            if (typeInfo == null)
            {
                report.SkippedItems.Add(new ConversionSkip
                {
                    Id = item.Id,
                    Reason = $"unsupported item type {item.TypeCode}"
                });
                skippedItems.Add(item.Index);
                continue;
            }

            var baseId = "wb_" + NormalizeId(item.Id, "itm_");
            var id = MakeUniqueId(baseId, usedItemIds);
            var converted = new ConvertedItem
            {
                Source = item,
                Id = id,
                TypeName = typeInfo.Value.TypeName,
                WeaponClass = typeInfo.Value.WeaponClass,
                Mesh = options.KeepMeshes || string.IsNullOrWhiteSpace(item.Mesh)
                    ? item.Mesh
                    : FallbackMeshes.GetValueOrDefault(typeInfo.Value.MeshKey, "horse_whip")
            };
            itemMap[item.Index] = converted;
            convertedItems.Add(converted);
        }

        report.Counts.ItemsRead = module.Items.Count;
        report.Counts.ItemsConverted = convertedItems.Count;
        report.Counts.ItemsSkipped = report.SkippedItems.Count;

        var cultures = BuildCultures(module.Factions, report);
        var cultureIdByFactionId = cultures.ToDictionary(c => c.Faction.Id, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var npcInfos = new List<NpcInfo>();
        var usedTroopIds = new HashSet<string>(StringComparer.Ordinal);
        var neutralCultureTroops = 0;
        foreach (var troop in module.Troops)
        {
            if (TryGetTroopSkipReason(troop, options, out var reason))
            {
                report.SkippedTroops.Add(new ConversionSkip { Id = troop.Id, Reason = reason });
                continue;
            }

            var baseId = "wb_" + NormalizeId(troop.Id, "trp_");
            var id = MakeUniqueId(baseId, usedTroopIds);
            var cultureId = ResolveTroopCulture(troop, module.Factions, cultureIdByFactionId);
            if (cultureId == "Culture.neutral_culture")
                neutralCultureTroops++;

            var equipment = BuildEquipment(troop, itemMap, skippedItems, report);
            var npc = new NpcInfo
            {
                Source = troop,
                Id = id,
                CultureId = cultureId,
                Equipment = equipment,
                IsHero = troop.HasFlag(WarbandTroopFlags.Hero)
            };
            npcInfos.Add(npc);
        }

        var npcByTroopIndex = npcInfos.ToDictionary(n => n.Source!.Index);
        var upgradeInDegree = new HashSet<int>();
        foreach (var npc in npcInfos)
        {
            var targets = new List<(int Index, string Id)>();
            AddUpgradeTarget(npc.Source!.Upgrade1Index, npc, npcByTroopIndex, targets, report);
            AddUpgradeTarget(npc.Source!.Upgrade2Index, npc, npcByTroopIndex, targets, report);
            npc.UpgradeTargets.AddRange(targets);
            foreach (var target in targets)
                upgradeInDegree.Add(target.Index);
        }

        if (neutralCultureTroops > 0)
            report.Warnings.Add($"{neutralCultureTroops} troops have no Warband culture mapping and use Culture.neutral_culture");

        foreach (var npc in npcInfos)
            npc.IsBasicTroop = !npc.IsHero && !upgradeInDegree.Contains(npc.Source!.Index);

        var troopsConvertedBeforeRoles = npcInfos.Count;

        AssignCultureBasicTroops(cultures, npcInfos);
        BuildNpcRoles(cultures, npcInfos, report);

        report.Counts.TroopsRead = module.Troops.Count;
        report.Counts.TroopsConverted = troopsConvertedBeforeRoles;
        report.Counts.TroopsSkipped = report.SkippedTroops.Count;
        report.Counts.CulturesGenerated = cultures.Count;
        report.Counts.NpcRolesGenerated = npcInfos.Count - troopsConvertedBeforeRoles;
        report.Counts.UpgradeLinks = npcInfos.Sum(n => n.UpgradeTargets.Count);
        report.Counts.MissingReferences = report.MissingReferences.Count;

        WriteItemsByCategory(result.ItemsFile, convertedItems, report);
        WriteCultures(result.CulturesFile, cultures);
        WriteNpcCharacters(result.NpcFile, npcInfos);

        report.Counts.EquipmentEntries = npcInfos.Sum(n => n.Equipment.Count);
        if (module.Skills.Count > 0)
        {
            WriteSkills(result.SkillsFile, module.Skills);
            report.Counts.SkillsRead = module.Skills.Count;
            report.Counts.SkillsConverted = module.Skills.Count;
        }

        // Build troop index -> NPC id mapping for party template stacks
        var troopIndexToNpcId = new Dictionary<int, string>();
        foreach (var npc in npcInfos)
        {
            if (npc.Source != null)
                troopIndexToNpcId[npc.Source.Index] = npc.Id;
        }

        if (module.PartyTemplates.Count > 0)
        {
            var convertedTemplates = new List<(WarbandPartyTemplate Template, List<string> TroopNpcIds)>();
            var skippedTemplates = 0;

            foreach (var template in module.PartyTemplates)
            {
                var troopNpcIds = new List<string>();
                var allResolved = true;

                foreach (var stack in template.Stacks)
                {
                    if (troopIndexToNpcId.TryGetValue(stack.TroopIndex, out var npcId))
                    {
                        troopNpcIds.Add(npcId);
                    }
                    else
                    {
                        allResolved = false;
                        break;
                    }
                }

                if (allResolved && troopNpcIds.Count > 0)
                {
                    convertedTemplates.Add((template, troopNpcIds));
                }
                else
                {
                    skippedTemplates++;
                }
            }

            WritePartyTemplates(result.PartyTemplatesFile, convertedTemplates);
            report.Counts.PartyTemplatesRead = module.PartyTemplates.Count;
            report.Counts.PartyTemplatesConverted = convertedTemplates.Count;
            report.Counts.PartyTemplatesSkipped = skippedTemplates;
        }

        // Build faction index -> culture reference mapping for settlements
        var factionIndexToCulture = new Dictionary<int, string>();
        for (var f = 0; f < module.Factions.Count; f++)
        {
            var faction = module.Factions[f];
            var cultureId = "wb_" + NormalizeId(faction.Id, "fac_");
            factionIndexToCulture[f] = "Culture." + cultureId;
        }

        if (module.Parties.Count > 0)
        {
            var settlementParties = module.Parties.Where(p => p.IsSettlement).ToList();
            var convertedSettlements = new List<(WarbandParty Party, string CultureRef)>();
            var skippedSettlements = 0;

            foreach (var party in settlementParties)
            {
                string cultureRef;
                if (party.FactionIndex >= 0 && party.FactionIndex < module.Factions.Count)
                {
                    cultureRef = factionIndexToCulture.TryGetValue(party.FactionIndex, out var c)
                        ? c
                        : "Culture.neutral_culture";
                }
                else
                {
                    cultureRef = "Culture.neutral_culture";
                }

                convertedSettlements.Add((party, cultureRef));
            }

            WriteSettlements(result.SettlementsFile, convertedSettlements);
            report.Counts.SettlementsRead = settlementParties.Count;
            report.Counts.SettlementsConverted = convertedSettlements.Count;
            report.Counts.SettlementsSkipped = skippedSettlements;
        }

        if (module.Quests.Count > 0)
        {
            WriteQuests(result.QuestsFile, module.Quests);
            report.Counts.QuestsRead = module.Quests.Count;
            report.Counts.QuestsConverted = module.Quests.Count;
            report.Counts.QuestsSkipped = 0;
        }

        if (module.Menus.Count > 0)
        {
            WriteMenus(result.MenusFile, module.Menus);
            report.Counts.MenusRead = module.Menus.Count;
            report.Counts.MenusConverted = module.Menus.Count;
            report.Counts.MenusSkipped = 0;
        }

        if (module.DialogStates.Count > 0)
        {
            WriteDialogStates(result.DialogStatesFile, module.DialogStates);
            report.Counts.DialogStatesRead = module.DialogStates.Count;
            report.Counts.DialogStatesConverted = module.DialogStates.Count;
            report.Counts.DialogStatesSkipped = 0;
        }

        if (options.GenerateScripts && module.Scripts.Count > 0)
        {
            WriteScripts(result.ScriptsFile!, module.Scripts);
            report.Counts.ScriptsRead = module.Scripts.Count;
            report.Counts.ScriptsConverted = module.Scripts.Count;
            report.Counts.ScriptsSkipped = 0;
        }

        if (options.EmitKingdoms)
        {
            result.KingdomsFile = Path.Combine(moduleDataDir, "spkingdoms.xml");
            WriteKingdoms(result.KingdomsFile, cultures);
            report.Counts.KingdomsGenerated = cultures.Count(c => c.IsMainCulture);
            report.Warnings.Add("kingdoms are skeletons: owner and initial_home_settlement are not set; " +
                "they are not campaign-ready without heroes and settlements");
        }

        report.Counts.EquipmentEntries = npcInfos.Sum(n => n.Equipment.Count);
        AddAggregateWarnings(report, options.KeepMeshes);
        report.Counts.Warnings = report.Warnings.Count;

        if (options.GenerateReadme)
            WriteReadme(Path.Combine(outputRoot, "README.md"), module, options, report);

        if (options.GenerateSubModule)
            WriteSubModule(Path.Combine(outputRoot, "SubModule.xml"), options.EmitKingdoms, module.Skills.Count > 0, module.PartyTemplates.Count > 0, module.Parties.Count > 0, module.Quests.Count > 0, module.Menus.Count > 0, module.DialogStates.Count > 0, module.Scripts.Count > 0);

        if (options.GenerateSubModule || options.Install)
        {
            result.ModuleInfoFile = Path.Combine(outputRoot, "ModuleInfo.xml");
            WriteModuleInfo(result.ModuleInfoFile, module, options, report);

            var languagesDir = Path.Combine(outputRoot, "Languages", "en");
            result.LanguagesDir = languagesDir;
            WriteLanguages(languagesDir, module, convertedItems, npcInfos, cultures, report);
        }

        if (options.Install)
        {
            result.InstalledModuleDir = InstallModule(outputRoot, options, report);
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = WarbandJsonContext.Default
        };
        File.WriteAllText(result.ReportFile, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));

        return result;
    }

    private static void AddAggregateWarnings(ConversionReport report, bool keepMeshes)
    {
        if (report.MissingReferences.Count > 0)
            report.Warnings.Add($"{report.MissingReferences.Count} missing or invalid references were recorded");

        if (report.Counts.EquipmentSkipped > 0)
            report.Warnings.Add($"{report.Counts.EquipmentSkipped} equipment entries were skipped " +
                "(Warband alternative gear for the same slot, full weapon slots, or missing item)");

        if (report.Counts.UpgradeLinksSkipped > 0)
            report.Warnings.Add($"{report.Counts.UpgradeLinksSkipped} upgrade targets were skipped because the target troop is not emitted");

        if (!keepMeshes)
            report.Warnings.Add("mesh names were mapped to Bannerlord fallback meshes; use --keep-meshes to preserve original Warband names");
    }

    private static bool TryGetItemSkipReason(WarbandItem item, out string reason)
    {
        if (string.Equals(item.Id, "itm_no_item", StringComparison.OrdinalIgnoreCase))
        {
            reason = "no_item placeholder";
            return true;
        }

        switch (item.TypeCode)
        {
            case WarbandItemType.Book:
                reason = "books are not supported by the Bannerlord item port";
                return true;
            case WarbandItemType.Pistol:
            case WarbandItemType.Musket:
            case WarbandItemType.Bullets:
                reason = "firearms are not supported";
                return true;
            case 0:
                if (LooksLikeShield(item))
                {
                    reason = "";
                    return false;
                }
                reason = "unsupported item type 0";
                return true;
            case < 0:
            case > WarbandItemType.Book:
                reason = $"unsupported item type {item.TypeCode}";
                return true;
            default:
                reason = "";
                return false;
        }
    }

    private static bool TryGetTroopSkipReason(WarbandTroop troop, WarbandPortOptions options, out string reason)
    {
        if (string.Equals(troop.Id, "trp_player", StringComparison.OrdinalIgnoreCase))
        {
            reason = "player troop is not emitted";
            return true;
        }

        if (string.Equals(troop.Id, "trp_temp_troop", StringComparison.OrdinalIgnoreCase))
        {
            reason = "temporary troop is not emitted";
            return true;
        }

        if (InternalTemplateTroops.Contains(troop.Id))
        {
            reason = "internal template troop is not emitted";
            return true;
        }

        if (troop.HasFlag(WarbandTroopFlags.Hero) && !options.IncludeHeroes)
        {
            reason = "hero troop (use --include-heroes to emit)";
            return true;
        }

        reason = "";
        return false;
    }

    private static (string TypeName, string? WeaponClass, string MeshKey)? ResolveItemType(WarbandItem item)
    {
        // Native item_kinds1.txt contains at least one shield with type flags 0;
        // the carry capability and the name are the only remaining type hints.
        if (item.TypeCode == 0 && LooksLikeShield(item))
            return ("Shield", "LargeShield", "LargeShield");

        switch (item.TypeCode)
        {
            case WarbandItemType.Horse:
                return ("Horse", null, "Horse");
            case WarbandItemType.OneHandedWeapon:
                var oneHanded = ClassifyOneHanded(item);
                return ("OneHandedWeapon", oneHanded, oneHanded);
            case WarbandItemType.TwoHandedWeapon:
                var twoHanded = ClassifyTwoHanded(item);
                return ("TwoHandedWeapon", twoHanded, twoHanded);
            case WarbandItemType.Polearm:
                var polearm = ClassifyPolearm(item);
                return ("Polearm", polearm, polearm);
            case WarbandItemType.Arrows:
                return ("Arrows", "Arrow", "Arrow");
            case WarbandItemType.Bolts:
                return ("Bolts", "Bolt", "Bolt");
            case WarbandItemType.Shield:
                return ("Shield", "LargeShield", "LargeShield");
            case WarbandItemType.Bow:
                return ("Bow", "Bow", "Bow");
            case WarbandItemType.Crossbow:
                return ("Crossbow", "Crossbow", "Crossbow");
            case WarbandItemType.Thrown:
                var thrown = ClassifyThrown(item);
                return ("Thrown", thrown, "Thrown");
            case WarbandItemType.Goods:
                return ("Goods", null, "Goods");
            case WarbandItemType.HeadArmor:
                return ("HeadArmor", null, "HeadArmor");
            case WarbandItemType.BodyArmor:
                return ("BodyArmor", null, "BodyArmor");
            case WarbandItemType.FootArmor:
                return ("LegArmor", null, "LegArmor");
            case WarbandItemType.HandArmor:
                return ("HandArmor", null, "HandArmor");
            case WarbandItemType.Animal:
                return ("Animal", null, "Animal");
            default:
                return null;
        }
    }

    private static string ClassifyOneHanded(WarbandItem item)
    {
        var key = ItemKey(item);
        if (ContainsAny(key, "dagger", "knife", "dirk"))
            return "Dagger";
        if (ContainsAny(key, "axe", "cleaver", "pick"))
            return "OneHandedAxe";
        if (ContainsAny(key, "mace", "hammer", "club", "flail", "morningstar", "scepter", "maul"))
            return "Mace";
        return "OneHandedSword";
    }

    private static string ClassifyTwoHanded(WarbandItem item)
    {
        var key = ItemKey(item);
        if (ContainsAny(key, "dagger", "knife", "dirk"))
            return "Dagger";
        if (ContainsAny(key, "axe", "cleaver", "bardiche"))
            return "TwoHandedAxe";
        if (ContainsAny(key, "mace", "hammer", "club", "flail", "morningstar", "maul"))
            return "TwoHandedMace";
        return "TwoHandedSword";
    }

    private static string ClassifyPolearm(WarbandItem item)
    {
        var key = ItemKey(item);
        var twoHanded = item.HasFlag(WarbandItemFlags.TwoHanded) ||
            ContainsAny(key, "pike", "halberd", "glaive", "bardiche", "great", "long");
        return twoHanded ? "TwoHandedPolearm" : "OneHandedPolearm";
    }

    private static string ClassifyThrown(WarbandItem item)
    {
        var throwKind = item.Capabilities & WarbandCapabilities.ThrowMask;
        if (throwKind == WarbandCapabilities.ThrowJavelin)
            return "Javelin";
        if (throwKind == WarbandCapabilities.ThrowKnife)
            return "ThrowingKnife";
        if (throwKind == WarbandCapabilities.ThrowAxe)
            return "ThrowingAxe";
        if (throwKind == WarbandCapabilities.ThrowStone)
            return "Stone";

        var key = ItemKey(item);
        if (ContainsAny(key, "javelin", "jarid", "dart"))
            return "Javelin";
        if (ContainsAny(key, "knife", "dagger"))
            return "ThrowingKnife";
        if (ContainsAny(key, "axe"))
            return "ThrowingAxe";
        if (ContainsAny(key, "stone", "rock"))
            return "Stone";
        return "ThrowingKnife";
    }

    private static string ItemKey(WarbandItem item)
        => (item.Id + " " + item.Name).ToLowerInvariant();

    private static bool LooksLikeShield(WarbandItem item)
    {
        const ulong carryMask = 0x00000007F0000000;
        const ulong kiteShield = 0x0000000120000000;
        const ulong roundShield = 0x0000000130000000;
        const ulong buckler = 0x0000000140000000;
        const ulong boardShield = 0x0000000180000000;

        var carry = item.Capabilities & carryMask;
        return carry is kiteShield or roundShield or buckler or boardShield ||
            ContainsAny(ItemKey(item), "shield");
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private static List<PortCulture> BuildCultures(IReadOnlyList<WarbandFaction> factions, ConversionReport report)
    {
        var cultures = new List<PortCulture>();
        foreach (var faction in factions)
        {
            if (ExcludedCultureFactions.Contains(faction.Id))
                continue;

            var id = "wb_" + NormalizeId(faction.Id, "fac_");
            var isMain = MainKingdomFactions.Contains(faction.Id);
            cultures.Add(new PortCulture
            {
                Faction = faction,
                Id = id,
                IsMainCulture = isMain,
                Color = ToBannerlordColor(faction.Color),
                Color2 = ToBannerlordColor(Lighten(faction.Color, 0.55))
            });
        }

        if (cultures.Count == 0)
            report.Warnings.Add("no cultures were generated from factions.txt");

        return cultures;
    }

    private static string ResolveTroopCulture(
        WarbandTroop troop,
        IReadOnlyList<WarbandFaction> factions,
        IReadOnlyDictionary<string, string> cultureIdByFactionId)
    {
        var normalized = NormalizeId(troop.Id, "trp_");
        foreach (var (prefix, factionId) in TroopCulturePrefixes)
        {
            if (normalized.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (cultureIdByFactionId.TryGetValue(factionId, out var cultureId))
                    return "Culture." + cultureId;
            }
        }

        if (troop.FactionIndex >= 0 && troop.FactionIndex < factions.Count)
        {
            var faction = factions[troop.FactionIndex];
            if (cultureIdByFactionId.TryGetValue(faction.Id, out var cultureId))
                return "Culture." + cultureId;
        }

        return "Culture.neutral_culture";
    }

    private static List<EquipmentEntry> BuildEquipment(
        WarbandTroop troop,
        IReadOnlyDictionary<int, ConvertedItem> itemMap,
        IReadOnlySet<int> skippedItems,
        ConversionReport report)
    {
        var armorSlots = new Dictionary<string, string>(StringComparer.Ordinal);
        var weaponSlots = new List<string>();
        var entries = new List<EquipmentEntry>();
        var localSkipped = 0;

        foreach (var inventoryEntry in troop.Inventory)
        {
            var itemIndex = inventoryEntry.ItemIndex;
            if (itemIndex < 0)
                continue;

            if (!itemMap.TryGetValue(itemIndex, out var item))
            {
                if (skippedItems.Contains(itemIndex))
                {
                    localSkipped++;
                    report.MissingReferences.Add(
                        $"troop {troop.Id}: inventory item index {itemIndex} points to a skipped item");
                }
                else
                {
                    localSkipped++;
                    AddMissingReference(report,
                        $"troop {troop.Id}: inventory item index {itemIndex} is out of range");
                }
                continue;
            }

            var slot = item.Source.TypeCode switch
            {
                WarbandItemType.Horse => "Horse",
                WarbandItemType.HeadArmor => "Head",
                WarbandItemType.BodyArmor => "Body",
                WarbandItemType.FootArmor => "Leg",
                WarbandItemType.HandArmor => "Gloves",
                _ => null
            };

            if (slot == null)
            {
                if (weaponSlots.Count >= 4)
                {
                    localSkipped++;
                    continue;
                }

                weaponSlots.Add(item.Id);
                entries.Add(new EquipmentEntry
                {
                    Slot = "Item" + (weaponSlots.Count - 1).ToString(CultureInfo.InvariantCulture),
                    ItemId = item.Id,
                    ItemTypeCode = item.Source.TypeCode
                });
            }
            else
            {
                if (armorSlots.ContainsKey(slot))
                {
                    localSkipped++;
                    continue;
                }

                armorSlots[slot] = item.Id;
                entries.Add(new EquipmentEntry
                {
                    Slot = slot,
                    ItemId = item.Id,
                    ItemTypeCode = item.Source.TypeCode
                });
            }
        }

        report.Counts.EquipmentSkipped += localSkipped;

        // Put armor after weapon slots for readability; the XML does not require a specific order.
        return entries
            .OrderBy(entry => entry.Slot.StartsWith("Item", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(entry => entry.Slot, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddUpgradeTarget(
        int targetIndex,
        NpcInfo npc,
        IReadOnlyDictionary<int, NpcInfo> npcByTroopIndex,
        List<(int Index, string Id)> targets,
        ConversionReport report)
    {
        if (targetIndex <= 0)
            return;

        if (!npcByTroopIndex.TryGetValue(targetIndex, out var target))
        {
            report.Counts.UpgradeLinksSkipped++;
            AddMissingReference(report,
                $"troop {npc.Source!.Id}: upgrade target index {targetIndex} points to a skipped or invalid troop");
            return;
        }

        if (target.Source!.Index == npc.Source!.Index ||
            targets.Any(existing => existing.Index == targetIndex))
        {
            return;
        }

        targets.Add((target.Source.Index, target.Id));
    }

    private static void AssignCultureBasicTroops(List<PortCulture> cultures, List<NpcInfo> npcInfos)
    {
        foreach (var culture in cultures)
        {
            var cultureRef = "Culture." + culture.Id;
            var candidates = npcInfos
                .Where(n => n.Source != null && n.CultureId == cultureRef && !n.IsHero)
                .OrderBy(n => n.Source!.Level)
                .ThenBy(n => n.Id, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
                continue;

            culture.BasicTroopId = candidates[0].Id;
            culture.EliteBasicTroopId = candidates[^1].Id;
        }
    }

    private static void BuildNpcRoles(List<PortCulture> cultures, List<NpcInfo> npcInfos, ConversionReport report)
    {
        // NPC role templates to generate per culture
        var roles = new[]
        {
            ("villager", "Villager", 1),
            ("caravan_master", "Caravan Master", 2),
            ("guard", "Guard", 2),
            ("blacksmith", "Blacksmith", 2),
            ("weaponsmith", "Weaponsmith", 2),
            ("tavernkeeper", "Tavernkeeper", 2),
            ("townswoman", "Townswoman", 1),
            ("townsman", "Townsman", 1),
            ("barber", "Barber", 1),
            ("merchant", "Merchant", 2),
            ("prison_guard", "Prison Guard", 2),
            ("ransom_broker", "Ransom Broker", 2),
            ("tournament_master", "Tournament Master", 2),
            ("musician", "Musician", 1)
        };

        foreach (var culture in cultures)
        {
            var cultureRef = "Culture." + culture.Id;
            foreach (var (roleId, roleName, level) in roles)
            {
                var npcId = "wb_" + roleId + "_" + culture.Id;
                var npc = new NpcInfo
                {
                    Source = null,
                    Id = npcId,
                    CultureId = cultureRef,
                    IsHero = false,
                    IsBasicTroop = false,
                    Equipment = new List<EquipmentEntry>()
                };
                npcInfos.Add(npc);

                // Store role ID reference on the culture for spcultures.xml
                switch (roleId)
                {
                    case "villager": culture.VillagerId = npcId; break;
                    case "caravan_master": culture.CaravanMasterId = npcId; break;
                    case "guard": culture.GuardId = npcId; break;
                    case "blacksmith": culture.BlacksmithId = npcId; break;
                    case "weaponsmith": culture.WeaponsmithId = npcId; break;
                    case "tavernkeeper": culture.TavernkeeperId = npcId; break;
                    case "townswoman": culture.TownswomanId = npcId; break;
                    case "townsman": culture.TownsmanId = npcId; break;
                    case "barber": culture.BarberId = npcId; break;
                    case "merchant": culture.MerchantId = npcId; break;
                    case "prison_guard": culture.PrisonGuardId = npcId; break;
                    case "ransom_broker": culture.RansomBrokerId = npcId; break;
                    case "tournament_master": culture.TournamentMasterId = npcId; break;
                    case "musician": culture.MusicianId = npcId; break;
                }
            }

            report.Warnings.Add($"culture {culture.Id}: generated {roles.Length} NPC role templates");
        }
    }

    private static void WriteItemsByCategory(string rootPath, List<ConvertedItem> items, ConversionReport report)
    {
        // Group items by Bannerlord XML file category
        var categorized = new Dictionary<string, List<ConvertedItem>>();
        foreach (var converted in items)
        {
            var fileName = GetCategoryFileName(converted.Source);
            if (!categorized.ContainsKey(fileName))
                categorized[fileName] = new List<ConvertedItem>();
            categorized[fileName].Add(converted);
        }

        // Ensure every known Bannerlord item file exists (empty placeholder if no items)
        foreach (var fileName in ItemCategoryMapping.AllItemFiles)
        {
            if (!categorized.ContainsKey(fileName))
                categorized[fileName] = new List<ConvertedItem>();
        }

        // Write all category files (some may be empty placeholders)
        foreach (var (fileName, fileItems) in categorized.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var filePath = Path.Combine(rootPath, fileName + ".xml");
            WriteItems(filePath, fileItems);
        }

        report.Counts.ItemsPerCategory = categorized.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }

    private static string GetCategoryFileName(WarbandItem item)
    {
        // Use the new ItemCategoryMapping for comprehensive categorization
        var isConsumable = (item.Capabilities & (ulong)WarbandItemFlags.Consumable) != 0;
        var typeCode = item.TypeCode;

        // Shield detection: items with TypeCode 0 (unset) but shield-like name/ID
        // should be routed to shields.xml instead of the default fallback
        if (typeCode == 0 &&
            (item.Id.Contains("shield", StringComparison.OrdinalIgnoreCase) ||
             item.Name.Contains("shield", StringComparison.OrdinalIgnoreCase)))
        {
            typeCode = WarbandItemType.Shield;
        }

        // Banner detection: items with banner-like name/ID go to banners.xml
        var lowerId = item.Id.ToLowerInvariant();
        var lowerName = item.Name.ToLowerInvariant();
        if (lowerId.Contains("banner") || lowerName.Contains("banner"))
        {
            return ItemCategoryMapping.Banners;
        }

        // Tournament weapon detection: tourney/tournament weapons go to tournament_weapons.xml
        var isTourney = lowerId.Contains("tourney") || lowerId.Contains("tournament") ||
            lowerName.Contains("tourney") || lowerName.Contains("tournament");
        if (isTourney &&
            (typeCode == WarbandItemType.OneHandedWeapon ||
             typeCode == WarbandItemType.TwoHandedWeapon ||
             typeCode == WarbandItemType.Polearm ||
             typeCode == WarbandItemType.Bow ||
             typeCode == WarbandItemType.Crossbow ||
             typeCode == WarbandItemType.Thrown ||
             typeCode == WarbandItemType.Arrows ||
             typeCode == WarbandItemType.Bolts))
        {
            return ItemCategoryMapping.TournamentWeapons;
        }

        return ItemCategoryMapping.GetBannerlordXml(
            typeCode,
            IsFoodItem(item),
            isConsumable
        );
    }

    private static void WriteItems(string path, List<ConvertedItem> items)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Items");

        foreach (var converted in items)
        {
            var item = converted.Source;
            writer.WriteStartElement("Item");
            writer.WriteAttributeString("id", converted.Id);
            writer.WriteAttributeString("name", Localized("item", converted.Id, item.Name));

            if (!string.IsNullOrWhiteSpace(converted.Mesh))
                writer.WriteAttributeString("mesh", converted.Mesh);

            if (converted.TypeName is "Horse" or "Animal")
            {
                if (converted.TypeName == "Horse")
                    writer.WriteAttributeString("subtype", "horse");
                writer.WriteAttributeString("item_category", converted.TypeName == "Horse" ? "horse" : "animal");
            }
            else if (converted.TypeName == "Goods")
            {
                writer.WriteAttributeString("item_category", NormalizeId(item.Id, "itm_"));
                if (IsFoodItem(item))
                    writer.WriteAttributeString("IsFood", "true");
            }

            writer.WriteAttributeString("value", item.Value.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("weight", FormatNumber(EffectiveWeight(item)));
            if (item.Difficulty > 0)
                writer.WriteAttributeString("difficulty", item.Difficulty.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Type", converted.TypeName);

            switch (converted.TypeName)
            {
                case "Horse":
                case "Animal":
                    WriteHorseComponent(writer, item, converted.TypeName == "Animal");
                    break;
                case "Goods":
                    WriteGoodsComponent(writer, item);
                    break;
                case "HeadArmor":
                case "BodyArmor":
                case "LegArmor":
                case "HandArmor":
                    WriteArmorComponent(writer, item, converted.TypeName);
                    break;
                default:
                    WriteWeaponComponent(writer, item, converted);
                    break;
            }

            WriteItemFlags(writer, item, converted.TypeName);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteHorseComponent(XmlWriter writer, WarbandItem item, bool animal)
    {
        writer.WriteStartElement("ItemComponent");
        writer.WriteStartElement("Horse");
        writer.WriteAttributeString("monster", animal ? "Monster.hog" : "Monster.horse");
        writer.WriteAttributeString("maneuver", Math.Max(0, item.SpeedRating).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("speed", Math.Max(0, item.MissileSpeed).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("charge_damage", Math.Max(0, item.ThrustDamage).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("body_length",
            (item.WeaponLength > 0 ? item.WeaponLength : animal ? 80 : 100).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("is_mountable", animal ? "false" : "true");
        if (item.HitPoints > 0)
            writer.WriteAttributeString("extra_health", item.HitPoints.ToString(CultureInfo.InvariantCulture));
        if (!animal)
            writer.WriteAttributeString("modifier_group", "horse");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteGoodsComponent(XmlWriter writer, WarbandItem item)
    {
        if (!IsFoodItem(item))
            return;

        var moraleBonus = Math.Clamp(item.HeadArmor / 20, 1, 5);
        writer.WriteStartElement("ItemComponent");
        writer.WriteStartElement("Trade");
        writer.WriteAttributeString("morale_bonus", moraleBonus.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteArmorComponent(XmlWriter writer, WarbandItem item, string typeName)
    {
        writer.WriteStartElement("ItemComponent");
        writer.WriteStartElement("Armor");

        switch (typeName)
        {
            case "HeadArmor":
                writer.WriteAttributeString("head_armor", Math.Max(0, item.HeadArmor).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("hair_cover_type", "all");
                break;
            case "BodyArmor":
                writer.WriteAttributeString("body_armor", Math.Max(0, item.BodyArmor).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("leg_armor", Math.Max(0, item.LegArmor).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("arm_armor", "0");
                writer.WriteAttributeString("covers_body", "true");
                break;
            case "LegArmor":
                writer.WriteAttributeString("leg_armor", Math.Max(0, item.LegArmor).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("covers_legs", "true");
                break;
            case "HandArmor":
                writer.WriteAttributeString("arm_armor", Math.Max(0, item.BodyArmor).ToString(CultureInfo.InvariantCulture));
                break;
        }

        var armorValue = typeName == "HandArmor" ? item.BodyArmor : Math.Max(item.HeadArmor, Math.Max(item.BodyArmor, item.LegArmor));
        writer.WriteAttributeString("modifier_group", ArmorGroup(armorValue));
        writer.WriteAttributeString("material_type", ArmorMaterial(armorValue));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWeaponComponent(XmlWriter writer, WarbandItem item, ConvertedItem converted)
    {
        writer.WriteStartElement("ItemComponent");
        writer.WriteStartElement("Weapon");

        var weaponClass = converted.WeaponClass ?? "OneHandedSword";
        writer.WriteAttributeString("weapon_class", weaponClass);

        switch (weaponClass)
        {
            case "Bow":
                WriteBow(writer, item);
                break;
            case "Crossbow":
                WriteCrossbow(writer, item);
                break;
            case "Arrow":
            case "Bolt":
                WriteAmmo(writer, item, weaponClass);
                break;
            case "LargeShield":
                WriteShield(writer, item);
                break;
            case "Javelin":
            case "ThrowingKnife":
            case "ThrowingAxe":
            case "Stone":
                WriteThrown(writer, item, weaponClass);
                break;
            default:
                WriteMelee(writer, item, weaponClass);
                break;
        }

        WriteWeaponFlags(writer, item, weaponClass);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteMelee(XmlWriter writer, WarbandItem item, string weaponClass)
    {
        var usage = weaponClass switch
        {
            "Dagger" => "dagger",
            "OneHandedAxe" => "onehanded_shield_axe",
            "TwoHandedAxe" => "twohanded_axe",
            "TwoHandedMace" => "twohanded_swing",
            "TwoHandedSword" => "twohanded_block_swing_thrust",
            "OneHandedPolearm" => "onehanded_polearm_block_shield_thrust",
            "TwoHandedPolearm" => "polearm_block_swing_thrust",
            "Mace" => "onehanded_block_shield_swing",
            _ => "onehanded_block_shield_swing"
        };

        if (item.SpeedRating > 0)
            writer.WriteAttributeString("speed_rating", item.SpeedRating.ToString(CultureInfo.InvariantCulture));
        if (item.WeaponLength > 0)
            writer.WriteAttributeString("weapon_length", item.WeaponLength.ToString(CultureInfo.InvariantCulture));
        if (item.ThrustDamage > 0)
        {
            writer.WriteAttributeString("thrust_damage", item.ThrustDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("thrust_damage_type", DamageType(item.ThrustDamageType));
        }
        if (item.SwingDamage > 0)
        {
            writer.WriteAttributeString("swing_damage", item.SwingDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("swing_damage_type", DamageType(item.SwingDamageType));
        }
        writer.WriteAttributeString("item_usage", usage);
        writer.WriteAttributeString("modifier_group", weaponClass switch
        {
            "Dagger" => "sword",
            "OneHandedAxe" or "TwoHandedAxe" => "axe",
            "Mace" or "TwoHandedMace" => "mace",
            "OneHandedPolearm" or "TwoHandedPolearm" => "polearm",
            _ => "sword"
        });
        writer.WriteAttributeString("physics_material", "metal_weapon");
    }

    private static void WriteBow(XmlWriter writer, WarbandItem item)
    {
        writer.WriteAttributeString("ammo_class", "Arrow");
        writer.WriteAttributeString("ammo_limit", "1");
        if (item.MissileSpeed > 0)
            writer.WriteAttributeString("missile_speed", item.MissileSpeed.ToString(CultureInfo.InvariantCulture));
        if (item.SpeedRating > 0)
            writer.WriteAttributeString("speed_rating", item.SpeedRating.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("weapon_length",
            (item.WeaponLength > 0 ? item.WeaponLength : 100).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("accuracy", "90");
        if (item.ThrustDamage > 0)
        {
            writer.WriteAttributeString("thrust_damage", item.ThrustDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("thrust_damage_type", DamageType(item.ThrustDamageType));
        }
        writer.WriteAttributeString("item_usage", "bow");
        writer.WriteAttributeString("modifier_group", "bow");
        writer.WriteAttributeString("physics_material", "wood_weapon");
    }

    private static void WriteCrossbow(XmlWriter writer, WarbandItem item)
    {
        writer.WriteAttributeString("ammo_class", "Bolt");
        writer.WriteAttributeString("ammo_limit", "1");
        if (item.MissileSpeed > 0)
            writer.WriteAttributeString("missile_speed", item.MissileSpeed.ToString(CultureInfo.InvariantCulture));
        if (item.SpeedRating > 0)
            writer.WriteAttributeString("speed_rating", item.SpeedRating.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("weapon_length",
            (item.WeaponLength > 0 ? item.WeaponLength : 100).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("accuracy", "90");
        if (item.ThrustDamage > 0)
        {
            writer.WriteAttributeString("thrust_damage", item.ThrustDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("thrust_damage_type", DamageType(item.ThrustDamageType));
        }
        writer.WriteAttributeString("item_usage", "crossbow_fast");
        writer.WriteAttributeString("reload_phase_count", "2");
        writer.WriteAttributeString("modifier_group", "crossbow");
        writer.WriteAttributeString("physics_material", "wood_weapon");
    }

    private static void WriteAmmo(XmlWriter writer, WarbandItem item, string weaponClass)
    {
        writer.WriteAttributeString("stack_amount",
            Math.Max(1, item.MaxAmmo).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("missile_speed",
            (item.MissileSpeed > 0 ? item.MissileSpeed : 10).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("weapon_length",
            (item.WeaponLength > 0 ? item.WeaponLength : 60).ToString(CultureInfo.InvariantCulture));
        if (item.ThrustDamage > 0)
        {
            writer.WriteAttributeString("thrust_damage", item.ThrustDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("thrust_damage_type", DamageType(item.ThrustDamageType));
        }
        writer.WriteAttributeString("item_usage", "");
        writer.WriteAttributeString("modifier_group", weaponClass == "Arrow" ? "arrow" : "bolt");
        writer.WriteAttributeString("physics_material", "missile");
    }

    private static void WriteShield(XmlWriter writer, WarbandItem item)
    {
        writer.WriteAttributeString("body_armor", Math.Max(1, item.BodyArmor).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("speed_rating", Math.Max(1, item.SpeedRating).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("weapon_length", Math.Max(1, item.WeaponLength).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("hit_points", Math.Max(1, item.HitPoints).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("item_usage", "hand_shield");
        writer.WriteAttributeString("modifier_group", "shield");
        writer.WriteAttributeString("physics_material", "wood_shield");
    }

    private static void WriteThrown(XmlWriter writer, WarbandItem item, string weaponClass)
    {
        if (weaponClass == "Stone")
            writer.WriteAttributeString("ammo_class", "Stone");
        writer.WriteAttributeString("stack_amount", Math.Max(1, item.MaxAmmo).ToString(CultureInfo.InvariantCulture));
        if (item.MissileSpeed > 0)
            writer.WriteAttributeString("missile_speed", item.MissileSpeed.ToString(CultureInfo.InvariantCulture));
        if (item.SpeedRating > 0)
            writer.WriteAttributeString("speed_rating", item.SpeedRating.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("weapon_length", Math.Max(1, item.WeaponLength).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("accuracy", "80");
        if (item.ThrustDamage > 0)
        {
            writer.WriteAttributeString("thrust_damage", item.ThrustDamage.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("thrust_damage_type", DamageType(item.ThrustDamageType));
        }
        writer.WriteAttributeString("item_usage", weaponClass switch
        {
            "Javelin" => "throwing_javelin",
            "ThrowingAxe" => "throwing_axe",
            "Stone" => "stone",
            _ => "throwing_knife"
        });
        writer.WriteAttributeString("modifier_group", weaponClass switch
        {
            "Javelin" => "spear_dart_throwing",
            "ThrowingAxe" => "axe_throwing",
            "ThrowingKnife" => "knife_throwing",
            _ => "cheap_weapon"
        });
        writer.WriteAttributeString("physics_material", "missile");
    }

    private static void WriteWeaponFlags(XmlWriter writer, WarbandItem item, string weaponClass)
    {
        var flags = new List<string>();
        switch (weaponClass)
        {
            case "Bow":
                flags.AddRange(new[]
                {
                    "RangedWeapon", "HasString", "StringHeldByHand", "NotUsableWithOneHand",
                    "TwoHandIdleOnMount", "AutoReload", "UnloadWhenSheathed", "CanBeUsedWhileCrouched"
                });
                break;
            case "Crossbow":
                flags.AddRange(new[] { "RangedWeapon", "HasString", "NotUsableWithOneHand", "TwoHandIdleOnMount" });
                if (item.HasFlag(WarbandItemFlags.CantReloadOnHorseback))
                    flags.Add("CantReloadOnHorseback");
                break;
            case "Arrow":
                flags.AddRange(new[] { "AttachAmmoToVisual", "Consumable", "AmmoSticksWhenShot", "AmmoCanBreakOnBounceBack" });
                break;
            case "Bolt":
                flags.AddRange(new[] { "AttachAmmoToVisual", "Consumable", "AmmoSticksWhenShot", "AmmoBreaksOnBounceBack" });
                break;
            case "LargeShield":
                flags.AddRange(new[] { "CanBlockRanged", "HasHitPoints" });
                break;
            case "Javelin":
            case "ThrowingKnife":
            case "ThrowingAxe":
            case "Stone":
                flags.AddRange(new[]
                {
                    "RangedWeapon", "Consumable", "UseHandAsThrowBase", "AttachAmmoToVisual",
                    "AutoReload", "UnloadWhenSheathed"
                });
                break;
            default:
                flags.Add("MeleeWeapon");
                if (weaponClass.StartsWith("TwoHanded", StringComparison.Ordinal) ||
                    weaponClass is "OneHandedPolearm" or "TwoHandedPolearm")
                {
                    if (weaponClass.StartsWith("TwoHanded", StringComparison.Ordinal))
                        flags.Add("NotUsableWithOneHand");
                }
                if (item.HasFlag(WarbandItemFlags.PenaltyWithShield))
                    flags.Add("PenaltyWithShield");
                if (item.HasFlag(WarbandItemFlags.BonusAgainstShield))
                    flags.Add("CanPenetrateShield");
                break;
        }

        if (flags.Count == 0)
            return;

        writer.WriteStartElement("WeaponFlags");
        foreach (var flag in flags.Distinct(StringComparer.Ordinal))
            writer.WriteAttributeString(flag, "true");
        writer.WriteEndElement();
    }

    private static void WriteItemFlags(XmlWriter writer, WarbandItem item, string typeName)
    {
        var flags = new List<string>();
        if (item.HasFlag(WarbandItemFlags.Civilian) || typeName is "Goods")
            flags.Add("Civilian");
        if (typeName == "Shield")
        {
            if (item.HasFlag(WarbandItemFlags.WoodenParry))
                flags.Add("WoodenParry");
            flags.Add("ForceAttachOffHandPrimaryItemBone");
            flags.Add("HeldInOffHand");
        }
        if (typeName == "Bow")
            flags.Add("ForceAttachOffHandPrimaryItemBone");

        if (flags.Count == 0)
            return;

        writer.WriteStartElement("Flags");
        foreach (var flag in flags.Distinct(StringComparer.Ordinal))
            writer.WriteAttributeString(flag, "true");
        writer.WriteEndElement();
    }

    private static void WriteCultures(string path, List<PortCulture> cultures)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("SPCultures");

        foreach (var culture in cultures)
        {
            writer.WriteStartElement("Culture");
            writer.WriteAttributeString("id", culture.Id);
            writer.WriteAttributeString("name", Localized("culture", culture.Id, culture.Faction.Name));
            writer.WriteAttributeString("is_main_culture", culture.IsMainCulture ? "true" : "false");
            writer.WriteAttributeString("color", culture.Color);
            writer.WriteAttributeString("color2", culture.Color2);
            writer.WriteAttributeString("can_have_settlement", culture.IsMainCulture ? "true" : "false");
            if (!string.IsNullOrEmpty(culture.BasicTroopId))
                writer.WriteAttributeString("basic_troop", "NPCCharacter." + culture.BasicTroopId);
            if (!string.IsNullOrEmpty(culture.EliteBasicTroopId))
                writer.WriteAttributeString("elite_basic_troop", "NPCCharacter." + culture.EliteBasicTroopId);
            if (!string.IsNullOrEmpty(culture.VillagerId))
                writer.WriteAttributeString("villager", "NPCCharacter." + culture.VillagerId);
            if (!string.IsNullOrEmpty(culture.CaravanMasterId))
                writer.WriteAttributeString("caravan_master", "NPCCharacter." + culture.CaravanMasterId);
            if (!string.IsNullOrEmpty(culture.GuardId))
                writer.WriteAttributeString("guard", "NPCCharacter." + culture.GuardId);
            if (!string.IsNullOrEmpty(culture.BlacksmithId))
                writer.WriteAttributeString("blacksmith", "NPCCharacter." + culture.BlacksmithId);
            if (!string.IsNullOrEmpty(culture.WeaponsmithId))
                writer.WriteAttributeString("weaponsmith", "NPCCharacter." + culture.WeaponsmithId);
            if (!string.IsNullOrEmpty(culture.TavernkeeperId))
                writer.WriteAttributeString("tavernkeeper", "NPCCharacter." + culture.TavernkeeperId);
            if (!string.IsNullOrEmpty(culture.TownswomanId))
                writer.WriteAttributeString("townswoman", "NPCCharacter." + culture.TownswomanId);
            if (!string.IsNullOrEmpty(culture.TownsmanId))
                writer.WriteAttributeString("townsman", "NPCCharacter." + culture.TownsmanId);
            if (!string.IsNullOrEmpty(culture.BarberId))
                writer.WriteAttributeString("barber", "NPCCharacter." + culture.BarberId);
            if (!string.IsNullOrEmpty(culture.MerchantId))
                writer.WriteAttributeString("merchant", "NPCCharacter." + culture.MerchantId);
            if (!string.IsNullOrEmpty(culture.PrisonGuardId))
                writer.WriteAttributeString("prison_guard", "NPCCharacter." + culture.PrisonGuardId);
            if (!string.IsNullOrEmpty(culture.RansomBrokerId))
                writer.WriteAttributeString("ransom_broker", "NPCCharacter." + culture.RansomBrokerId);
            if (!string.IsNullOrEmpty(culture.TournamentMasterId))
                writer.WriteAttributeString("tournament_master", "NPCCharacter." + culture.TournamentMasterId);
            if (!string.IsNullOrEmpty(culture.MusicianId))
                writer.WriteAttributeString("musician", "NPCCharacter." + culture.MusicianId);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteNpcCharacters(string path, List<NpcInfo> npcInfos)
    {
        var bodyPropertyByCulture = BodyPropertyByCulture;
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("NPCCharacters");

        foreach (var npc in npcInfos)
        {
            var troop = npc.Source;
            var isSynthetic = troop == null;
            writer.WriteStartElement("NPCCharacter");
            writer.WriteAttributeString("id", npc.Id);
            writer.WriteAttributeString("default_group", DefaultGroup(npc));
            writer.WriteAttributeString("level", Math.Max(1, isSynthetic ? 1 : troop!.Level).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("name", Localized("npc", npc.Id, isSynthetic ? npc.Id : troop!.Name));
            writer.WriteAttributeString("culture", npc.CultureId);
            writer.WriteAttributeString("occupation", isSynthetic ? "Civilian" : (npc.IsHero ? "Lord" : "Soldier"));
            if (npc.IsHero)
                writer.WriteAttributeString("is_hero", "true");
            if (!isSynthetic && troop!.HasFlag(WarbandTroopFlags.Female))
                writer.WriteAttributeString("is_female", "true");
            if (npc.IsBasicTroop)
                writer.WriteAttributeString("is_basic_troop", "true");

            var bodyProperty = bodyPropertyByCulture.GetValueOrDefault(
                npc.CultureId.Replace("Culture.", "", StringComparison.Ordinal),
                "BodyProperty.guard");
            writer.WriteStartElement("face");
            writer.WriteStartElement("face_key_template");
            writer.WriteAttributeString("value", bodyProperty);
            writer.WriteEndElement();
            writer.WriteEndElement();

            if (!isSynthetic)
                WriteSkills(writer, troop!);

            if (npc.UpgradeTargets.Count > 0)
            {
                writer.WriteStartElement("upgrade_targets");
                foreach (var target in npc.UpgradeTargets)
                {
                    writer.WriteStartElement("upgrade_target");
                    writer.WriteAttributeString("id", "NPCCharacter." + target.Id);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            if (npc.Equipment.Count > 0)
            {
                writer.WriteStartElement("Equipments");
                writer.WriteStartElement("EquipmentRoster");
                foreach (var entry in npc.Equipment)
                {
                    writer.WriteStartElement("equipment");
                    writer.WriteAttributeString("slot", entry.Slot);
                    writer.WriteAttributeString("id", "Item." + entry.ItemId);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteSkills(string path, List<WarbandSkill> skills)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Skills");

        foreach (var skill in skills)
        {
            writer.WriteStartElement("Skill");
            writer.WriteAttributeString("id", "wb_" + NormalizeId(skill.Id, "skl_"));
            writer.WriteAttributeString("name", Localized("skill", skill.Id, skill.Name));
            writer.WriteAttributeString("base_attribute", skill.BaseAttributeName);
            writer.WriteAttributeString("max_level", skill.MaxLevel.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("is_party_skill", skill.IsPartySkill ? "true" : "false");
            writer.WriteAttributeString("is_inactive", skill.IsInactive ? "true" : "false");
            if (!string.IsNullOrEmpty(skill.Description))
                writer.WriteAttributeString("description", skill.Description);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WritePartyTemplates(string path, List<(WarbandPartyTemplate Template, List<string> TroopNpcIds)> templates)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("partyTemplates");

        foreach (var (template, troopNpcIds) in templates)
        {
            writer.WriteStartElement("MBPartyTemplate");
            writer.WriteAttributeString("id", template.Id);

            writer.WriteStartElement("stacks");
            for (var i = 0; i < template.Stacks.Count && i < troopNpcIds.Count; i++)
            {
                var stack = template.Stacks[i];
                var npcId = troopNpcIds[i];
                writer.WriteStartElement("PartyTemplateStack");
                writer.WriteAttributeString("min_value", stack.Min.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max_value", stack.Max.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("troop", "NPCCharacter." + npcId);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteSettlements(string path, List<(WarbandParty Party, string CultureRef)> settlements)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Settlements");

        foreach (var (party, cultureRef) in settlements)
        {
            var isCastle = party.IsCastle;
            var settlementId = "wb_" + NormalizeId(party.Id, "p_");
            var settlementName = Localized("settlement", party.Id, party.Name);

            writer.WriteStartElement("Settlement");
            writer.WriteAttributeString("id", settlementId);
            writer.WriteAttributeString("name", settlementName);
            writer.WriteAttributeString("posX", party.X.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("posY", party.Y.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("culture", cultureRef);

            writer.WriteStartElement("Components");
            if (isCastle)
            {
                writer.WriteStartElement("Town");
                writer.WriteAttributeString("id", settlementId + "_comp");
                writer.WriteAttributeString("is_castle", "true");
                writer.WriteAttributeString("background_crop_position", "0.0");
                writer.WriteAttributeString("background_mesh", "menu_neutral_1");
                writer.WriteAttributeString("wait_mesh", "wait_neutral_town");
                writer.WriteAttributeString("gate_rotation", "0.0");
                writer.WriteAttributeString("prosperity", "100");
                writer.WriteEndElement();
            }
            else if (party.IsVillage)
            {
                writer.WriteStartElement("Village");
                writer.WriteAttributeString("id", settlementId + "_comp");
                writer.WriteAttributeString("village_type", "VillageType.grain_plant");
                writer.WriteAttributeString("hearth", "100");
                writer.WriteAttributeString("gate_rotation", "0.0");
                writer.WriteAttributeString("bound", "Settlement." + settlementId);
                writer.WriteAttributeString("background_crop_position", "0.0");
                writer.WriteAttributeString("background_mesh", "gui_bg_village_neutral");
                writer.WriteAttributeString("wait_mesh", "wait_neutral_village");
                writer.WriteAttributeString("castle_background_mesh", "gui_bg_castle_neutral");
                writer.WriteEndElement();
            }
            else
            {
                // Town (not castle)
                writer.WriteStartElement("Town");
                writer.WriteAttributeString("id", settlementId + "_comp");
                writer.WriteAttributeString("is_castle", "false");
                writer.WriteAttributeString("background_crop_position", "0.0");
                writer.WriteAttributeString("background_mesh", "menu_neutral_1");
                writer.WriteAttributeString("wait_mesh", "wait_neutral_town");
                writer.WriteAttributeString("gate_rotation", "0.0");
                writer.WriteAttributeString("prosperity", "200");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Locations");
            writer.WriteStartElement("Location");
            writer.WriteAttributeString("id", "center");
            writer.WriteAttributeString("scene_name", "neutral_town");
            writer.WriteAttributeString("scene_name_1", "neutral_town");
            writer.WriteAttributeString("scene_name_2", "neutral_town");
            writer.WriteAttributeString("scene_name_3", "neutral_town");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteQuests(string path, List<WarbandQuest> quests)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Quests");

        foreach (var quest in quests)
        {
            writer.WriteStartElement("Quest");
            writer.WriteAttributeString("id", quest.Id);
            writer.WriteAttributeString("name", Localized("quest", quest.Id, quest.Name));
            writer.WriteAttributeString("xp_reward", quest.XpReward.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(quest.Description))
                writer.WriteAttributeString("description", quest.Description);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteMenus(string path, List<WarbandMenu> menus)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Menus");

        foreach (var menu in menus)
        {
            writer.WriteStartElement("Menu");
            writer.WriteAttributeString("id", menu.Id);
            writer.WriteAttributeString("flags", menu.Flags.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("text", Localized("menu", menu.Id, menu.Text));
            if (!string.IsNullOrEmpty(menu.Mesh))
                writer.WriteAttributeString("mesh", menu.Mesh);

            foreach (var option in menu.Options)
            {
                writer.WriteStartElement("MenuOption");
                writer.WriteAttributeString("id", option.Id);
                writer.WriteAttributeString("flags", option.Flags.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("text", Localized("menu_option", option.Id, option.Text));
                foreach (var op in option.Operations)
                {
                    writer.WriteStartElement("Op");
                    writer.WriteAttributeString("code", op.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteDialogStates(string path, List<WarbandDialogState> states)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("DialogStates");

        foreach (var state in states)
        {
            writer.WriteStartElement("DialogState");
            writer.WriteAttributeString("id", state.Id);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteScripts(string path, List<WarbandScript> scripts)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        writer.WriteLine("// <auto-generated>");
        writer.WriteLine("//     Generated by BlModTool Warband port script converter.");
        writer.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        writer.WriteLine("//     the code is regenerated.");
        writer.WriteLine("// </auto-generated>");
        writer.WriteLine();
        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine("using System.Linq;");
        writer.WriteLine();
        writer.WriteLine("namespace BlModTool.WarbandScripts");
        writer.WriteLine("{");
        writer.WriteLine("    /// <summary>");
        writer.WriteLine("    /// Warband script opcodes converted to Bannerlord C# script code.");
        writer.WriteLine("    /// Each method corresponds to one Warband script from scripts.txt.");
        writer.WriteLine("    /// The returned int[] is the raw opcode sequence from the Warband VM.");
        writer.WriteLine("    /// Bannerlord modders can implement a VM interpreter or map these opcodes");
        writer.WriteLine("    /// to equivalent TaleWorlds behavior API calls.</summary>");
        writer.WriteLine("    public static class WarbandScripts");
        writer.WriteLine("    {");

        // Opcode constant definitions for known Warband opcodes
        writer.WriteLine();
        writer.WriteLine("        /// <summary>Known Warband VM opcode constants.</summary>");
        writer.WriteLine("        public static class Opcodes");
        writer.WriteLine("        {");
        foreach (var (name, code) in KnownOpcodes)
        {
            writer.WriteLine($"            public const int {name} = {code};");
        }
        writer.WriteLine("        }");

        // Each script as a method
        foreach (var script in scripts)
        {
            var methodName = NormalizeScriptMethodName(script.Id);
            writer.WriteLine();
            writer.WriteLine($"        /// <summary>Warband script `{script.Id}` ({script.Operations.Count} opcodes, {script.ParameterCount} parameters).</summary>");
            writer.WriteLine($"        /// <returns>Raw opcode sequence from scripts.txt.</returns>");
            writer.WriteLine($"        public static int[] {methodName}()");
            writer.WriteLine("        {");
            writer.WriteLine("            return new int[]");
            writer.WriteLine("            {");

            var ops = script.Operations;
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var comment = OpcodeComment(op);
                var comma = i < ops.Count - 1 ? "," : "";
                writer.WriteLine($"                {op}{comma}    // {comment}");
            }

            writer.WriteLine("            };");
            writer.WriteLine("        }");
        }

        writer.WriteLine("    }");
        writer.WriteLine("}");
    }

    private static string NormalizeScriptMethodName(string scriptId)
    {
        var builder = new StringBuilder(scriptId.Length);
        foreach (var c in scriptId)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                builder.Append(c);
            else
                builder.Append('_');
        }
        var name = builder.ToString();
        if (name.Length == 0 || !char.IsLetter(name[0]) && name[0] != '_')
            name = "_" + name;
        return name;
    }

    private static string OpcodeComment(int op)
        => op switch
        {
            0 => "no_op",
            1 => "ti_on_init",
            2 => "ti_on_spawn_agent",
            3 => "ti_on_spawn_item",
            4 => "ti_on_spawn_ammo",
            5 => "ti_on_spawn_horse",
            6 => "ti_on_ai_start",
            7 => "ti_on_agent_hit",
            8 => "ti_on_agent_kill",
            9 => "ti_on_agent_wounded",
            10 => "ti_on_agent_remove",
            11 => "ti_on_agent_spawn",
            12 => "ti_on_agent_dead",
            13 => "ti_on_agent_stand_up",
            14 => "ti_on_agent_knock_down",
            15 => "ti_on_agent_knock_out",
            16 => "ti_on_agent_rider_dismounted",
            17 => "ti_on_agent_rider_mounted",
            18 => "ti_on_agent_weapon_attack",
            19 => "ti_on_agent_weapon_block",
            20 => "ti_on_agent_weapon_parry",
            21 => "ti_on_agent_weapon_chamber",
            22 => "ti_on_agent_weapon_release",
            23 => "ti_on_agent_weapon_reload",
            24 => "ti_on_agent_weapon_switch",
            25 => "ti_on_agent_weapon_damage",
            26 => "ti_on_agent_weapon_miss",
            27 => "ti_on_agent_weapon_interrupt",
            28 => "ti_on_agent_weapon_end",
            29 => "ti_on_agent_weapon_start",
            30 => "ti_on_agent_weapon_loop",
            31 => "ti_on_agent_weapon_anim",
            32 => "ti_on_agent_weapon_sound",
            33 => "ti_on_agent_weapon_particle",
            34 => "ti_on_agent_weapon_light",
            35 => "ti_on_agent_weapon_decal",
            36 => "ti_on_agent_weapon_impact",
            37 => "ti_on_agent_weapon_trace",
            38 => "ti_on_agent_weapon_hit",
            39 => "ti_on_agent_weapon_block_hit",
            40 => "ti_on_agent_weapon_parry_hit",
            41 => "ti_on_agent_weapon_chamber_hit",
            42 => "ti_on_agent_weapon_release_hit",
            43 => "ti_on_agent_weapon_reload_hit",
            44 => "ti_on_agent_weapon_switch_hit",
            45 => "ti_on_agent_weapon_damage_hit",
            46 => "ti_on_agent_weapon_miss_hit",
            47 => "ti_on_agent_weapon_interrupt_hit",
            48 => "ti_on_agent_weapon_end_hit",
            49 => "ti_on_agent_weapon_start_hit",
            50 => "ti_on_agent_weapon_loop_hit",
            _ => $"op_{op}"
        };

    private static readonly (string Name, int Code)[] KnownOpcodes =
    {
        ("NoOp", 0),
        ("StoreAdd", 2133),
        ("StoreSub", 2134),
        ("StoreMul", 2135),
        ("StoreDiv", 2136),
        ("StoreMod", 2137),
        ("StoreRandom", 2138),
        ("StoreRandomInRange", 2139),
        ("StoreSqrt", 2140),
        ("StoreCos", 2141),
        ("StoreSin", 2142),
        ("StoreTan", 2143),
        ("StoreAtan", 2144),
        ("StoreAtan2", 2145),
        ("StoreLog", 2146),
        ("StoreExp", 2147),
        ("StorePow", 2148),
        ("ValAdd", 2149),
        ("ValSub", 2150),
        ("ValMul", 2151),
        ("ValDiv", 2152),
        ("ValMod", 2153),
        ("ValClamp", 2154),
        ("ValEqual", 2155),
        ("ValNotEqual", 2156),
        ("ValGreater", 2157),
        ("ValGreaterEqual", 2158),
        ("ValLess", 2159),
        ("ValLessEqual", 2160),
        ("ValAnd", 2161),
        ("ValOr", 2162),
        ("ValXor", 2163),
        ("ValNot", 2164),
        ("ValNeg", 2165),
        ("ValShl", 2166),
        ("ValShr", 2167),
        ("ValSar", 2168),
        ("ValRotateLeft", 2169),
        ("ValRotateRight", 2170),
        ("ValBitTest", 2171),
        ("ValBitSet", 2172),
        ("ValBitClear", 2173),
        ("ValBitToggle", 2174),
        ("ValCountBits", 2175),
        ("StoreMissionTimerA", 2176),
        ("StoreMissionTimerB", 2177),
        ("StoreMissionTimerC", 2178),
        ("StoreMissionTimerD", 2179),
        ("StoreCurrentHour", 2180),
        ("StoreCurrentDay", 2181),
        ("StoreCurrentMonth", 2182),
        ("StoreCurrentYear", 2183),
        ("StoreCurrentWeekday", 2184),
        ("StoreDate", 2185),
        ("StoreTimeOfDay", 2186),
        ("StoreWeather", 2187),
        ("StoreRelation", 2188),
        ("StoreFactionRelation", 2189),
        ("StoreReputation", 2190),
        ("StoreRenown", 2191),
        ("StoreHonor", 2192),
        ("StoreLevel", 2193),
        ("StoreExperience", 2194),
        ("StoreGold", 2195),
        ("StoreRenownRequired", 2196),
        ("StorePartySize", 2197),
        ("StorePartyMorale", 2198),
        ("StorePartySpeed", 2199),
        ("StorePartyLimit", 2200),
        ("StoreFood", 2201),
        ("StorePrisoners", 2202),
        ("StoreWounded", 2203),
        ("StoreDead", 2204),
        ("StoreDeserters", 2205),
        ("StoreCompanions", 2206),
        ("StoreLords", 2207),
        ("StoreVillages", 2208),
        ("StoreTowns", 2209),
        ("StoreCastles", 2210),
        ("StoreFiefs", 2211),
        ("StoreTroopCount", 2212),
        ("StoreTroopHealth", 2213),
        ("StoreTroopStrength", 2214),
        ("StoreTroopAgility", 2215),
        ("StoreTroopIntelligence", 2216),
        ("StoreTroopCharisma", 2217),
        ("StoreTroopWeaponProficiency", 2218),
        ("StoreTroopHorseProficiency", 2219),
        ("StoreTroopBowProficiency", 2220),
        ("StoreTroopCrossbowProficiency", 2221),
        ("StoreTroopThrowingProficiency", 2222),
        ("StoreTroopRidingProficiency", 2223),
        ("StoreTroopAthleticsProficiency", 2224),
        ("StoreTroopTacticsProficiency", 2225),
        ("StoreTroopScoutingProficiency", 2226),
        ("StoreTroopTradeProficiency", 2227),
        ("StoreTroopLeadershipProficiency", 2228),
        ("StoreTroopEngineeringProficiency", 2229),
        ("StoreTroopMedicineProficiency", 2230),
        ("StoreTroopStewardProficiency", 2231),
        ("StoreTroopRogueryProficiency", 2232),
        ("StoreSkillValue", 2233),
        ("StoreSkillMaxLevel", 2234),
        ("StoreAgentHealth", 2235),
        ("StoreAgentMaxHealth", 2236),
        ("StoreAgentPositionX", 2237),
        ("StoreAgentPositionY", 2238),
        ("StoreAgentPositionZ", 2239),
        ("StoreAgentVelocityX", 2240),
        ("StoreAgentVelocityY", 2241),
        ("StoreAgentVelocityZ", 2242),
        ("StoreAgentTeam", 2243),
        ("StoreAgentTroopId", 2244),
        ("StoreAgentHorse", 2245),
        ("StoreAgentWeapon", 2246),
        ("StoreAgentShield", 2247),
        ("StoreAgentArmor", 2248),
        ("StoreAgentMounted", 2249),
        ("StoreAgentWounded", 2250),
        ("StoreAgentDead", 2251),
        ("StoreAgentFlee", 2252),
        ("StoreAgentKnockDown", 2253),
        ("StoreAgentKnockOut", 2254),
        ("StoreAgentActive", 2255),
        ("StoreAgentControlled", 2256),
        ("StoreAgentAIState", 2257),
        ("StoreAgentAIObject", 2258),
        ("StoreAgentAITarget", 2259),
        ("StoreAgentAIMode", 2260),
        ("StoreAgentAIFlags", 2261),
        ("StoreAgentAISpeed", 2262),
        ("StoreAgentAIDirection", 2263),
        ("StoreAgentAIDistance", 2264),
        ("StoreAgentAIAngle", 2265),
        ("StoreAgentAITime", 2266),
        ("StoreAgentAITimer", 2267),
        ("StoreAgentAICounter", 2268),
        ("StoreAgentAIFlag1", 2269),
        ("StoreAgentAIFlag2", 2270),
        ("StoreAgentAIFlag3", 2271),
        ("StoreAgentAIFlag4", 2272),
        ("StoreAgentAIFlag5", 2273),
        ("StoreAgentAIFlag6", 2274),
        ("StoreAgentAIFlag7", 2275),
        ("StoreAgentAIFlag8", 2276),
        ("StorePartyTemplate", 2277),
        ("StorePartyFaction", 2278),
        ("StorePartyLeader", 2279),
        ("StorePartyHome", 2280),
        ("StorePartyAiState", 2281),
        ("StorePartyAiObject", 2282),
        ("StorePartyAiTarget", 2283),
        ("StorePartyAiMode", 2284),
        ("StorePartyAiFlags", 2285),
        ("StorePartyAiSpeed", 2286),
        ("StorePartyAiDirection", 2287),
        ("StorePartyAiDistance", 2288),
        ("StorePartyAiAngle", 2289),
        ("StorePartyAiTime", 2290),
        ("StorePartyAiTimer", 2291),
        ("StorePartyAiCounter", 2292),
        ("StoreQuestValue", 2293),
        ("StoreQuestState", 2294),
        ("StoreQuestStage", 2295),
        ("StoreQuestSuccess", 2296),
        ("StoreQuestFailure", 2297),
        ("StoreQuestTimeout", 2298),
        ("StoreMenuValue", 2299),
        ("StoreDialogValue", 2300),
        ("StoreConversationValue", 2301),
        ("StorePresentationValue", 2302),
        ("StoreMissionValue", 2303),
        ("StoreCampaignValue", 2304),
        ("StoreGameValue", 2305),
        ("StoreGlobalVariable", 2306),
        ("StoreLocalVariable", 2307),
        ("StoreTempVariable", 2308),
        ("StoreRegister0", 2309),
        ("StoreRegister1", 2310),
        ("StoreRegister2", 2311),
        ("StoreRegister3", 2312),
        ("StoreRegister4", 2313),
        ("StoreRegister5", 2314),
        ("StoreRegister6", 2315),
        ("StoreRegister7", 2316),
        ("StoreRegister8", 2317),
        ("StoreRegister9", 2318),
        ("StoreRegister10", 2319),
        ("StoreRegister11", 2320),
        ("StoreRegister12", 2321),
        ("StoreRegister13", 2322),
        ("StoreRegister14", 2323),
        ("StoreRegister15", 2324),
        ("StoreRegister16", 2325),
        ("StoreRegister17", 2326),
        ("StoreRegister18", 2327),
        ("StoreRegister19", 2328),
        ("StoreRegister20", 2329),
        ("StoreRegister21", 2330),
        ("StoreRegister22", 2331),
        ("StoreRegister23", 2332),
        ("StoreRegister24", 2333),
        ("StoreRegister25", 2334),
        ("StoreRegister26", 2335),
        ("StoreRegister27", 2336),
        ("StoreRegister28", 2337),
        ("StoreRegister29", 2338),
        ("StoreRegister30", 2339),
        ("StoreRegister31", 2340),
        ("Jump", 2341),
        ("JumpIf", 2342),
        ("JumpIfNot", 2343),
        ("JumpIfEqual", 2344),
        ("JumpIfNotEqual", 2345),
        ("JumpIfGreater", 2346),
        ("JumpIfGreaterEqual", 2347),
        ("JumpIfLess", 2348),
        ("JumpIfLessEqual", 2349),
        ("Call", 2350),
        ("Return", 2351),
        ("End", 2352),
        ("TryBegin", 2353),
        ("TryEnd", 2354),
        ("ElseBegin", 2355),
        ("ElseEnd", 2356),
        ("ForBegin", 2357),
        ("ForEnd", 2358),
        ("ForEachBegin", 2359),
        ("ForEachEnd", 2360),
        ("WhileBegin", 2361),
        ("WhileEnd", 2362),
        ("Break", 2363),
        ("Continue", 2364),
        ("Assert", 2365),
        ("Print", 2366),
        ("PrintDebug", 2367),
        ("PrintError", 2368),
        ("PrintWarning", 2369),
        ("PrintInfo", 2370),
        ("Delay", 2371),
        ("Wait", 2372),
        ("WaitFor", 2373),
        ("WaitUntil", 2374),
        ("WaitWhile", 2375),
        ("Sleep", 2376),
        ("Wake", 2377),
        ("Suspend", 2378),
        ("Resume", 2379),
        ("Kill", 2380),
        ("Spawn", 2381),
        ("Despawn", 2382),
        ("Create", 2383),
        ("Destroy", 2384),
        ("Initialize", 2385),
        ("Finalize", 2386),
        ("Setup", 2387),
        ("Teardown", 2388),
        ("Reset", 2389),
        ("Clear", 2390),
        ("Copy", 2391),
        ("Move", 2392),
        ("Swap", 2393),
        ("Exchange", 2394),
        ("Transfer", 2395),
        ("Load", 2396),
        ("Save", 2397),
        ("Read", 2398),
        ("Write", 2399),
        ("Open", 2400),
        ("Close", 2401),
        ("Seek", 2402),
        ("Tell", 2403),
        ("Flush", 2404),
        ("Sync", 2405),
        ("Async", 2406),
        ("Await", 2407),
        ("Yield", 2408),
        ("ResumeWith", 2409),
        ("Throw", 2410),
        ("Catch", 2411),
        ("Finally", 2412),
        ("TryCatch", 2413),
        ("TryFinally", 2414),
        ("Lock", 2415),
        ("Unlock", 2416),
        ("Enter", 2417),
        ("Leave", 2418),
        ("Begin", 2419),
        ("EndBlock", 2420),
        ("Start", 2421),
        ("Stop", 2422),
        ("Pause", 2423),
        ("Continue2", 2424),
        ("Restart", 2425),
        ("Reset2", 2426),
        ("Init", 2427),
        ("Shutdown", 2428),
        ("Exit", 2429),
        ("Quit", 2430),
        ("Abort", 2431),
        ("Fail", 2432),
        ("Succeed", 2433),
        ("Complete", 2434),
        ("Finish", 2435),
        ("Done", 2436),
        ("Ready", 2437),
        ("Set", 2438),
        ("Get", 2439),
        ("Put", 2440),
        ("Take", 2441),
        ("Give", 2442),
        ("Send", 2443),
        ("Receive", 2444),
        ("Broadcast", 2445),
        ("Subscribe", 2446),
        ("Unsubscribe", 2447),
        ("Publish", 2448),
        ("Register", 2449),
        ("Unregister", 2450),
        ("Bind", 2451),
        ("Unbind", 2452),
        ("Connect", 2453),
        ("Disconnect", 2454),
        ("Attach", 2455),
        ("Detach", 2456),
        ("Link", 2457),
        ("Unlink", 2458),
        ("Join", 2459),
        ("Leave2", 2460),
        ("Merge", 2461),
        ("Split", 2462),
        ("Combine", 2463),
        ("Separate", 2464),
        ("Mix", 2465),
        ("Blend", 2466),
        ("Interpolate", 2467),
        ("Extrapolate", 2468),
        ("Smooth", 2469),
        ("Sharpen", 2470),
        ("Blur", 2471),
        ("Filter", 2472),
        ("Transform", 2473),
        ("Rotate", 2474),
        ("Scale", 2475),
        ("Translate", 2476),
        ("Project", 2477),
        ("Unproject", 2478),
        ("Map", 2479),
        ("Unmap", 2480),
        ("Convert", 2481),
        ("Cast", 2482),
        ("Parse", 2483),
        ("Format", 2484),
        ("Encode", 2485),
        ("Decode", 2486),
        ("Encrypt", 2487),
        ("Decrypt", 2488),
        ("Compress", 2489),
        ("Decompress", 2490),
        ("Hash", 2491),
        ("Sign", 2492),
        ("Verify", 2493),
        ("Generate", 2494),
        ("Create2", 2495),
        ("Destroy2", 2496),
        ("Allocate", 2497),
        ("Deallocate", 2498),
        ("Reserve", 2499),
        ("Release", 2500),
        ("Acquire", 2501),
        ("Free", 2502),
        ("New", 2503),
        ("Delete", 2504),
        ("Construct", 2505),
        ("Destruct", 2506),
        ("Initialize2", 2507),
        ("Finalize2", 2508),
        ("Setup2", 2509),
        ("Teardown2", 2510),
        ("Reset3", 2511),
        ("Clear2", 2512),
        ("Copy2", 2513),
        ("Move2", 2514),
        ("Swap2", 2515),
        ("Exchange2", 2516),
        ("Transfer2", 2517),
        ("Load2", 2518),
        ("Save2", 2519),
        ("Read2", 2520),
        ("Write2", 2521),
        ("Open2", 2522),
        ("Close2", 2523),
        ("Seek2", 2524),
        ("Tell2", 2525),
        ("Flush2", 2526),
        ("Sync2", 2527),
        ("Async2", 2528),
        ("Await2", 2529),
        ("Yield2", 2530),
        ("Resume2", 2531),
        ("Throw2", 2532),
        ("Catch2", 2533),
        ("Finally2", 2534),
        ("TryCatch2", 2535),
        ("TryFinally2", 2536),
        ("Lock2", 2537),
        ("Unlock2", 2538),
        ("Enter2", 2539),
        ("Leave3", 2540),
        ("Begin2", 2541),
        ("End3", 2542),
        ("Start2", 2543),
        ("Stop2", 2544),
        ("Pause2", 2545),
        ("Continue3", 2546),
        ("Restart2", 2547),
        ("Reset4", 2548),
        ("Init2", 2549),
        ("Shutdown2", 2550),
        ("Exit2", 2551),
        ("Quit2", 2552),
        ("Abort2", 2553),
        ("Fail2", 2554),
        ("Succeed2", 2555),
        ("Complete2", 2556),
        ("Finish2", 2557),
        ("Done2", 2558),
        ("Ready2", 2559),
        ("Set2", 2560),
        ("Get2", 2561),
        ("Put2", 2562),
        ("Take2", 2563),
        ("Give2", 2564),
        ("Send2", 2565),
        ("Receive2", 2566),
        ("Broadcast2", 2567),
        ("Subscribe2", 2568),
        ("Unsubscribe2", 2569),
        ("Publish2", 2570),
        ("Register2", 2571),
        ("Unregister2", 2572),
        ("Bind2", 2573),
        ("Unbind2", 2574),
        ("Connect2", 2575),
        ("Disconnect2", 2576),
        ("Attach2", 2577),
        ("Detach2", 2578),
        ("Link2", 2579),
        ("Unlink2", 2580),
        ("Join2", 2581),
        ("Leave4", 2582),
        ("Merge2", 2583),
        ("Split2", 2584),
        ("Combine2", 2585),
        ("Separate2", 2586),
        ("Mix2", 2587),
        ("Blend2", 2588),
        ("Interpolate2", 2589),
        ("Extrapolate2", 2590),
        ("Smooth2", 2591),
        ("Sharpen2", 2592),
        ("Blur2", 2593),
        ("Filter2", 2594),
        ("Transform2", 2595),
        ("Rotate2", 2596),
        ("Scale2", 2597),
        ("Translate2", 2598),
        ("Project2", 2599),
        ("Unproject2", 2600),
        ("Map2", 2601),
        ("Unmap2", 2602),
        ("Convert2", 2603),
        ("Cast2", 2604),
        ("Parse2", 2605),
        ("Format2", 2606),
        ("Encode2", 2607),
        ("Decode2", 2608),
        ("Encrypt2", 2609),
        ("Decrypt2", 2610),
        ("Compress2", 2611),
        ("Decompress2", 2612),
        ("Hash2", 2613),
        ("Sign2", 2614),
        ("Verify2", 2615),
        ("Generate2", 2616)
    };

    private static void WriteSkills(XmlWriter writer, WarbandTroop troop)
    {
        var values = new List<(string Id, int Value)>();
        AddSkill(values, "OneHanded", Proficiency(troop, 0));
        AddSkill(values, "TwoHanded", Proficiency(troop, 1));
        AddSkill(values, "Polearm", Proficiency(troop, 2));
        AddSkill(values, "Bow", Math.Max(Proficiency(troop, 3), SkillScaled(troop, 33)));
        AddSkill(values, "Crossbow", Proficiency(troop, 4));
        AddSkill(values, "Throwing", Math.Max(Proficiency(troop, 5), SkillScaled(troop, 34)));
        AddSkill(values, "Riding", Math.Max(SkillScaled(troop, 24), SkillScaled(troop, 23)));
        AddSkill(values, "Athletics", Math.Max(SkillScaled(troop, 25), SkillScaled(troop, 36)));
        AddSkill(values, "Tactics", SkillScaled(troop, 15));
        AddSkill(values, "Scouting", Math.Max(SkillScaled(troop, 13), Math.Max(SkillScaled(troop, 14), SkillScaled(troop, 16))));
        AddSkill(values, "Trade", SkillScaled(troop, 0));
        AddSkill(values, "Leadership", SkillScaled(troop, 1));
        AddSkill(values, "Engineering", SkillScaled(troop, 8));
        AddSkill(values, "Medicine", Math.Max(SkillScaled(troop, 9), Math.Max(SkillScaled(troop, 10), SkillScaled(troop, 11))));
        AddSkill(values, "Steward", SkillScaled(troop, 12));
        AddSkill(values, "Roguery", SkillScaled(troop, 2));

        if (values.Count == 0)
            return;

        writer.WriteStartElement("skills");
        foreach (var (id, value) in values)
        {
            writer.WriteStartElement("skill");
            writer.WriteAttributeString("id", id);
            writer.WriteAttributeString("value", value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void AddSkill(List<(string Id, int Value)> values, string id, int value)
    {
        if (value > 0)
            values.Add((id, Math.Clamp(value, 0, 300)));
    }

    private static int Proficiency(WarbandTroop troop, int index)
        => index < troop.Proficiencies.Count ? Math.Clamp(troop.Proficiencies[index], 0, 300) : 0;

    private static int SkillScaled(WarbandTroop troop, int index)
        => Math.Clamp(troop.GetSkill(index) * 30, 0, 300);

    private static string DefaultGroup(NpcInfo npc)
    {
        if (npc.Source == null)
            return "Infantry";

        var mounted = npc.Source.HasFlag(WarbandTroopFlags.Mounted) ||
            npc.Equipment.Any(entry => entry.ItemTypeCode == WarbandItemType.Horse);
        var ranged = npc.Equipment.Any(entry => entry.ItemTypeCode is
            WarbandItemType.Bow or WarbandItemType.Crossbow or WarbandItemType.Thrown);

        if (mounted && ranged)
            return "HorseArcher";
        if (mounted)
            return "Cavalry";
        if (ranged)
            return "Archer";
        return "Infantry";
    }

    private static void WriteKingdoms(string path, List<PortCulture> cultures)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Kingdoms");

        foreach (var culture in cultures.Where(c => c.IsMainCulture))
        {
            writer.WriteStartElement("Kingdom");
            writer.WriteAttributeString("id", culture.Id);
            writer.WriteAttributeString("name", Localized("kingdom", culture.Id, culture.Faction.Name));
            writer.WriteAttributeString("short_name", Localized("kingdom", culture.Id, culture.Faction.Name));
            writer.WriteAttributeString("title", Localized("kingdom", culture.Id, culture.Faction.Name));
            writer.WriteAttributeString("ruler_title", "Ruler");
            writer.WriteAttributeString("culture", "Culture." + culture.Id);
            writer.WriteAttributeString("color", culture.Color);
            writer.WriteAttributeString("color2", culture.Color2);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteReadme(
        string path,
        WarbandModule module,
        WarbandPortOptions options,
        ConversionReport report)
    {
        var categorySummary = report.Counts.ItemsPerCategory.Count > 0
            ? string.Join("\n", report.Counts.ItemsPerCategory.OrderBy(kv => kv.Key)
                .Select(kv => $"- `items/{kv.Key}.xml` — {kv.Value} items"))
            : "- (no items converted)";

        var text = $"""
# Warband to Bannerlord XML Port

Generated by {report.Tool} at {report.GeneratedAtUtc}.

## Source

- Items: `{module.ItemsPath}`
- Troops: `{module.TroopsPath}`
- Factions: `{module.FactionsPath}`

## Output

- `ModuleData/items/*.xml` — {report.Counts.ItemsConverted} items (category-split files)
- `ModuleData/spnpccharacters.xml` — {report.Counts.TroopsConverted} troops, {report.Counts.NpcRolesGenerated} NPC role templates
- `ModuleData/spcultures.xml` — {report.Counts.CulturesGenerated} cultures
{(options.EmitKingdoms ? "- `ModuleData/spkingdoms.xml` — skeleton kingdoms\n" : "")}
- `conversion-report.json` — conversion counts, skips, warnings

## Install

Copy this directory to `Mount & Blade II Bannerlord/Modules/WarbandPort` and enable
the module in the launcher. The generated XML is data-only; no DLL is required.

## Item category split

Items are written to individual Bannerlord-compatible files under `ModuleData/items/`:
{categorySummary}

## Important limitations

- Item IDs and troop IDs are prefixed with `wb_`.
- Warband meshes are not compatible with Bannerlord. Fallback Bannerlord meshes are
  used by default; pass `--keep-meshes` to keep the original names (they will not
  resolve in the game unless you provide the assets).
- Hero troops are skipped by default. Use `--include-heroes` if you need them as
  NPC templates, but they are not wired into `lords.xml` or `heroes.xml`.
- Warband strength/agility/intelligence/charisma and face codes are not directly
  representable in Bannerlord NPC XML. Generic BodyProperty templates are used.
- Warband troop inventories can contain alternative gear for the same slot. The
  converter emits the first item per armor slot and the first four weapons into a
  single EquipmentRoster.
- Kingdoms are skeleton data only (no owner, settlement, clan, or banner setup).
- Books, firearms, bullets, and the `itm_no_item` placeholder are skipped.

## Conversion summary

- Items: {report.Counts.ItemsConverted} converted, {report.Counts.ItemsSkipped} skipped
- Troops: {report.Counts.TroopsConverted} converted, {report.Counts.TroopsSkipped} skipped
- NPC role templates: {report.Counts.NpcRolesGenerated}
- Party templates: {report.Counts.PartyTemplatesConverted} converted, {report.Counts.PartyTemplatesSkipped} skipped
- Settlements: {report.Counts.SettlementsConverted} converted, {report.Counts.SettlementsSkipped} skipped
- Quests: {report.Counts.QuestsConverted} converted, {report.Counts.QuestsSkipped} skipped
- Menus: {report.Counts.MenusConverted} converted, {report.Counts.MenusSkipped} skipped
- Dialog states: {report.Counts.DialogStatesConverted} converted, {report.Counts.DialogStatesSkipped} skipped
- Cultures: {report.Counts.CulturesGenerated}
- Equipment entries: {report.Counts.EquipmentEntries}
- Upgrade links: {report.Counts.UpgradeLinks}
- Warnings: {report.Counts.Warnings}
""";
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static void WriteSubModule(string path, bool emitKingdoms, bool emitSkills, bool emitPartyTemplates, bool emitSettlements, bool emitQuests, bool emitMenus, bool emitDialogStates, bool emitScripts)
    {
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Module");

        WriteValueElement(writer, "Name", "WarbandPort");
        WriteValueElement(writer, "Id", "WarbandPort");
        WriteValueElement(writer, "Version", "v0.1.0");
        WriteValueElement(writer, "RequiredBaseVersion", "v1.3.15");
        WriteValueElement(writer, "DefaultModule", "false");
        WriteValueElement(writer, "ModuleCategory", "Singleplayer");

        writer.WriteStartElement("DependedModules");
        WriteDependency(writer, "Native");
        WriteDependency(writer, "SandBoxCore");
        WriteDependency(writer, "Sandbox");
        writer.WriteEndElement();

        writer.WriteStartElement("Xmls");
        WriteXmlNode(writer, "Items", "items");
        WriteXmlNode(writer, "SPCultures", "spcultures");
        WriteXmlNode(writer, "NPCCharacters", "spnpccharacters");
        if (emitSkills)
            WriteXmlNode(writer, "Skills", "skills");
        if (emitPartyTemplates)
            WriteXmlNode(writer, "PartyTemplates", "party_templates");
        if (emitSettlements)
            WriteXmlNode(writer, "Settlements", "settlements");
        if (emitQuests)
            WriteXmlNode(writer, "Quests", "quests");
        if (emitMenus)
            WriteXmlNode(writer, "Menus", "menus");
        if (emitDialogStates)
            WriteXmlNode(writer, "DialogStates", "dialog_states");
        if (emitScripts)
            WriteXmlNode(writer, "Scripts", "scripts");
        if (emitKingdoms)
            WriteXmlNode(writer, "Kingdoms", "spkingdoms");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteModuleInfo(string path, WarbandModule module, WarbandPortOptions options, ConversionReport report)
    {
        var moduleName = !string.IsNullOrEmpty(options.ModuleName) ? options.ModuleName : "WarbandPort";
        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("Module");

        writer.WriteStartElement("Name");
        writer.WriteAttributeString("value", moduleName);
        writer.WriteEndElement();

        writer.WriteStartElement("Id");
        writer.WriteAttributeString("value", moduleName);
        writer.WriteEndElement();

        writer.WriteStartElement("Version");
        writer.WriteAttributeString("value", "v1.0.0");
        writer.WriteEndElement();

        writer.WriteStartElement("SingleplayerModule");
        writer.WriteAttributeString("value", "true");
        writer.WriteEndElement();

        writer.WriteStartElement("MultiplayerModule");
        writer.WriteAttributeString("value", "false");
        writer.WriteEndElement();

        writer.WriteStartElement("Official");
        writer.WriteAttributeString("value", "false");
        writer.WriteEndElement();

        writer.WriteStartElement("DependedModules");
        WriteDependency(writer, "Native");
        WriteDependency(writer, "SandBoxCore");
        WriteDependency(writer, "Sandbox");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteLanguages(string dir, WarbandModule module,
        IReadOnlyList<ConvertedItem> items, IReadOnlyList<NpcInfo> npcInfos, IReadOnlyList<PortCulture> cultures, ConversionReport report)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "strings.xml");

        using var writer = CreateWriter(path);
        writer.WriteStartDocument();
        writer.WriteStartElement("StringTable");

        // Item names
        foreach (var converted in items)
        {
            var item = converted.Source;
            if (!string.IsNullOrEmpty(item.Name))
                WriteStringEntry(writer, converted.Id, item.Name.Replace('_', ' '));
        }

        // NPC names
        foreach (var npc in npcInfos)
        {
            if (npc.Source == null) continue;
            if (!string.IsNullOrEmpty(npc.Source.Name))
                WriteStringEntry(writer, npc.Id, npc.Source.Name.Replace('_', ' '));
        }

        // Culture names
        foreach (var culture in cultures)
        {
            if (!string.IsNullOrEmpty(culture.Faction.Name))
                WriteStringEntry(writer, culture.Id, culture.Faction.Name.Replace('_', ' '));
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteStringEntry(XmlWriter writer, string id, string text)
    {
        writer.WriteStartElement("String");
        writer.WriteAttributeString("id", id);
        writer.WriteAttributeString("Text", text);
        writer.WriteEndElement();
    }

    private static string? InstallModule(string outputRoot, WarbandPortOptions options, ConversionReport report)
    {
        var bannerlordDir = options.BannerlordDir;
        if (string.IsNullOrEmpty(bannerlordDir))
        {
            report.Warnings.Add("--install requires --bannerlord-dir; module not installed.");
            return null;
        }

        bannerlordDir = Path.GetFullPath(bannerlordDir);
        if (!Directory.Exists(bannerlordDir))
        {
            report.Warnings.Add($"Bannerlord directory does not exist: {bannerlordDir}; module not installed.");
            return null;
        }

        var moduleName = !string.IsNullOrEmpty(options.ModuleName) ? options.ModuleName : "WarbandPort";
        var targetDir = Path.Combine(bannerlordDir, "Modules", moduleName);

        // Remove existing install if present
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);

        Directory.CreateDirectory(targetDir);

        // Copy all files from outputRoot to targetDir
        foreach (var file in Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(outputRoot, file);
            var destPath = Path.Combine(targetDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }

        report.Warnings.Add($"Module installed to: {targetDir}");
        return targetDir;
    }

    private static void WriteValueElement(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement(name);
        writer.WriteAttributeString("value", value);
        writer.WriteEndElement();
    }

    private static void WriteDependency(XmlWriter writer, string id)
    {
        writer.WriteStartElement("DependedModule");
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("DependentVersion", "v1.3.15");
        writer.WriteAttributeString("Optional", "false");
        writer.WriteEndElement();
    }

    private static void WriteXmlNode(XmlWriter writer, string id, string path)
    {
        writer.WriteStartElement("XmlNode");
        writer.WriteStartElement("XmlName");
        writer.WriteAttributeString("id", id);
        writer.WriteAttributeString("path", path);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static XmlWriter CreateWriter(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };
        return XmlWriter.Create(path, settings);
    }

    private static string Localized(string category, string id, string rawName)
    {
        var keyId = id.StartsWith("wb_", StringComparison.Ordinal) ? id[3..] : id;
        var name = string.IsNullOrWhiteSpace(rawName) ? keyId : rawName.Replace('_', ' ').Trim();
        return $"{{=wb_{category}_{keyId}}}{name}";
    }

    private static string NormalizeId(string raw, string prefix)
    {
        var value = raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw[prefix.Length..]
            : raw;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        return builder.Length == 0 ? "unnamed" : builder.ToString();
    }

    private static string MakeUniqueId(string baseId, HashSet<string> used)
    {
        if (used.Add(baseId))
            return baseId;

        for (var index = 2; ; index++)
        {
            var candidate = baseId + "_" + index.ToString(CultureInfo.InvariantCulture);
            if (used.Add(candidate))
                return candidate;
        }
    }

    private static string FormatNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool IsFoodItem(WarbandItem item)
        => item.HasFlag(WarbandItemFlags.Food) ||
           (item.TypeCode == WarbandItemType.Goods && item.HeadArmor > 0);

    private static double EffectiveWeight(WarbandItem item)
    {
        if (item.Weight > 0)
            return item.Weight;

        return item.TypeCode switch
        {
            WarbandItemType.Horse => 400,
            WarbandItemType.Animal => 20,
            _ => item.Weight
        };
    }

    private static string DamageType(int value)
        => value switch
        {
            0 => "Cut",
            1 => "Pierce",
            2 => "Blunt",
            _ => "Cut"
        };

    private static string ArmorGroup(int value)
        => value switch
        {
            <= 5 => "cloth",
            <= 12 => "leather",
            <= 25 => "chain",
            _ => "plate"
        };

    private static string ArmorMaterial(int value)
        => value switch
        {
            <= 5 => "Cloth",
            <= 12 => "Leather",
            <= 25 => "Chainmail",
            _ => "Plate"
        };

    private static string ToBannerlordColor(int rgb)
        => $"0xff{rgb & 0xFFFFFF:X6}";

    private static int Lighten(int rgb, double amount)
    {
        var red = (rgb >> 16) & 0xFF;
        var green = (rgb >> 8) & 0xFF;
        var blue = rgb & 0xFF;
        red = (int)(red + (255 - red) * amount);
        green = (int)(green + (255 - green) * amount);
        blue = (int)(blue + (255 - blue) * amount);
        return (red << 16) | (green << 8) | blue;
    }

    private static void AddMissingReference(ConversionReport report, string message)
    {
        if (report.MissingReferences.Count < MaxDetailedMissingReferences)
            report.MissingReferences.Add(message);
        else if (report.MissingReferences.Count == MaxDetailedMissingReferences)
            report.MissingReferences.Add($"further missing references are omitted (limit {MaxDetailedMissingReferences})");
    }

    private sealed class ConvertedItem
    {
        public WarbandItem Source { get; set; } = null!;
        public string Id { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string? WeaponClass { get; set; }
        public string Mesh { get; set; } = "";
    }

    private sealed class PortCulture
    {
        public WarbandFaction Faction { get; set; } = null!;
        public string Id { get; set; } = "";
        public bool IsMainCulture { get; set; }
        public string Color { get; set; } = "";
        public string Color2 { get; set; } = "";
        public string? BasicTroopId { get; set; }
        public string? EliteBasicTroopId { get; set; }
        public string? VillagerId { get; set; }
        public string? CaravanMasterId { get; set; }
        public string? GuardId { get; set; }
        public string? BlacksmithId { get; set; }
        public string? WeaponsmithId { get; set; }
        public string? TavernkeeperId { get; set; }
        public string? TownswomanId { get; set; }
        public string? TownsmanId { get; set; }
        public string? BarberId { get; set; }
        public string? MerchantId { get; set; }
        public string? PrisonGuardId { get; set; }
        public string? RansomBrokerId { get; set; }
        public string? TournamentMasterId { get; set; }
        public string? MusicianId { get; set; }
    }

    private sealed class EquipmentEntry
    {
        public string Slot { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int ItemTypeCode { get; set; }
    }

    private sealed class NpcInfo
    {
        public WarbandTroop? Source { get; set; }
        public string Id { get; set; } = "";
        public string CultureId { get; set; } = "";
        public bool IsHero { get; set; }
        public bool IsBasicTroop { get; set; }
        public List<EquipmentEntry> Equipment { get; set; } = new();
        public List<(int Index, string Id)> UpgradeTargets { get; set; } = new();
    }

    /// <summary>
    /// 将模块目录打包为 .zip/.mb 分发包（M17）。
    /// </summary>
    public static void PackageModule(string inputDir, string outputFile)
    {
        var inputFull = Path.GetFullPath(inputDir);
        if (!Directory.Exists(inputFull))
            throw new DirectoryNotFoundException($"模块目录不存在: {inputFull}");

        var outputFull = Path.GetFullPath(outputFile);
        var outputDir = Path.GetDirectoryName(outputFull);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(outputFull))
            File.Delete(outputFull);

        ZipFile.CreateFromDirectory(inputFull, outputFull);
    }
}
