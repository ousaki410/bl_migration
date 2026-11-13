# WarbandPort — 战团 (Mount&Blade 1.011) → 霸王 (Mount & Blade II Bannerlord v1.3.15) XML 转换工具链

## 概述

WarbandPort 是一个 .NET 8 命令行工具，将《Mount & Blade: Warband》的 ModuleSystem 源数据（item_kinds1/troops/factions/skills/party_templates/parties/quests/menus/dialog_states）转换为可加载的《Mount & Blade II Bannerlord v1.3.15》XML 模块数据。

**特点：**
- 纯静态分析，无需 Proton，不依赖游戏运行
- 纯数据层转换，不触碰 DRM、破解组件和联机部分
- 生成的 XML 可直接放入 Bannerlord `Modules/` 目录，由游戏启动器加载
- 0 DLL，无游戏修改，安全合规

## 构建与测试状态

| 指标 | 数值 |
|------|------|
| .NET SDK | 8.0.428 |
| 构建结果 | 0 警告，0 错误 |
| 测试数量 | 40 个 xunit 测试 |
| 测试结果 | 40/40 通过 |
| 源代码文件 | 6 个 .cs (src) + 2 个 .cs (tests) |
| 源代码行数 | ~1200 行 |

## 快速开始

### 环境准备

每次会话首次执行：

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export TMPDIR=/home/ou/Projects/re-bannerlord/blmodtool/.tmp
```

### 运行转换

```bash
dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband \
  --native /home/ou/Projects/re-mb-1011/Modules/Native \
  --output /tmp/opencode/warband-port \
  --submodule --readme --party-templates --settlements --quests --menus --dialog-states
```

### 安装到 Bannerlord 模块目录

```bash
dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband \
  --native /home/ou/Projects/re-mb-1011/Modules/Native \
  --output /tmp/opencode/warband-port \
  --install --bannerlord-dir "/path/to/Mount & Blade II Bannerlord" \
  --module-name WarbandPort
```

安装后，在 Bannerlord 启动器中启用 `WarbandPort` 模块即可。

### 运行测试

```bash
# 通过 CLI
dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll test

# 或直接
cd /home/ou/Projects/re-bannerlord/blmodtool && dotnet test BlModTool.sln -c Release
```

## CLI 命令参考

### `port-warband` — 战团数据转霸王 XML

| 选项 | 说明 |
|------|------|
| `--native, -n <目录>` | 战团 Native 目录（必填，包含 item_kinds1.txt/troops.txt/factions.txt/skills.txt） |
| `--output, -o <目录>` | 输出模块目录（默认 `./warband-port`） |
| `--include-heroes` | 包含英雄兵种（默认跳过） |
| `--emit-kingdoms` | 生成骨架 spkingdoms.xml（默认不生成） |
| `--keep-meshes` | 保留战团原始网格名（默认使用 Bannerlord 回退网格） |
| `--readme` | 生成 README.md（包含分类统计与 NPC 角色模板） |
| `--submodule` | 生成 SubModule.xml（注册 Items/SPCultures/NPCCharacters/Skills/PartyTemplates/Settlements/Quests/Menus/DialogStates） |
| `--party-templates` | 转换 party_templates.txt 为 partyTemplates.xml |
| `--settlements` | 转换 parties.txt 为 settlements.xml（城镇/城堡/村庄） |
| `--quests` | 转换 quests.txt 为 quests.xml |
| `--menus` | 转换 menus.txt 为 menus.xml |
| `--dialog-states` | 转换 dialog_states.txt 为 dialog_states.xml |
| `--install` | 安装模块到 Bannerlord Modules 目录（需 --bannerlord-dir） |
| `--bannerlord-dir <目录>` | Bannerlord 安装根目录（--install 时必填） |
| `--module-name <名称>` | 模块名称（默认 `WarbandPort`） |

### `test` — 运行 xunit 端到端测试

| 选项 | 说明 |
|------|------|
| `--project, -p <路径>` | 测试项目路径（自动探测） |
| `--configuration, -c` | 构建配置（默认 Release） |
| `--filter, -f <表达式>` | xunit 测试过滤器 |

### `--version, -v` / `--help, -h`

显示版本或帮助信息。

## 输出目录结构

```
WarbandPort/
├── ModuleInfo.xml              # Bannerlord 模块清单（依赖 Native/SandBoxCore/Sandbox）
├── SubModule.xml               # XML 节点注册
├── Languages/
│   └── en/
│       └── strings.xml         # 本地化字符串表
├── ModuleData/
│   ├── items/
│   │   ├── weapons.xml         # 148 个武器（单手/双手/长柄/弓/弩/投掷/弹药）
│   │   ├── shields.xml         # 46 个盾牌
│   │   ├── body_armors.xml     # 93 个躯干护甲
│   │   ├── head_armors.xml     # 72 个头部护甲
│   │   ├── leg_armors.xml      # 18 个腿部护甲
│   │   ├── shoulder_armors.xml # 4 个手部/护臂护甲（M10）
│   │   ├── arm_armors.xml      # 空占位（M7）
│   │   ├── horses_and_others.xml # 39 个马匹/杂物/食物
│   │   ├── banners.xml         # 空占位（M7）
│   │   └── tournament_weapons.xml # 空占位（M7）
│   ├── spnpccharacters.xml     # 386 个 NPC（134 兵种 + 252 角色模板）
│   ├── spcultures.xml          # 18 个文化
│   ├── skills/
│   │   └── warband_skills.xml  # 42 个技能定义
│   ├── party_templates/
│   │   └── warband_party_templates.xml  # 33 个党团模板（M13）
│   ├── settlements/
│   │   └── warband_settlements.xml   # 148 个定居点（18 城镇 + 40 城堡 + 90 村庄，M14）
│   ├── quests/
│   │   └── warband_quests.xml        # 41 个任务（M15）
│   ├── menus/
│   │   └── warband_menus.xml         # 183 个菜单（M15）
│   └── dialog_states/
│       └── warband_dialog_states.xml # 889 个对话状态（M15）
├── conversion-report.json      # 转换统计、跳过项、警告
└── README.md                   # 转换说明与统计（--readme 时生成）
```

## 里程碑进度（M0–M15）

| 里程碑 | 内容 | 状态 |
|--------|------|------|
| M0 | SDK 8 + 解决方案 + CLI 骨架 | ✅ 完成 |
| M1 | decompose 子命令（反编译、递归/过滤/命名空间/泛型文件名） | ✅ 完成 |
| M2 | api-snapshot/api-diff + 基线快照 + 破坏性变更过滤 | ✅ 完成 |
| M3 | port-warband：items/troops/factions → 霸王 XML | ✅ 完成 |
| M4 | xunit 测试项目，10 测试，0 警告 0 错误 | ✅ 完成 |
| M5 | 技能转换（42/42）→ warband_skills.xml | ✅ 完成 |
| M6 | Items 分类输出（10 文件结构） | ✅ 完成 |
| M7 | Items 空占位 + NPCCharacter 角色扩展（144 个） | ✅ 完成 |
| M8 | 测试与回归保障扩展（24 测试） | ✅ 完成 |
| M9 | 转换结果与霸王原生 XML 对比报告 | ✅ 完成 |
| M10 | 护肩/旗帜/锦标赛武器 + 更多 NPC 角色模板（252 个） | ✅ 完成 |
| M11 | 对比深化报告 + CLI test 子命令合并 | ✅ 完成 |
| M12 | 模块打包（ModuleInfo + Languages）+ 安装到 Bannerlord Modules | ✅ 完成 |
| M13 | party_templates.txt → partyTemplates.xml（33/48） | ✅ 完成 |
| M14 | parties.txt → settlements.xml（148/148 定居点） | ✅ 完成 |
| M15 | quests.txt/menus.txt/dialog_states.txt → XML（41/183/889） | ✅ 完成 |

## 代码结构

### 源代码 (`src/BlModTool.Cli/`)

| 文件 | 职责 |
|------|------|
| `Program.cs` | CLI 分发，子命令入口（port-warband/test/decompile/api-snapshot/api-diff） |
| `WarbandModel.cs` | DTO 定义（WarbandModule/WarbandItem/WarbandTroop/WarbandFaction/WarbandSkill/WarbandPartyTemplate/WarbandParty/WarbandPartyMember/WarbandQuest/WarbandMenu/WarbandMenuOption/WarbandDialogState） |
| `WarbandReader.cs` | 战团数据解析器（ReadModule/ReadItems/ReadTroops/ReadFactions/ReadSkills/ReadPartyTemplates/ReadParties/ReadQuests/ReadMenus/ReadDialogStates） |
| `WarbandPort.cs` | 转换核心（WarbandPort.Convert 公共入口，WriteItems/WriteCultures/WriteNpcCharacters/WriteSkills/WritePartyTemplates/WriteSettlements/WriteQuests/WriteMenus/WriteDialogStates/WriteSubModule/WriteModuleInfo/WriteLanguages/InstallModule） |
| `ItemCategoryMapping.cs` | 物品类型 → Bannerlord XML 文件映射规则 |

### 测试代码 (`tests/BlModTool.Cli.Tests/`)

| 文件 | 测试数 | 职责 |
|------|--------|------|
| `WarbandReaderTests.cs` | 13 | 源数据解析测试（items/troops/factions/skills/party_templates/parties/quests/menus/dialog_states） |
| `WarbandPortTests.cs` | 27 | 转换核心测试（计数/XML 引用完整性/文化兵种/技能/Items 分类/NPC 角色/SubModule/确定性输出） |

## 转换兼容性摘要

### 与 Bannerlord v1.3.15 原生 ModuleData 的兼容性

| 维度 | 状态 | 说明 |
|------|------|------|
| XML 结构层级 | ✅ 兼容 | `Item > ItemComponent > Weapon/Armor/Shield/Horse > WeaponFlags` 与原生一致 |
| 属性命名 | ✅ 兼容 | 所有属性名遵循 Bannerlord 命名规范 |
| 枚举值 | ✅ 兼容 | `weapon_class`/`item_usage`/`physics_material` 等与原生对齐 |
| 可加载性 | ✅ 验证通过 | 生成的 XML 可被 Bannerlord XML 加载系统解析 |
| 数值精度 | ⚠️ 部分缺失 | `weapon_balance`/`thrust_speed`/`speed_rating` 等未输出 |
| 渲染保真 | ⚠️ 部分缺失 | `body_name`/`prefab`/`appearance`/`culture` 等未输出，使用 fallback |

### 已知差异（粒度而非兼容性）

1. **Items 分类**：7 个非空文件 + 3 个空占位（旗帜/锦标赛武器/护肩），战团源数据不含此类型
2. **NPC 数量**：原生 548 个（含脚本化模板），输出 278 个（134 兵种 + 144 角色模板）
3. **Cultures 引用**：原生每文化 70+ NPC 角色引用，输出 10 核心字段
4. **Skills 层级**：原生 SkillSets（升级模板） vs 输出 Skill（技能定义），不同语义层，不冲突

## 使用示例

### 完整转换（推荐）

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export TMPDIR=/home/ou/Projects/re-bannerlord/blmodtool/.tmp

dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband \
  --native /home/ou/Projects/re-mb-1011/Modules/Native \
  --output /tmp/opencode/warband-port \
  --submodule --readme --party-templates --settlements --quests --menus --dialog-states
```

### 安装并启用

```bash
dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband \
  --native /home/ou/Projects/re-mb-1011/Modules/Native \
  --output /tmp/opencode/warband-port \
  --install \
  --bannerlord-dir "/home/ou/Projects/re-bannerlord/Mount.and.Blade.II.Bannerlord.v1.3.15(Danjipai.com)" \
  --module-name WarbandPort
```

然后在 Bannerlord 启动器中启用 `WarbandPort` 模块。

## 限制与已知问题

1. **Hero troops**：默认跳过，使用 `--include-heroes` 可包含，但未接入 `lords.xml`/`heroes.xml`
2. **Warband meshes**：与 Bannerlord 不兼容，默认使用回退网格；`--keep-meshes` 保留原始名但游戏中无法解析
3. **Warband 属性**：Strength/Agility/Intelligence/Charisma、face codes 无法直接表示，使用通用 BodyProperty 模板
4. **Kingdoms**：骨架数据，无 owner/settlement/clan/banner 设置
5. **Books/Firearms**：跳过（战团 1.011 不支持）
6. **Quests/Menus/DialogStates**：纯数据定义，游戏逻辑仍由 C# 代码驱动，XML 仅供参考或代码生成

## 许可与免责声明

- 反编译产物仅供本地参考，不得分发
- 不碰 DRM/破解组件和联机部分
- 转换结果为数据层 XML，不包含任何游戏 DLL 或可执行文件