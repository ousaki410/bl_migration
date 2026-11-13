using System.Globalization;
using System.Text;

namespace BlModTool.Cli;

public static class WarbandReader
{
    public static WarbandModule ReadModule(string itemsPath, string troopsPath, string factionsPath, string? skillsPath = null, string? partyTemplatesPath = null, string? partiesPath = null, string? questsPath = null, string? menusPath = null, string? dialogStatesPath = null, string? scriptsPath = null)
    {
        var module = new WarbandModule
        {
            ItemsPath = itemsPath,
            TroopsPath = troopsPath,
            FactionsPath = factionsPath,
            SkillsPath = skillsPath,
            PartyTemplatesPath = partyTemplatesPath,
            PartiesPath = partiesPath,
            QuestsPath = questsPath,
            MenusPath = menusPath,
            DialogStatesPath = dialogStatesPath,
            ScriptsPath = scriptsPath
        };

        module.Items.AddRange(ReadItems(itemsPath));
        module.Troops.AddRange(ReadTroops(troopsPath));
        module.Factions.AddRange(ReadFactions(factionsPath));

        if (!string.IsNullOrEmpty(skillsPath))
        {
            module.Skills.AddRange(ReadSkills(skillsPath));
        }

        if (!string.IsNullOrEmpty(partyTemplatesPath))
        {
            module.PartyTemplates.AddRange(ReadPartyTemplates(partyTemplatesPath));
        }

        if (!string.IsNullOrEmpty(partiesPath))
        {
            module.Parties.AddRange(ReadParties(partiesPath));
        }

        if (!string.IsNullOrEmpty(questsPath))
        {
            module.Quests.AddRange(ReadQuests(questsPath));
        }

        if (!string.IsNullOrEmpty(menusPath))
        {
            module.Menus.AddRange(ReadMenus(menusPath));
        }

        if (!string.IsNullOrEmpty(dialogStatesPath))
        {
            module.DialogStates.AddRange(ReadDialogStates(dialogStatesPath));
        }

        if (!string.IsNullOrEmpty(scriptsPath))
        {
            module.Scripts.AddRange(ReadScripts(scriptsPath));
        }

        return module;
    }

    public static IReadOnlyList<WarbandPartyTemplate> ReadPartyTemplates(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 2 || header[0] != "partytemplatesfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 partytemplatesfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行模板数量");
        var templates = new List<WarbandPartyTemplate>(expectedCount);

        for (var lineIndex = 2; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
                continue;

            var tokens = SplitTokens(line);
            if (tokens.Length < 6)
                throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板字段不足。");

            var template = new WarbandPartyTemplate
            {
                Index = templates.Count,
                Id = tokens[0],
                Name = tokens[1],
                Flags = (long)ParseULong(tokens[2], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板标志"),
                Field3 = ParseInt(tokens[3], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板字段3"),
                Behavior = ParseInt(tokens[4], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板行为"),
                FactionIndex = ParseInt(tokens[5], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板阵营"),
            };

            // 每个 troop stack 占 4 个 token：troop_id, min, max, probability；共 6 个 slots
            var position = 6;
            for (var stackIndex = 0; stackIndex < 6; stackIndex++)
            {
                if (position + 3 >= tokens.Length)
                    break;

                var troopIndex = ParseInt(tokens[position], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板第 {stackIndex + 1} 个 stack 的 troop 索引");
                if (troopIndex < 0)
                {
                    // -1 表示空 slot，填充其余
                    break;
                }

                var min = ParseInt(tokens[position + 1], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板第 {stackIndex + 1} 个 stack 的最小数量");
                var max = ParseInt(tokens[position + 2], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板第 {stackIndex + 1} 个 stack 的最大数量");
                var probability = position + 3 < tokens.Length ? ParseInt(tokens[position + 3], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行模板第 {stackIndex + 1} 个 stack 的概率") : 0;

                template.Stacks.Add(new WarbandPartyTemplateStack
                {
                    TroopIndex = troopIndex,
                    Min = min,
                    Max = max,
                    Probability = probability,
                });

                position += 4;
            }

            templates.Add(template);
        }

        if (templates.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 模板数量不匹配: 头部声明 {expectedCount}，实际解析 {templates.Count}。");
        }

        return templates;
    }

    public static IReadOnlyList<WarbandParty> ReadParties(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 2 || header[0] != "partiesfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 partiesfile version N。");

        var countHeader = SplitTokens(lines[1]);
        if (countHeader.Length < 1)
            throw new InvalidDataException($"{Path.GetFileName(path)} 第 2 行队伍数量缺失。");
        var expectedCount = ParseInt(countHeader[0], $"{Path.GetFileName(path)} 第 2 行队伍数量");
        var parties = new List<WarbandParty>(expectedCount);

        var lineIndex = 2;
        while (lineIndex < lines.Length && parties.Count < expectedCount)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                lineIndex++;
                continue;
            }

            var tokens = SplitTokens(line);
            // parties.txt format: 1 <index> <index> <id> <name> <flags> <menu_no>
            //   <ai_behavior> <faction> <ai_obj1> <ai_obj1> <ai_obj2> <ai_obj2>
            //   <loc_x> <loc_y> <loc_x> <loc_y> <pos_x> <pos_y> <pos_z>
            //   <member_count> <member_data...>
            //   <bearing> (on next line)
            if (tokens.Length < 22)
                throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍字段不足。");

            var party = new WarbandParty
            {
                Index = parties.Count,
                Id = tokens[3],
                Name = tokens[4],
                Flags = ParseUInt(tokens[5], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍标志"),
                MenuNo = ParseInt(tokens[6], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍菜单号"),
                AiBehavior = ParseInt(tokens[7], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍AI行为"),
                FactionIndex = ParseInt(tokens[8], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍阵营"),
                X = ParseDouble(tokens[18], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍X坐标"),
                Y = ParseDouble(tokens[19], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍Y坐标"),
            };

            // Parse members: member_count is at tokens[21], member data starts at tokens[22]
            var memberCount = ParseInt(tokens[21], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍成员数量");
            var memberPosition = 22;
            for (var m = 0; m < memberCount; m++)
            {
                if (memberPosition + 3 >= tokens.Length)
                    break;

                party.Members.Add(new WarbandPartyMember
                {
                    TroopIndex = ParseInt(tokens[memberPosition], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍成员 {m} 的 troop 索引"),
                    Count = ParseInt(tokens[memberPosition + 1], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍成员 {m} 的数量"),
                    Field3 = ParseInt(tokens[memberPosition + 2], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍成员 {m} 的字段3"),
                    Field4 = ParseInt(tokens[memberPosition + 3], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍成员 {m} 的字段4"),
                });
                memberPosition += 4;
            }

            lineIndex++;

            // Bearing is on the next line (separate line in parties.txt format)
            if (lineIndex < lines.Length)
            {
                var bearingLine = lines[lineIndex].Trim();
                if (bearingLine.Length > 0)
                {
                    try
                    {
                        party.Bearing = ParseDouble(bearingLine,
                            $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行队伍方位角");
                    }
                    catch
                    {
                        // bearing line might not be a valid number
                    }
                }
                lineIndex++;
            }

            parties.Add(party);
        }

        if (parties.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 队伍数量不匹配: 头部声明 {expectedCount}，实际解析 {parties.Count}。");
        }

        return parties;
    }

    public static IReadOnlyList<WarbandQuest> ReadQuests(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 2 || header[0] != "questsfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 questsfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行任务数量");
        var quests = new List<WarbandQuest>(expectedCount);

        for (var lineIndex = 2; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
                continue;

            var tokens = SplitTokens(line);
            if (tokens.Length < 4)
                throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行任务字段不足。");

            quests.Add(new WarbandQuest
            {
                Index = quests.Count,
                Id = tokens[0],
                Name = tokens[1],
                XpReward = ParseInt(tokens[2], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行任务经验奖励"),
                Description = string.Join(" ", tokens, 3, tokens.Length - 3),
            });
        }

        if (quests.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 任务数量不匹配: 头部声明 {expectedCount}，实际解析 {quests.Count}。");
        }

        return quests;
    }

    public static IReadOnlyList<WarbandMenu> ReadMenus(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 2 || header[0] != "menusfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 menusfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行菜单数量");
        var menus = new List<WarbandMenu>(expectedCount);

        // menus.txt 格式: 每个菜单占多行
        //   第一行: menu_id, flags, text, mesh, unk1, unk2
        //   后续行(缩进): option_id, option_flags, option_text, ops..., .  (多个选项可在同一行或跨行)
        WarbandMenu? currentMenu = null;

        for (var lineIndex = 2; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.Length == 0)
                continue;

            var tokens = SplitTokens(line);
            if (tokens.Length == 0)
                continue;

            // 新菜单行: 以 menu_ 开头
            if (tokens[0].StartsWith("menu_", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length < 6)
                    throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行菜单字段不足。");

                currentMenu = new WarbandMenu
                {
                    Id = tokens[0],
                    Flags = (long)ParseULong(tokens[1], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行菜单标志"),
                    Text = tokens[2],
                    Mesh = tokens[3],
                };
                menus.Add(currentMenu);
                continue;
            }

            // 选项行: 以 mno_ 开头
            if (currentMenu == null)
                continue;

            var position = 0;
            while (position < tokens.Length)
            {
                if (!tokens[position].StartsWith("mno_", StringComparison.OrdinalIgnoreCase))
                    break;

                if (position + 2 >= tokens.Length)
                    break;

                var option = new WarbandMenuOption
                {
                    Id = tokens[position],
                    Flags = (long)ParseULong(tokens[position + 1], $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行菜单选项标志"),
                    Text = tokens[position + 2],
                };

                position += 3;

                // 读取操作码直到遇到 . 或下一个 mno_ 选项
                while (position < tokens.Length)
                {
                    var peek = tokens[position];
                    if (peek == ".")
                    {
                        position++; // 跳过 .
                        break;
                    }
                    if (peek.StartsWith("mno_", StringComparison.OrdinalIgnoreCase))
                    {
                        break; // 下一个选项
                    }
                    if (int.TryParse(peek, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opCode))
                    {
                        option.Operations.Add(opCode);
                        position++;
                    }
                    else
                        break;
                }

                currentMenu.Options.Add(option);
            }
        }

        if (menus.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 菜单数量不匹配: 头部声明 {expectedCount}，实际解析 {menus.Count}。");
        }

        return menus;
    }

    public static IReadOnlyList<WarbandDialogState> ReadDialogStates(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length == 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要一行。");

        var states = new List<WarbandDialogState>();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
                continue;

            var tokens = SplitTokens(line);
            states.Add(new WarbandDialogState
            {
                Index = states.Count,
                Id = tokens[0],
            });
        }

        return states;
    }

    public static IReadOnlyList<WarbandScript> ReadScripts(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行头部。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 2 || header[0] != "scriptsfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 scriptsfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行脚本数量");
        var scripts = new List<WarbandScript>(expectedCount);

        // scripts.txt 格式: 每个脚本占两行
        //   第一行: script_name -1  (第二个 token 是固定值，不是参数数量)
        //   第二行: 操作码序列 (space-separated integers, 可能有前导空格)
        var lineIndex = 2;
        while (lineIndex < lines.Length)
        {
            var nameLine = lines[lineIndex].Trim();
            if (nameLine.Length == 0)
            {
                lineIndex++;
                continue;
            }

            var nameTokens = SplitTokens(nameLine);
            if (nameTokens.Length < 1)
            {
                lineIndex++;
                continue;
            }

            var scriptId = nameTokens[0];
            // 第二个 token (如果存在) 是固定值 -1，不是参数数量；脚本参数数量未知，记为 -1
            var parameterCount = -1;

            lineIndex++;
            // 跳过空行，找到操作码行
            while (lineIndex < lines.Length && lines[lineIndex].Trim().Length == 0)
                lineIndex++;

            if (lineIndex >= lines.Length)
                throw new InvalidDataException($"{Path.GetFileName(path)} 脚本 `{scriptId}` 缺少操作码行。");

            var opLine = lines[lineIndex].Trim();
            var opTokens = SplitTokens(opLine);

            var script = new WarbandScript
            {
                Index = scripts.Count,
                Id = scriptId,
                ParameterCount = parameterCount
            };

            foreach (var token in opTokens)
            {
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opCode))
                    script.Operations.Add(opCode);
            }

            scripts.Add(script);
            lineIndex++;
        }

        if (scripts.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 脚本数量不匹配: 头部声明 {expectedCount}，实际解析 {scripts.Count}。");
        }

        return scripts;
    }

    public static IReadOnlyList<WarbandSkill> ReadSkills(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行。");

        var expectedCount = ParseInt(lines[0], $"{Path.GetFileName(path)} 第 1 行技能数量");
        var skills = new List<WarbandSkill>(expectedCount);

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
                continue;

            var tokens = SplitTokens(line);
            if (tokens.Length < 5)
                throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行技能字段不足。");

            var baseAttribute = ParseInt(tokens[2],
                $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行技能基础属性");
            var maxLevel = ParseInt(tokens[3],
                $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行技能最大等级");

            var skill = new WarbandSkill
            {
                Index = skills.Count,
                Id = tokens[0],
                Name = tokens[1],
                BaseAttribute = baseAttribute,
                MaxLevel = maxLevel,
                Description = string.Join(" ", tokens, 4, tokens.Length - 4),
                IsPartySkill = (baseAttribute & 0x10) != 0,
                IsInactive = (baseAttribute & 0x100) != 0
            };

            skills.Add(skill);
        }

        if (skills.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 技能数量不匹配: 头部声明 {expectedCount}，实际解析 {skills.Count}。");
        }

        return skills;
    }

    public static IReadOnlyList<WarbandItem> ReadItems(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行头部。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 3 || header[0] != "itemsfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 itemsfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行物品数量");
        var items = new List<WarbandItem>(expectedCount);

        for (var lineIndex = 2; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (!line.StartsWith("itm_", StringComparison.Ordinal))
                continue;

            var tokens = SplitTokens(line);
            if (tokens.Length < 4)
                throw new InvalidDataException($"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品字段不足。");

            var variationCount = ParseInt(tokens[3],
                $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品变体数量");
            if (variationCount < 0 || variationCount > 64)
                throw new InvalidDataException(
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品变体数量异常: {variationCount}");

            var valueStart = 4 + variationCount * 2;
            const int valueCount = 17;
            if (tokens.Length < valueStart + valueCount)
                throw new InvalidDataException(
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品 {tokens[0]} 字段不足: " +
                    $"需要 {valueStart + valueCount} 个 token，实际 {tokens.Length} 个。");

            var item = new WarbandItem
            {
                Index = items.Count,
                Id = tokens[0],
                Name = tokens[1],
                Mesh = variationCount > 0 ? tokens[4] : "",
                TypeFlags = ParseUInt(tokens[valueStart],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品类型"),
                Capabilities = ParseULong(tokens[valueStart + 1],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品能力"),
                Value = ParseInt(tokens[valueStart + 2],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品价值"),
                ModifierBits = ParseULong(tokens[valueStart + 3],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品修饰位"),
                Weight = ParseDouble(tokens[valueStart + 4],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品重量"),
                Abundance = ParseInt(tokens[valueStart + 5],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品丰度"),
                HeadArmor = ParseInt(tokens[valueStart + 6],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品头部护甲"),
                BodyArmor = ParseInt(tokens[valueStart + 7],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品身体护甲"),
                LegArmor = ParseInt(tokens[valueStart + 8],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品腿部护甲"),
                Difficulty = ParseInt(tokens[valueStart + 9],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品难度"),
                HitPoints = ParseInt(tokens[valueStart + 10],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品耐久"),
                SpeedRating = ParseInt(tokens[valueStart + 11],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品速度"),
                MissileSpeed = ParseInt(tokens[valueStart + 12],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品弹速"),
                WeaponLength = ParseInt(tokens[valueStart + 13],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品长度"),
                MaxAmmo = ParseInt(tokens[valueStart + 14],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品弹药量"),
                ThrustPacked = ParseInt(tokens[valueStart + 15],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品刺击伤害"),
                SwingPacked = ParseInt(tokens[valueStart + 16],
                    $"{Path.GetFileName(path)} 第 {lineIndex + 1} 行物品挥砍伤害")
            };

            items.Add(item);
        }

        if (items.Count != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 物品数量不匹配: 头部声明 {expectedCount}，实际解析 {items.Count}。");
        }

        return items;
    }

    public static IReadOnlyList<WarbandTroop> ReadTroops(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length < 2)
            throw new InvalidDataException($"{Path.GetFileName(path)} 至少需要两行头部。");

        var header = SplitTokens(lines[0]);
        if (header.Length < 3 || header[0] != "troopsfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 troopsfile version N。");

        var expectedCount = ParseInt(lines[1], $"{Path.GetFileName(path)} 第 2 行兵种数量");
        var recordLines = lines.Skip(2)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (recordLines.Count % 6 != 0)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 兵种记录行数不是 6 的倍数: {recordLines.Count} 行。" +
                "请确认文件没有被换行符或其他工具破坏。");
        }

        var recordCount = recordLines.Count / 6;
        if (recordCount != expectedCount)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 兵种数量不匹配: 头部声明 {expectedCount}，实际解析 {recordCount}。");
        }

        var troops = new List<WarbandTroop>(recordCount);
        for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            var offset = recordIndex * 6;
            var head = SplitTokens(recordLines[offset]);
            if (head.Length < 9)
            {
                throw new InvalidDataException(
                    $"{Path.GetFileName(path)} 第 {offset + 3} 行兵种头部字段不足: 需要 9 个 token，实际 {head.Length} 个。");
            }

            var troop = new WarbandTroop
            {
                Index = recordIndex,
                Id = head[0],
                Name = head[1],
                PluralName = head[2],
                Flags = ParseInt(head[3], $"{Path.GetFileName(path)} 兵种 {head[0]} 标志"),
                Scene = ParseInt(head[4], $"{Path.GetFileName(path)} 兵种 {head[0]} 场景"),
                Reserved = ParseInt(head[5], $"{Path.GetFileName(path)} 兵种 {head[0]} 保留字段"),
                FactionIndex = ParseInt(head[6], $"{Path.GetFileName(path)} 兵种 {head[0]} 阵营"),
                Upgrade1Index = ParseInt(head[7], $"{Path.GetFileName(path)} 兵种 {head[0]} 升级目标 1"),
                Upgrade2Index = ParseInt(head[8], $"{Path.GetFileName(path)} 兵种 {head[0]} 升级目标 2")
            };

            var inventoryTokens = SplitTokens(recordLines[offset + 1]);
            for (var i = 0; i + 1 < inventoryTokens.Length; i += 2)
            {
                var itemIndex = ParseInt(inventoryTokens[i],
                    $"{Path.GetFileName(path)} 兵种 {troop.Id} 装备索引");
                var modifier = ParseInt(inventoryTokens[i + 1],
                    $"{Path.GetFileName(path)} 兵种 {troop.Id} 装备修饰");
                troop.Inventory.Add(new WarbandInventoryEntry
                {
                    ItemIndex = itemIndex,
                    Modifier = modifier
                });
            }

            var attributeTokens = SplitTokens(recordLines[offset + 2]);
            if (attributeTokens.Length < 5)
                throw new InvalidDataException($"{Path.GetFileName(path)} 兵种 {troop.Id} 属性字段不足。");

            troop.Strength = ParseInt(attributeTokens[0], $"{Path.GetFileName(path)} 兵种 {troop.Id} 力量");
            troop.Agility = ParseInt(attributeTokens[1], $"{Path.GetFileName(path)} 兵种 {troop.Id} 敏捷");
            troop.Intelligence = ParseInt(attributeTokens[2], $"{Path.GetFileName(path)} 兵种 {troop.Id} 智力");
            troop.Charisma = ParseInt(attributeTokens[3], $"{Path.GetFileName(path)} 兵种 {troop.Id} 魅力");
            troop.Level = ParseInt(attributeTokens[4], $"{Path.GetFileName(path)} 兵种 {troop.Id} 等级");

            var proficiencyTokens = SplitTokens(recordLines[offset + 3]);
            for (var i = 0; i < proficiencyTokens.Length; i++)
            {
                troop.Proficiencies.Add(ParseInt(proficiencyTokens[i],
                    $"{Path.GetFileName(path)} 兵种 {troop.Id} 熟练度"));
            }

            var skillTokens = SplitTokens(recordLines[offset + 4]);
            foreach (var token in skillTokens)
            {
                troop.SkillWords.Add(ParseUInt(token,
                    $"{Path.GetFileName(path)} 兵种 {troop.Id} 技能字"));
            }

            var faceTokens = SplitTokens(recordLines[offset + 5]);
            foreach (var token in faceTokens)
            {
                troop.FaceKeys.Add(ParseULong(token,
                    $"{Path.GetFileName(path)} 兵种 {troop.Id} 面部编码"));
            }

            troops.Add(troop);
        }

        return troops;
    }

    public static IReadOnlyList<WarbandFaction> ReadFactions(string path)
    {
        var tokens = SplitTokens(File.ReadAllText(path, Encoding.Latin1));
        if (tokens.Length < 4 || tokens[0] != "factionsfile")
            throw new InvalidDataException($"{Path.GetFileName(path)} 头部不是 factionsfile version N。");

        var expectedCount = ParseInt(tokens[3], $"{Path.GetFileName(path)} 阵营数量");
        var factions = new List<WarbandFaction>(expectedCount);
        var position = 4;

        for (var index = 0; index < expectedCount; index++)
        {
            if (position + 4 > tokens.Length)
                throw new InvalidDataException($"{Path.GetFileName(path)} 阵营 {index} 头部被截断。");

            var faction = new WarbandFaction
            {
                Index = index,
                Id = tokens[position++],
                Name = tokens[position++],
                Flags = ParseInt(tokens[position++], $"{Path.GetFileName(path)} 阵营 {index} 标志"),
                Color = ParseInt(tokens[position++], $"{Path.GetFileName(path)} 阵营 {index} 颜色")
            };

            for (var relationIndex = 0; relationIndex < expectedCount; relationIndex++)
            {
                if (position >= tokens.Length)
                    throw new InvalidDataException($"{Path.GetFileName(path)} 阵营 {faction.Id} 关系数据被截断。");

                faction.Relations.Add(ParseDouble(tokens[position++],
                    $"{Path.GetFileName(path)} 阵营 {faction.Id} 关系"));
            }

            if (position >= tokens.Length)
                throw new InvalidDataException($"{Path.GetFileName(path)} 阵营 {faction.Id} 缺少爵位数量。");

            var rankCount = ParseInt(tokens[position++], $"{Path.GetFileName(path)} 阵营 {faction.Id} 爵位数量");
            if (rankCount < 0 || position + rankCount > tokens.Length)
                throw new InvalidDataException($"{Path.GetFileName(path)} 阵营 {faction.Id} 爵位数据被截断。");

            for (var rankIndex = 0; rankIndex < rankCount; rankIndex++)
                faction.Ranks.Add(tokens[position++]);

            factions.Add(faction);
        }

        if (position != tokens.Length)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} 解析结束后仍有 {tokens.Length - position} 个 token 未处理。");
        }

        return factions;
    }

    private static string[] SplitTokens(string value)
        => value.TrimStart('\uFEFF')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static int ParseInt(string token, string context)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"{context} 不是整数: {token}");

        return value;
    }

    private static uint ParseUInt(string token, string context)
    {
        if (!uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"{context} 不是无符号整数: {token}");

        return value;
    }

    private static ulong ParseULong(string token, string context)
    {
        if (!ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"{context} 不是无符号长整数: {token}");

        return value;
    }

    private static double ParseDouble(string token, string context)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"{context} 不是浮点数: {token}");

        return value;
    }
}
