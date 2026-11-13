using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using BlModTool.Cli;
using Xunit;

namespace BlModTool.Cli.Tests;

public class WarbandPortTests
{
    private const string NativeDir = "/home/ou/Projects/re-mb-1011/Modules/Native";
    private const string ToolName = "BlModTool-test";

    private static WarbandModule ReadModule()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        return WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath);
    }

    private static string TempOutputDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "m4-port-test-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Convert_default_mode_counts()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(433, c.ItemsRead);
            Assert.Equal(420, c.ItemsConverted);
            Assert.Equal(13, c.ItemsSkipped);
            Assert.Equal(619, c.TroopsRead);
            Assert.Equal(134, c.TroopsConverted);
            Assert.Equal(485, c.TroopsSkipped);
            Assert.Equal(18, c.CulturesGenerated);
            Assert.Equal(0, c.KingdomsGenerated);
            Assert.Equal(775, c.EquipmentEntries);
            Assert.Equal(59, c.UpgradeLinks);
            Assert.Equal(0, c.MissingReferences);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_include_heroes_counts()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { IncludeHeroes = true }, ToolName);
            var c = result.Report.Counts;

            Assert.Equal(613, c.TroopsConverted);
            Assert.Equal(6, c.TroopsSkipped);
            Assert.Equal(2395, c.EquipmentEntries);
            Assert.Equal(13, c.MissingReferences); // 书店兵种引用被跳过的书籍
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_emit_kingdoms_counts()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { EmitKingdoms = true }, ToolName);
            var c = result.Report.Counts;

            Assert.Equal(5, c.KingdomsGenerated);
            Assert.NotNull(result.KingdomsFile);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_xml_has_no_bad_references()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);

            var items = LoadAllItems(result.ItemsFile);
            var npcs = XDocument.Load(result.NpcFile).Root!;
            var cults = XDocument.Load(result.CulturesFile).Root!;

            var itemIds = new HashSet<string>(items.Elements("Item").Select(e => e.Attribute("id")!.Value));
            var npcIds = new HashSet<string>(npcs.Elements("NPCCharacter").Select(e => e.Attribute("id")!.Value));
            var cultureIds = new HashSet<string>(cults.Elements("Culture").Select(e => e.Attribute("id")!.Value))
            { "neutral_culture" };

            int badEq = 0, badUp = 0, badCult = 0;
            foreach (var npc in npcs.Elements("NPCCharacter"))
            {
                foreach (var eq in npc.Elements("equipment"))
                {
                    var refId = eq.Attribute("id")!.Value;
                    if (!refId.StartsWith("Item.") || !itemIds.Contains(refId["Item.".Length..]))
                        badEq++;
                }
                foreach (var up in npc.Elements("upgrade_target"))
                {
                    var refId = up.Attribute("id")!.Value;
                    if (!refId.StartsWith("NPCCharacter.") || !npcIds.Contains(refId["NPCCharacter.".Length..]))
                        badUp++;
                }
                var cult = npc.Attribute("culture")!.Value;
                if (!cult.StartsWith("Culture.") || !cultureIds.Contains(cult["Culture.".Length..]))
                    badCult++;
            }

            Assert.Equal(0, badEq);
            Assert.Equal(0, badUp);
            Assert.Equal(0, badCult);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_cultures_have_basic_and_elite_troop()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var cults = XDocument.Load(result.CulturesFile).Root!;

            foreach (var cult in cults.Elements("Culture"))
            {
                var hasTroops = cult.Attribute("basic_troop") is not null;
                if (hasTroops)
                {
                    Assert.NotNull(cult.Attribute("elite_basic_troop"));
                    Assert.StartsWith("NPCCharacter.", cult.Attribute("basic_troop")!.Value);
                    Assert.StartsWith("NPCCharacter.", cult.Attribute("elite_basic_troop")!.Value);
                }
            }

            // 主文化都应有基础兵种
            var mainCultures = cults.Elements("Culture")
                .Where(c => c.Attribute("is_main_culture")?.Value == "true");
            foreach (var c in mainCultures)
                Assert.NotNull(c.Attribute("basic_troop"));
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_animal_no_subtype_horse_has_subtype()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var items = LoadAllItems(result.ItemsFile);

            foreach (var item in items.Elements("Item"))
            {
                var type = item.Attribute("Type")!.Value;
                var subtype = item.Attribute("subtype");
                if (type == "Animal")
                    Assert.Null(subtype);
                else if (type == "Horse")
                    Assert.Equal("horse", subtype?.Value);
            }
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_skills_written_and_referenced()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(42, c.SkillsRead);
            Assert.Equal(42, c.SkillsConverted);
            Assert.Equal(0, c.SkillsSkipped);
            Assert.NotNull(result.SkillsFile);

            var skills = XDocument.Load(result.SkillsFile!).Root!;
            Assert.Equal(42, skills.Elements("Skill").Count());

            var trade = skills.Elements("Skill").First(s => s.Attribute("id")!.Value == "wb_trade");
            Assert.Equal("Charisma", trade.Attribute("base_attribute")!.Value);
            Assert.Equal("true", trade.Attribute("is_party_skill")!.Value);
            Assert.StartsWith("{=wb_skill_", trade.Attribute("name")!.Value);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_skills()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(System.IO.Path.Combine(outDir, "SubModule.xml")).Root!;
            var skillsNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "Skills");
            Assert.NotNull(skillsNode);
            Assert.Equal("skills", skillsNode.Element("XmlName")?.Attribute("path")?.Value);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    private static XElement LoadAllItems(string itemsDir)
    {
        var root = new XElement("Items");
        if (!System.IO.Directory.Exists(itemsDir))
            return root;

        foreach (var file in System.IO.Directory.GetFiles(itemsDir, "*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                foreach (var item in doc.Root!.Elements("Item"))
                    root.Add(item);
            }
            catch
            {
                // ignore malformed category files
            }
        }

        return root;
    }

    [Fact]
    public void Convert_items_split_into_category_files()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var itemsDir = result.ItemsFile;

            Assert.True(System.IO.Directory.Exists(itemsDir), "items directory should exist");

            var files = System.IO.Directory.GetFiles(itemsDir, "*.xml");
            Assert.NotEmpty(files);

            // Verify each category file is valid XML and contains Items
            int totalItems = 0;
            foreach (var file in files)
            {
                var doc = XDocument.Load(file);
                var items = doc.Root!.Elements("Item");
                totalItems += items.Count();
            }

            Assert.Equal(420, totalItems);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_items_category_files_match_types()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var itemsDir = result.ItemsFile;

            var expected = new Dictionary<string, string[]>
            {
                { "weapons.xml", new[] { "OneHandedWeapon", "TwoHandedWeapon", "Bow", "Crossbow", "Thrown", "Polearm", "Arrow", "Bolt", "Arrows", "Bolts" } },
                { "shields.xml", new[] { "Shield" } },
                { "body_armors.xml", new[] { "BodyArmor" } },
                { "head_armors.xml", new[] { "HeadArmor" } },
                { "leg_armors.xml", new[] { "FootArmor", "LegArmor" } },
                { "shoulder_armors.xml", new[] { "HandArmor" } },
                { "horses_and_others.xml", new[] { "Horse", "Animal", "Goods" } }
            };

            foreach (var (fileName, expectedTypes) in expected)
            {
                var path = Path.Combine(itemsDir, fileName);
                Assert.True(File.Exists(path), $"category file {fileName} should exist");

                var doc = XDocument.Load(path);
                foreach (var item in doc.Root!.Elements("Item"))
                {
                    var type = item.Attribute("Type")!.Value;
                    Assert.True(expectedTypes.Contains(type),
                        $"item {item.Attribute("id")!.Value} has unexpected type {type} in {fileName}");
                }
            }

            // Empty placeholder files should exist but contain no items
            foreach (var emptyFile in new[] { "banners.xml", "tournament_weapons.xml", "arm_armors.xml" })
            {
                var path = Path.Combine(itemsDir, emptyFile);
                Assert.True(File.Exists(path), $"empty placeholder {emptyFile} should exist");
                var doc = XDocument.Load(path);
                Assert.Empty(doc.Root!.Elements("Item"));
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_npc_roles_generated_per_culture()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var npcs = XDocument.Load(result.NpcFile).Root!;

            var roleNpcs = npcs.Elements("NPCCharacter")
                .Where(n => n.Attribute("occupation")?.Value == "Civilian")
                .ToList();

            // 18 cultures × 14 roles = 252 role NPCs
            Assert.Equal(252, roleNpcs.Count);

            var roleIds = new[] { "villager", "caravan_master", "guard", "blacksmith",
                "weaponsmith", "tavernkeeper", "townswoman", "townsman", "barber",
                "merchant", "prison_guard", "ransom_broker", "tournament_master", "musician" };

            foreach (var roleId in roleIds)
            {
                var count = roleNpcs.Count(n => n.Attribute("id")!.Value.StartsWith("wb_" + roleId + "_"));
                Assert.True(count == 18, $"role {roleId} should exist for all 18 cultures");
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_cultures_have_npc_role_references()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var cults = XDocument.Load(result.CulturesFile).Root!;
            var npcs = XDocument.Load(result.NpcFile).Root!;

            var npcIds = new HashSet<string>(npcs.Elements("NPCCharacter")
                .Select(n => n.Attribute("id")!.Value));

            var roleAttrs = new[] { "villager", "caravan_master", "guard", "blacksmith",
                "weaponsmith", "tavernkeeper", "townswoman", "townsman", "barber",
                "merchant", "prison_guard", "ransom_broker", "tournament_master", "musician" };

            foreach (var cult in cults.Elements("Culture"))
            {
                foreach (var attr in roleAttrs)
                {
                    var refId = cult.Attribute(attr)?.Value;
                    Assert.NotNull(refId);
                    Assert.True(refId!.StartsWith("NPCCharacter."), $"culture {cult.Attribute("id")!.Value} {attr} should reference NPCCharacter");
                    var npcId = refId!["NPCCharacter.".Length..];
                    Assert.True(npcIds.Contains(npcId), $"referenced NPC {npcId} does not exist");
                }
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_all_xml_nodes()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(Path.Combine(outDir, "SubModule.xml")).Root!;
            var xmlNodes = sub.Descendants("XmlNode").Select(n =>
                n.Element("XmlName")!.Attribute("id")!.Value).ToHashSet();

            Assert.Contains("Items", xmlNodes);
            Assert.Contains("SPCultures", xmlNodes);
            Assert.Contains("NPCCharacters", xmlNodes);
            Assert.Contains("Skills", xmlNodes);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_without_skills_when_no_skills()
    {
        var module = ReadModule();
        module.Skills.Clear(); // simulate no skills
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(Path.Combine(outDir, "SubModule.xml")).Root!;
            var xmlNodes = sub.Descendants("XmlNode").Select(n =>
                n.Element("XmlName")!.Attribute("id")!.Value).ToHashSet();

            Assert.DoesNotContain("Skills", xmlNodes);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_skills_linked_from_npcs()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);

            var skillsDoc = XDocument.Load(result.SkillsFile!);
            var skillIds = new HashSet<string>(skillsDoc.Root!.Elements("Skill")
                .Select(s => s.Attribute("id")!.Value));

            // All 42 skills.txt-defined skills must appear in skills.xml
            foreach (var skill in module.Skills)
            {
                var expectedId = "wb_" + skill.Id.Replace("skl_", "", StringComparison.Ordinal);
                Assert.True(skillIds.Contains(expectedId),
                    $"skill {skill.Id} -> {expectedId} missing from skills.xml");
            }

            // NPC-referenced skill IDs must be valid Bannerlord skill names
            var npcs = XDocument.Load(result.NpcFile).Root!;
            var validBannerlordSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "onehanded", "twohanded", "polearm", "bow", "crossbow", "throwing", "riding",
                "athletics", "tactics", "scouting", "trade", "leadership", "engineering",
                "medicine", "steward", "roguery"
            };

            foreach (var npc in npcs.Elements("NPCCharacter"))
            {
                var skillsElem = npc.Element("skills");
                if (skillsElem == null)
                    continue;
                foreach (var skill in skillsElem.Elements("skill"))
                {
                    var skillId = skill.Attribute("id")!.Value.ToLowerInvariant();
                    Assert.True(validBannerlordSkills.Contains(skillId),
                        $"NPC {npc.Attribute("id")!.Value} references unknown skill {skillId}");
                }
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_report_items_per_category_counts()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var cats = result.Report.Counts.ItemsPerCategory;

            Assert.Equal(10, cats.Count);
            Assert.Equal(420, cats.Values.Sum());

            // Verify directory files match report counts
            var dir = result.ItemsFile;
            var files = Directory.GetFiles(dir, "*.xml");
            Assert.Equal(10, files.Length);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var doc = XDocument.Load(file);
                var count = doc.Root!.Elements("Item").Count();
                Assert.True(cats.TryGetValue(fileName.Replace(".xml", ""), out var expected),
                    $"report missing category {fileName}");
                Assert.Equal(expected, count);
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_readme_has_category_summary()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateReadme = true }, ToolName);

            var readmePath = Path.Combine(outDir, "README.md");
            Assert.True(File.Exists(readmePath), "README.md should be generated");

            var readme = File.ReadAllText(readmePath);
            Assert.Contains("items/weapons.xml", readme);
            Assert.Contains("items/body_armors.xml", readme);
            Assert.Contains("NPC role templates", readme);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_deterministic_output()
    {
        var module = ReadModule();
        var dir1 = TempOutputDir();
        var dir2 = TempOutputDir();
        try
        {
            var r1 = WarbandPort.Convert(module, dir1, new WarbandPortOptions(), ToolName);
            var r2 = WarbandPort.Convert(module, dir2, new WarbandPortOptions(), ToolName);

            // Item counts should match
            Assert.Equal(r1.Report.Counts.ItemsConverted, r2.Report.Counts.ItemsConverted);
            Assert.Equal(r1.Report.Counts.TroopsConverted, r2.Report.Counts.TroopsConverted);
            Assert.Equal(r1.Report.Counts.NpcRolesGenerated, r2.Report.Counts.NpcRolesGenerated);

            // NPC XML should be identical (except GeneratedAtUtc in report)
            var xml1 = File.ReadAllText(r1.NpcFile);
            var xml2 = File.ReadAllText(r2.NpcFile);
            Assert.Equal(xml1, xml2);
        }
        finally
        {
            Directory.Delete(dir1, recursive: true);
            Directory.Delete(dir2, recursive: true);
        }
    }

    [Fact]
    public void Convert_party_templates_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partyTemplatesPath = System.IO.Path.Combine(NativeDir, "party_templates.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, partyTemplatesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(48, c.PartyTemplatesRead);
            Assert.True(c.PartyTemplatesConverted > 0);
            Assert.NotNull(result.PartyTemplatesFile);

            var doc = XDocument.Load(result.PartyTemplatesFile!);
            var templates = doc.Root!.Elements("MBPartyTemplate").ToList();
            Assert.True(templates.Count == c.PartyTemplatesConverted);

            // Looters template should exist with 1 stack
            var looters = templates.FirstOrDefault(t => t.Attribute("id")!.Value == "pt_looters");
            Assert.NotNull(looters);
            var stacks = looters.Element("stacks")!.Elements("PartyTemplateStack").ToList();
            Assert.Single(stacks);
            Assert.Equal("NPCCharacter.wb_looter", stacks.First().Attribute("troop")!.Value);
            Assert.Equal("4", stacks.First().Attribute("min_value")!.Value);
            Assert.Equal("25", stacks.First().Attribute("max_value")!.Value);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_party_templates()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partyTemplatesPath = System.IO.Path.Combine(NativeDir, "party_templates.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, partyTemplatesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(System.IO.Path.Combine(outDir, "SubModule.xml")).Root!;
            var ptNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "PartyTemplates");
            Assert.NotNull(ptNode);
            Assert.Equal("party_templates", ptNode.Element("XmlName")?.Attribute("path")?.Value);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_settlements_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partiesPath = System.IO.Path.Combine(NativeDir, "parties.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, partiesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(148, c.SettlementsRead);
            Assert.Equal(148, c.SettlementsConverted);
            Assert.Equal(0, c.SettlementsSkipped);
            Assert.NotNull(result.SettlementsFile);

            var doc = XDocument.Load(result.SettlementsFile!);
            var settlements = doc.Root!.Elements("Settlement").ToList();
            Assert.Equal(148, settlements.Count);

            // Verify Sargoth town
            var sargoth = settlements.FirstOrDefault(s => s.Attribute("id")!.Value == "wb_town_1");
            Assert.NotNull(sargoth);
            Assert.Equal("Sargoth", sargoth.Attribute("name")!.Value.Replace("{=wb_settlement_p_town_1}", ""));
            Assert.Equal("-1.55", sargoth.Attribute("posX")!.Value);
            Assert.Equal("66.45", sargoth.Attribute("posY")!.Value);
            Assert.Equal("Culture.wb_neutral", sargoth.Attribute("culture")!.Value);

            // Verify town component
            var town = sargoth.Element("Components")!.Element("Town");
            Assert.NotNull(town);
            Assert.Equal("false", town.Attribute("is_castle")!.Value);

            // Verify a castle
            var culmarr = settlements.FirstOrDefault(s => s.Attribute("id")!.Value == "wb_castle_1");
            Assert.NotNull(culmarr);
            var castle = culmarr.Element("Components")!.Element("Town");
            Assert.NotNull(castle);
            Assert.Equal("true", castle.Attribute("is_castle")!.Value);

            // Verify a village
            var yaragar = settlements.FirstOrDefault(s => s.Attribute("id")!.Value == "wb_village_1");
            Assert.NotNull(yaragar);
            var village = yaragar.Element("Components")!.Element("Village");
            Assert.NotNull(village);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_settlements()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partiesPath = System.IO.Path.Combine(NativeDir, "parties.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, partiesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(System.IO.Path.Combine(outDir, "SubModule.xml")).Root!;
            var settlementsNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "Settlements");
            Assert.NotNull(settlementsNode);
            Assert.Equal("settlements", settlementsNode.Element("XmlName")?.Attribute("path")!.Value);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_quests_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var questsPath = System.IO.Path.Combine(NativeDir, "quests.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, questsPath, null, null);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(41, c.QuestsRead);
            Assert.Equal(41, c.QuestsConverted);
            Assert.Equal(0, c.QuestsSkipped);
            Assert.NotNull(result.QuestsFile);

            var doc = XDocument.Load(result.QuestsFile!);
            var quests = doc.Root!.Elements("Quest").ToList();
            Assert.Equal(41, quests.Count);

            var deliverMessage = quests.FirstOrDefault(q => q.Attribute("id")!.Value == "qst_deliver_message");
            Assert.NotNull(deliverMessage);
            Assert.Equal("2", deliverMessage.Attribute("xp_reward")!.Value);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_menus_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var menusPath = System.IO.Path.Combine(NativeDir, "menus.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, null, menusPath, null);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(183, c.MenusRead);
            Assert.Equal(183, c.MenusConverted);
            Assert.Equal(0, c.MenusSkipped);
            Assert.NotNull(result.MenusFile);

            var doc = XDocument.Load(result.MenusFile!);
            var menus = doc.Root!.Elements("Menu").ToList();
            Assert.Equal(183, menus.Count);

            var startGame = menus.FirstOrDefault(m => m.Attribute("id")!.Value == "menu_start_game_1");
            Assert.NotNull(startGame);
            Assert.Equal("none", startGame.Attribute("mesh")!.Value);
            Assert.True(startGame.Element("MenuOption") != null);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_dialog_states_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var dialogStatesPath = System.IO.Path.Combine(NativeDir, "dialog_states.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, null, null, dialogStatesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions(), ToolName);
            var c = result.Report.Counts;

            Assert.Equal(889, c.DialogStatesRead);
            Assert.Equal(889, c.DialogStatesConverted);
            Assert.Equal(0, c.DialogStatesSkipped);
            Assert.NotNull(result.DialogStatesFile);

            var doc = XDocument.Load(result.DialogStatesFile!);
            var states = doc.Root!.Elements("DialogState").ToList();
            Assert.Equal(889, states.Count);
            Assert.Contains(states, s => s.Attribute("id")!.Value == "start");
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_quests_menus_dialog_states()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var questsPath = System.IO.Path.Combine(NativeDir, "quests.txt");
        var menusPath = System.IO.Path.Combine(NativeDir, "menus.txt");
        var dialogStatesPath = System.IO.Path.Combine(NativeDir, "dialog_states.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, questsPath, menusPath, dialogStatesPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var sub = XDocument.Load(System.IO.Path.Combine(outDir, "SubModule.xml")).Root!;
            var questsNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "Quests");
            Assert.NotNull(questsNode);
            Assert.Equal("quests", questsNode.Element("XmlName")?.Attribute("path")!.Value);

            var menusNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "Menus");
            Assert.NotNull(menusNode);
            Assert.Equal("menus", menusNode.Element("XmlName")?.Attribute("path")!.Value);

            var dialogStatesNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "DialogStates");
            Assert.NotNull(dialogStatesNode);
            Assert.Equal("dialog_states", dialogStatesNode.Element("XmlName")?.Attribute("path")!.Value);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_scripts_written()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var scriptsPath = System.IO.Path.Combine(NativeDir, "scripts.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, null, null, null, scriptsPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir, new WarbandPortOptions { GenerateScripts = true }, ToolName);
            var c = result.Report.Counts;

            Assert.Equal(354, c.ScriptsRead);
            Assert.Equal(354, c.ScriptsConverted);
            Assert.Equal(0, c.ScriptsSkipped);
            Assert.NotNull(result.ScriptsFile);

            var scriptText = System.IO.File.ReadAllText(result.ScriptsFile!);
            Assert.Contains("public static class WarbandScripts", scriptText);
            Assert.Contains("public static class Opcodes", scriptText);
            Assert.Contains("public static int[] game_start()", scriptText);
            Assert.Contains("public static int[] game_event_party_encounter()", scriptText);

            // 每个脚本都应该有一个对应的方法
            var methodCount = System.Text.RegularExpressions.Regex.Matches(scriptText, @"public static int\[\] \w+\(\)").Count;
            Assert.Equal(354, methodCount);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_submodule_registers_scripts()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var scriptsPath = System.IO.Path.Combine(NativeDir, "scripts.txt");
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, null, null, null, scriptsPath);

        var outDir = TempOutputDir();
        try
        {
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateScripts = true, GenerateSubModule = true }, ToolName);

            var sub = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(outDir, "SubModule.xml")).Root!;
            var scriptsNode = sub.Descendants("XmlNode")
                .FirstOrDefault(n => n.Element("XmlName")?.Attribute("id")?.Value == "Scripts");
            Assert.NotNull(scriptsNode);
            Assert.Equal("scripts", scriptsNode.Element("XmlName")?.Attribute("path")!.Value);
        }
        finally
        {
            System.IO.Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_package_creates_zip_archive()
    {
        var module = ReadModule();
        var outDir = TempOutputDir();
        try
        {
            // 先生成一些模块输出文件（带 SubModule 以包含 ModuleInfo/SubModule）
            var result = WarbandPort.Convert(module, outDir,
                new WarbandPortOptions { GenerateSubModule = true }, ToolName);

            var zipPath = Path.Combine(System.IO.Path.GetTempPath(), $"warband-package-test-{System.Guid.NewGuid():N}.zip");
            try
            {
                WarbandPort.PackageModule(outDir, zipPath);

                Assert.True(File.Exists(zipPath), "zip 文件应该被创建");
                var info = new FileInfo(zipPath);
                Assert.True(info.Length > 0, "zip 文件不应为空");

                // 验证 zip 包含预期文件
                using var archive = ZipFile.OpenRead(zipPath);
                var entryNames = archive.Entries.Select(e => e.FullName).ToHashSet();
                Assert.Contains("ModuleData/spnpccharacters.xml", entryNames);
                Assert.Contains("ModuleData/spcultures.xml", entryNames);
                Assert.Contains("ModuleInfo.xml", entryNames);
                Assert.Contains("SubModule.xml", entryNames);
                Assert.Contains("Languages/en/strings.xml", entryNames);
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Convert_package_throws_on_missing_directory()
    {
        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            WarbandPort.PackageModule("/nonexistent/path/that/does/not/exist", "/tmp/out.zip"));
        Assert.Contains("模块目录不存在", ex.Message);
    }
}
