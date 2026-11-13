using System.Xml.Linq;
using BlModTool.Cli;
using Xunit;

namespace BlModTool.Cli.Tests;

public class WarbandReaderTests
{
    private const string NativeDir = "/home/ou/Projects/re-mb-1011/Modules/Native";

    [Fact]
    public void ReadModule_returns_expected_counts()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath);

        Assert.Equal(433, module.Items.Count);
        Assert.Equal(619, module.Troops.Count);
        Assert.Equal(31, module.Factions.Count);
    }

    [Fact]
    public void ReadItems_has_expected_skippable_items()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var items = WarbandReader.ReadItems(itemsPath);

        Assert.Contains(items, i => i.Id == "itm_no_item");
        Assert.Contains(items, i => i.Id == "itm_cartridges");
        Assert.Contains(items, i => i.Id == "itm_flintlock_pistol");
        // 10 本书
        var books = items.Count(i => i.Id.StartsWith("itm_book_", System.StringComparison.Ordinal));
        Assert.Equal(10, books);
    }

    [Fact]
    public void ReadTroops_has_template_troops()
    {
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var troops = WarbandReader.ReadTroops(troopsPath);

        Assert.Contains(troops, t => t.Id == "trp_player");
        Assert.Contains(troops, t => t.Id == "trp_temp_troop");
        Assert.Contains(troops, t => t.Id == "trp_game");
    }

    [Fact]
    public void ReadFactions_has_five_kingdoms()
    {
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var factions = WarbandReader.ReadFactions(factionsPath);

        var kingdoms = factions.Count(f => f.Id.StartsWith("fac_kingdom_", System.StringComparison.Ordinal));
        Assert.Equal(5, kingdoms);
    }

    [Fact]
    public void ReadSkills_parses_all_42_skills()
    {
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var skills = WarbandReader.ReadSkills(skillsPath);

        Assert.Equal(42, skills.Count);
        Assert.Contains(skills, s => s.Id == "skl_trade");
        Assert.Contains(skills, s => s.Id == "skl_leadership");
        Assert.Contains(skills, s => s.Id == "skl_power_draw");
        Assert.Contains(skills, s => s.Id == "skl_ironflesh");

        var trade = Assert.Single(skills, s => s.Id == "skl_trade");
        Assert.Equal("Trade", trade.Name);
        Assert.True(trade.IsPartySkill);
        Assert.False(trade.IsInactive);
        Assert.Equal("Charisma", trade.BaseAttributeName);
        Assert.Equal(10, trade.MaxLevel);

        var powerDraw = Assert.Single(skills, s => s.Id == "skl_power_draw");
        Assert.Equal("Strength", powerDraw.BaseAttributeName);
        Assert.False(powerDraw.IsPartySkill);

        var reserved = Assert.Single(skills, s => s.Id == "skl_reserved_1");
        Assert.True(reserved.IsInactive);
        // reserved_1 has base_attribute = 259 (0x0FF + 0x10), low 4 bits = 3 (Charisma)
        Assert.Equal("Charisma", reserved.BaseAttributeName);
    }

    [Fact]
    public void ReadModule_with_skills_loads_skills()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath);

        Assert.Equal(42, module.Skills.Count);
        Assert.Equal(skillsPath, module.SkillsPath);
    }

    [Fact]
    public void ReadPartyTemplates_parses_all_48_templates()
    {
        var partyTemplatesPath = System.IO.Path.Combine(NativeDir, "party_templates.txt");
        var templates = WarbandReader.ReadPartyTemplates(partyTemplatesPath);

        Assert.Equal(48, templates.Count);

        // Looters: 1 stack, troop index 98, min 4, max 25
        var looters = Assert.Single(templates, t => t.Id == "pt_looters");
        Assert.Equal("Looters", looters.Name);
        Assert.Single(looters.Stacks);
        Assert.Equal(98, looters.Stacks[0].TroopIndex);
        Assert.Equal(4, looters.Stacks[0].Min);
        Assert.Equal(25, looters.Stacks[0].Max);

        // Village defenders: 2 stacks
        var villageDefenders = Assert.Single(templates, t => t.Id == "pt_village_defenders");
        Assert.Equal(2, villageDefenders.Stacks.Count);
        Assert.Equal(24, villageDefenders.Stacks[0].TroopIndex);
        Assert.Equal(10, villageDefenders.Stacks[0].Min);
        Assert.Equal(20, villageDefenders.Stacks[0].Max);
        Assert.Equal(116, villageDefenders.Stacks[1].TroopIndex);
        Assert.Equal(0, villageDefenders.Stacks[1].Min);
        Assert.Equal(4, villageDefenders.Stacks[1].Max);

        // Merchant caravan: 2 stacks
        var caravan = Assert.Single(templates, t => t.Id == "pt_merchant_caravan");
        Assert.Equal(2, caravan.Stacks.Count);
        Assert.Equal(117, caravan.Stacks[0].TroopIndex);
        Assert.Equal(1, caravan.Stacks[0].Min);
        Assert.Equal(1, caravan.Stacks[0].Max);
        Assert.Equal(27, caravan.Stacks[1].TroopIndex);
        Assert.Equal(5, caravan.Stacks[1].Min);
        Assert.Equal(25, caravan.Stacks[1].Max);
    }

    [Fact]
    public void ReadModule_with_party_templates_loads_templates()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partyTemplatesPath = System.IO.Path.Combine(NativeDir, "party_templates.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, partyTemplatesPath);

        Assert.Equal(48, module.PartyTemplates.Count);
        Assert.Equal(partyTemplatesPath, module.PartyTemplatesPath);
    }

    [Fact]
    public void ReadParties_parses_all_200_parties()
    {
        var partiesPath = System.IO.Path.Combine(NativeDir, "parties.txt");
        var parties = WarbandReader.ReadParties(partiesPath);

        Assert.Equal(200, parties.Count);

        // Sargoth town: index 19, faction 3, position (-1.55, 66.45)
        var sargoth = Assert.Single(parties, p => p.Id == "p_town_1");
        Assert.Equal("Sargoth", sargoth.Name);
        Assert.True(sargoth.IsTown);
        Assert.False(sargoth.IsCastle);
        Assert.False(sargoth.IsVillage);
        Assert.Equal(-1.55, sargoth.X, 2);
        Assert.Equal(66.45, sargoth.Y, 2);
        Assert.Equal(3, sargoth.FactionIndex);

        // Culmarr Castle: index 37
        var culmarr = Assert.Single(parties, p => p.Id == "p_castle_1");
        Assert.Equal("Culmarr_Castle", culmarr.Name);
        Assert.True(culmarr.IsCastle);
        Assert.Equal(-69.2, culmarr.X, 2);
        Assert.Equal(31.3, culmarr.Y, 2);

        // Yaragar village: index 77
        var yaragar = Assert.Single(parties, p => p.Id == "p_village_1");
        Assert.Equal("Yaragar", yaragar.Name);
        Assert.True(yaragar.IsVillage);
        Assert.Equal(-15.0, yaragar.X, 2);
        Assert.Equal(0.0, yaragar.Y, 2);
    }

    [Fact]
    public void ReadModule_with_parties_loads_parties()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var partiesPath = System.IO.Path.Combine(NativeDir, "parties.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, partiesPath);

        Assert.Equal(200, module.Parties.Count);
        Assert.Equal(148, module.Parties.Count(p => p.IsSettlement));
        Assert.Equal(partiesPath, module.PartiesPath);
    }

    [Fact]
    public void ReadQuests_parses_all_41_quests()
    {
        var questsPath = System.IO.Path.Combine(NativeDir, "quests.txt");
        var quests = WarbandReader.ReadQuests(questsPath);

        Assert.Equal(41, quests.Count);

        var deliverMessage = Assert.Single(quests, q => q.Id == "qst_deliver_message");
        Assert.Equal("Deliver_Message_to_{s13}", deliverMessage.Name);
        Assert.Equal(2, deliverMessage.XpReward);
        Assert.Contains("asked_you_to_take_a_message_to_{s13}", deliverMessage.Description);
    }

    [Fact]
    public void ReadMenus_parses_all_183_menus()
    {
        var menusPath = System.IO.Path.Combine(NativeDir, "menus.txt");
        var menus = WarbandReader.ReadMenus(menusPath);

        Assert.Equal(183, menus.Count);

        var startGame = Assert.Single(menus, m => m.Id == "menu_start_game_1");
        Assert.Equal("Welcome,_adventurer,_to_Mount&Blade._Before_you_can_start_playing_the_game_you_must_create_a_character._To_begin,_select_your_character's_gender.", startGame.Text);
        Assert.Equal("none", startGame.Mesh);
        Assert.True(startGame.Options.Count > 0);
    }

    [Fact]
    public void ReadDialogStates_parses_all_889_states()
    {
        var dialogStatesPath = System.IO.Path.Combine(NativeDir, "dialog_states.txt");
        var states = WarbandReader.ReadDialogStates(dialogStatesPath);

        Assert.Equal(889, states.Count);
        Assert.Contains(states, s => s.Id == "start");
        Assert.Contains(states, s => s.Id == "party_encounter");
        Assert.Contains(states, s => s.Id == "trade");
    }

    [Fact]
    public void ReadScripts_parses_all_354_scripts()
    {
        var scriptsPath = System.IO.Path.Combine(NativeDir, "scripts.txt");
        var scripts = WarbandReader.ReadScripts(scriptsPath);

        Assert.Equal(354, scripts.Count);

        var gameStart = Assert.Single(scripts, s => s.Id == "game_start");
        Assert.Equal(-1, gameStart.ParameterCount);
        Assert.True(gameStart.Operations.Count > 0);

        var gameEncounter = Assert.Single(scripts, s => s.Id == "game_event_party_encounter");
        Assert.True(gameEncounter.Operations.Count > 0);

        // 第一个操作码应该是非负整数
        Assert.True(gameStart.Operations[0] >= 0);
    }

    [Fact]
    public void ReadModule_with_scripts_loads_scripts()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var scriptsPath = System.IO.Path.Combine(NativeDir, "scripts.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, scriptsPath: scriptsPath);

        Assert.Equal(354, module.Scripts.Count);
        Assert.Equal(scriptsPath, module.ScriptsPath);
    }

    [Fact]
    public void ReadModule_with_quests_menus_dialog_states()
    {
        var itemsPath = System.IO.Path.Combine(NativeDir, "item_kinds1.txt");
        var troopsPath = System.IO.Path.Combine(NativeDir, "troops.txt");
        var factionsPath = System.IO.Path.Combine(NativeDir, "factions.txt");
        var skillsPath = System.IO.Path.Combine(NativeDir, "skills.txt");
        var questsPath = System.IO.Path.Combine(NativeDir, "quests.txt");
        var menusPath = System.IO.Path.Combine(NativeDir, "menus.txt");
        var dialogStatesPath = System.IO.Path.Combine(NativeDir, "dialog_states.txt");

        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath, null, null, questsPath, menusPath, dialogStatesPath);

        Assert.Equal(41, module.Quests.Count);
        Assert.Equal(183, module.Menus.Count);
        Assert.Equal(889, module.DialogStates.Count);
        Assert.Equal(questsPath, module.QuestsPath);
        Assert.Equal(menusPath, module.MenusPath);
        Assert.Equal(dialogStatesPath, module.DialogStatesPath);
    }
}
