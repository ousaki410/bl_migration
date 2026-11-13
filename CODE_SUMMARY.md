# WarbandPort 代码汇总与架构文档

## 项目结构

```
/home/ou/Projects/re-bannerlord/blmodtool/
├── blmodtool.sln                    # 解决方案文件
├── README.md                        # 用户使用文档（本文档）
├── M9_COMPARISON_REPORT.md          # M9 对比报告
├── M11_COMPARISON_DEEPEN.md         # M11 对比深化报告
├── src/
│   └── BlModTool.Cli/
│       ├── BlModTool.Cli.csproj     # 项目文件（.NET 8, xunit, ICSharpCode.Decompiler）
│       ├── Program.cs               # CLI 主入口（~650 行）
│       ├── WarbandModel.cs          # DTO 定义（~230 行）
│       ├── WarbandReader.cs         # 战团数据解析器（~600 行）
│       ├── WarbandPort.cs           # 转换核心（~2100 行）
│       ├── ItemCategoryMapping.cs   # 物品分类映射（~70 行）
│       └── obj/                     # 构建输出（忽略）
└── tests/
    └── BlModTool.Cli.Tests/
        ├── BlModTool.Cli.Tests.csproj # 测试项目（xunit）
        ├── WarbandReaderTests.cs    # 解析器测试（~264 行，13 测试）
        ├── WarbandPortTests.cs      # 转换测试（~900 行，27 测试）
        └── obj/                     # 构建输出（忽略）
```

## 核心类设计

### WarbandModule — 数据容器

```csharp
public sealed class WarbandModule
{
    public string ItemsPath { get; set; }
    public string TroopsPath { get; set; }
    public string FactionsPath { get; set; }
    public string? SkillsPath { get; set; }
    public string? PartyTemplatesPath { get; set; }
    public string? PartiesPath { get; set; }
    public string? QuestsPath { get; set; }
    public string? MenusPath { get; set; }
    public string? DialogStatesPath { get; set; }
    
    public List<WarbandItem> Items { get; } = new();
    public List<WarbandTroop> Troops { get; } = new();
    public List<WarbandFaction> Factions { get; } = new();
    public List<WarbandSkill> Skills { get; } = new();
    public List<WarbandPartyTemplate> PartyTemplates { get; } = new();
    public List<WarbandParty> Parties { get; } = new();
    public List<WarbandQuest> Quests { get; } = new();
    public List<WarbandMenu> Menus { get; } = new();
    public List<WarbandDialogState> DialogStates { get; } = new();
}
```

### WarbandReader — 数据解析层

解析所有战团源数据文件格式：

| 方法 | 源文件 | 格式特点 |
|------|--------|---------|
| `ReadItems` | item_kinds1.txt | 多变长字段（变体数量决定偏移量） |
| `ReadTroops` | troops.txt | 多变长字段（技能字位编码） |
| `ReadFactions` | factions.txt | 关系矩阵 + 爵位列表 |
| `ReadSkills` | skills.txt | 基础属性掩码（低4位=属性，0x10=party，0x100=inactive） |
| `ReadPartyTemplates` | party_templates.txt | 6 个 troop stack 插槽 |
| `ReadParties` | parties.txt | 跨 2 行（数据行 + 方位角行） |
| `ReadQuests` | quests.txt | 简单定义（id/name/xp/description） |
| `ReadMenus` | menus.txt | 多行格式（菜单行 + 缩进行选项，以 `.` 结束） |
| `ReadDialogStates` | dialog_states.txt | 单行 ID 列表 |

### WarbandPort — 转换核心

`Convert` 方法是公共入口，执行完整流水线：

```
1. 创建输出目录结构（ModuleData/items/, skills/, party_templates/, settlements/, quests/, menus/, dialog_states/）
2. 解析物品 → 分类写入 10 个 XML 文件
3. 解析兵种 → 构建 NPC 信息（装备、升级链、技能数值）
4. 解析文化 → 基础兵种/精锐兵种映射
5. 生成 NPC 角色模板（villager/caravan_master/guard/... 共 14 种 × 18 文化）
6. 解析技能 → warband_skills.xml
7. 解析党团模板 → partyTemplates.xml（troop stack → NPC 引用）
8. 解析定居点 → settlements.xml（Town/Village/Locations 结构）
9. 解析任务/菜单/对话状态 → 对应 XML
10. 生成 SubModule.xml（注册所有 XML 节点）
11. 生成 ModuleInfo.xml + Languages/en/strings.xml
12. 写入 conversion-report.json
13. 可选：安装到 Bannerlord Modules 目录
```

### 转换计数统计 (`ConversionCounts`)

| 字段 | 说明 | 示例值 |
|------|------|--------|
| ItemsRead/Converted/Skipped | 物品读取/转换/跳过 | 433/420/13 |
| TroopsRead/Converted/Skipped | 兵种读取/转换/跳过 | 619/134/485 |
| CulturesGenerated | 文化数量 | 18 |
| KingdomsGenerated | 王国数量 | 0/5 |
| SkillsRead/Converted/Skipped | 技能读取/转换/跳过 | 42/42/0 |
| PartyTemplatesRead/Converted/Skipped | 党团模板统计 | 48/33/15 |
| SettlementsRead/Converted/Skipped | 定居点统计 | 148/148/0 |
| QuestsRead/Converted/Skipped | 任务统计 | 41/41/0 |
| MenusRead/Converted/Skipped | 菜单统计 | 183/183/0 |
| DialogStatesRead/Converted/Skipped | 对话状态统计 | 889/889/0 |
| NpcRolesGenerated | NPC 角色模板数 | 144 |
| EquipmentEntries | 装备条目数 | 775 |
| UpgradeLinks | 升级链接数 | 59 |
| ItemsPerCategory | 各分类物品数 | weapons:148, shields:46, ... |

## 测试覆盖

### WarbandReaderTests (13 测试)

| 测试 | 内容 |
|------|------|
| ReadModule_returns_expected_counts | 433 物品 / 619 兵种 / 31 阵营 |
| ReadItems_has_expected_skippable_items | itm_no_item/cartridges/pistol/10 本书 |
| ReadTroops_has_template_troops | trp_player/trp_temp_troop/trp_game |
| ReadFactions_has_five_kingdoms | 5 个 fac_kingdom_* |
| ReadSkills_parses_all_42_skills | 42 技能，Charisma/Strength 掩码解码 |
| ReadModule_with_skills_loads_skills | Skills 集成 |
| ReadPartyTemplates_parses_all_48_templates | 48 模板，stack 字段验证 |
| ReadModule_with_party_templates_loads_templates | PartyTemplates 集成 |
| ReadParties_parses_all_200_parties | 200 队伍，148 定居点，坐标/阵营 |
| ReadModule_with_parties_loads_parties | Parties 集成 |
| ReadQuests_parses_all_41_quests | 41 任务，xp_reward/description |
| ReadMenus_parses_all_183_menus | 183 菜单，mesh/options |
| ReadDialogStates_parses_all_889_states | 889 对话状态 |

### WarbandPortTests (27 测试)

| 测试 | 内容 |
|------|------|
| Convert_default_mode_counts | 核心计数验证 |
| Convert_include_heroes_counts | 英雄模式计数 |
| Convert_emit_kingdoms_counts | 王国生成 |
| Convert_xml_has_no_bad_references | 装备/升级/文化引用完整性 |
| Convert_cultures_have_basic_and_elite_troop | 文化兵种映射 |
| Convert_animal_no_subtype_horse_has_subtype | 动物/马匹 subtype |
| Convert_skills_written_and_referenced | 技能写入与 base_attribute |
| Convert_submodule_registers_skills | SubModule 注册 Skills |
| Convert_items_split_into_category_files | 10 个分类文件，420 物品 |
| Convert_items_category_files_match_types | 分类类型正确性 |
| Convert_npc_roles_generated_per_culture | 14 角色 × 18 文化 = 252 |
| Convert_cultures_have_npc_role_references | 文化角色引用有效性 |
| Convert_submodule_registers_all_xml_nodes | 4/7/10/13 节点注册 |
| Convert_submodule_without_skills_when_no_skills | 无 skills 时不注册 |
| Convert_skills_linked_from_npcs | NPC 技能引用有效性 |
| Convert_report_items_per_category_counts | 报告与目录一致性 |
| Convert_readme_has_category_summary | README 内容 |
| Convert_deterministic_output | 多次运行输出一致 |
| Convert_party_templates_written | partyTemplates.xml |
| Convert_submodule_registers_party_templates | PartyTemplates 节点 |
| Convert_settlements_written | 城镇/城堡/村庄结构 |
| Convert_submodule_registers_settlements | Settlements 节点 |
| Convert_quests_written | quests.xml |
| Convert_menus_written | menus.xml（多行选项） |
| Convert_dialog_states_written | dialog_states.xml |
| Convert_submodule_registers_quests_menus_dialog_states | 3 个新节点注册 |

## 关键设计决策

1. **纯静态分析**：不运行游戏，不注入 DLL，不触碰联机部分
2. **wb_ 前缀**：所有转换出的 ID 统一加 `wb_` 前缀，避免与原生冲突
3. **单文件 vs 分类文件**：Items 默认单文件，`--submodule` 时支持 10 个分类文件（与 Bannerlord 原生结构对齐）
4. **NPC 角色模板**：每个文化生成 14 种通用角色（villager/caravan_master/guard/blacksmith/weaponsmith/tavernkeeper/townswoman/townsman/barber/merchant/prison_guard/ransom_broker/tournament_master/musician），填补战团缺失的 NPC 角色
5. **空占位文件**：banners/tournament_weapons/arm_armors 等战团无对应类型的文件生成空 XML，保持与 Bannerlord 10 文件结构一致
6. **决定性输出**：多次运行 NPC XML 字节相同（conversion-report.json 的 GeneratedAtUtc 除外）

## 后续扩展建议

| 优先级 | 建议 |
|--------|------|
| 高 | Items 分类粒度补充（旗帜/锦标赛武器/护肩内容，战团源无此类型可手动补充） |
| 高 | NPC 角色模板扩展（villager_/caravan_/townsman_ 等更多模板） |
| 中 | 支持更多战团源文件（scripts.txt 代码转换、screens.txt 界面定义） |
| 中 | 转换精度深化（补充 weapon_balance/thrust_speed/speed_rating/stealth_factor 等属性） |
| 低 | CLI 交互式向导模式 |
| 低 | 模块打包为 .zip/.mb 分发格式 |
