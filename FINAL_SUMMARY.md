# BlModTool — 最终汇总 (M0–M18)

## 项目概述

BlModTool 是一个运行在 Linux 上的《Mount & Blade II: Bannerlord》模组逆向工程/转换工具链。它将《Mount & Blade: Warband》的 ModuleSystem 源数据（item_kinds1.txt、troops.txt、factions.txt 等）转换为 Bannerlord 可加载的 XML 模组数据，并支持模块打包与交互式向导。

**路线 C**：逆向工程/Mod 研究，静态分析优先，不依赖 Proton，不做完整原生移植，不碰 DRM/破解组件和联机部分。

---

## 里程碑完成情况

| 里程碑 | 内容 | 状态 |
|--------|------|------|
| M0 | .NET 8 SDK 环境，解决方案与 CLI 骨架 | ✅ |
| M1 | `decompile` 子命令、递归/过滤/命名空间目录/泛型文件名处理 | ✅ |
| M2 | `api-snapshot`/`api-diff`、schema 2 特性与泛型约束、差异分类、破坏性变更过滤 | ✅ |
| M3 | `port-warband`（别名 `port-items`），物品/兵种/阵营转 Bannerlord XML | ✅ |
| M4 | 测试项目 `BlModTool.Cli.Tests`（xunit），40 个测试全部通过 | ✅ |
| M5 | 技能（skills.txt）转 `warband_skills.xml`（42/42），基础属性掩码解码 | ✅ |
| M6 | 物品分类输出（10 个 XML 文件，空占位文件自动生成） | ✅ |
| M7 | NPCCharacter 角色扩展（14 角色模板 × 18 文化 = 252 个合成 NPC） | ✅ |
| M8 | 测试与回归保障扩展（14 个新增 xunit 测试，总计 40 个全部通过） | ✅ |
| M9 | 转换结果与 Bannerlord 原生 XML 对比报告 | ✅ |
| M10 | Items 分类完善，NPCCharacter 角色扩展（252 个 NPC 角色模板） | ✅ |
| M11 | 对比深化报告（逐字段核对 ItemComponent/Weapon/Armor/Shield/Horse 属性）+ CLI `test` 子命令合并 | ✅ |
| M12 | 模块打包（ModuleInfo.xml + Languages/en/strings.xml）+ 安装到 Bannerlord Modules 目录 | ✅ |
| M13 | parties.txt → settlements.xml（148 个定居点，完全转换） | ✅ |
| M15 | quests.txt/menus.txt/dialog_states.txt → XML（41 任务，183 菜单，889 对话状态） | ✅ |
| M16 | scripts.txt → Bannerlord C# 脚本代码（354/354 脚本，Opcodes 常量类） | ✅ |
| M17 | `package` 子命令，将模块目录打包为 `.zip`/`.mb` 分发包 | ✅ |
| M18 | `wizard` 子命令，交互式向导逐步收集参数并执行转换 | ✅ |

---

## 工具链文件结构

```
blmodtool/
├── BlModTool.sln                 # 解决方案文件
├── README.md                     # 用户使用文档
├── CODE_SUMMARY.md               # 代码架构汇总
├── FINAL_SUMMARY.md              # 最终汇总（本文档）
├── M9_COMPARISON_REPORT.md       # M5–M11 转换结果对比报告
├── M11_COMPARISON_DEEPEN.md      # 对比深化报告
├── run.sh                        # Linux 一键启动脚本
├── test_bp.sh                    # 断点测试脚本（30 个测试）
├── test_breakpoints.sh           # 断点测试脚本（副本）
├── src/BlModTool.Cli/
│   ├── BlModTool.Cli.csproj      # .NET 8 AOT 项目文件
│   ├── Program.cs                # CLI 分发（decompile/api-snapshot/api-diff/port-warband/package/wizard/test）
│   ├── WarbandModel.cs           # DTO 定义（WarbandModule/Item/Troop/Faction/Skill/Quest/Menu/DialogState/Script）
│   ├── WarbandReader.cs          # 战团数据解析器（ReadItems/ReadTroops/ReadFactions/ReadSkills/ReadPartyTemplates/ReadParties/ReadQuests/ReadMenus/ReadDialogStates/ReadScripts）
│   ├── WarbandPort.cs            # 转换核心（WarbandPort.Convert 公共入口，所有 Write* 方法，PackageModule 打包方法）
│   ├── ItemCategoryMapping.cs    # 战团物品类型到 Bannerlord XML 文件映射规则
│   ├── JsonContext.cs            # JsonSerializerContext（AOT 兼容源生成）
│   ├── ApiAttributeTypeProvider.cs
│   ├── ApiDiff.cs
│   ├── ApiModel.cs
│   ├── ApiSignatureProvider.cs
│   ├── ApiSnapshotBuilder.cs
│   └── NamespaceFilter.cs
├── tests/BlModTool.Cli.Tests/
│   ├── BlModTool.Cli.Tests.csproj
│   ├── WarbandReaderTests.cs     # 读取器测试
│   └── WarbandPortTests.cs       # 转换器测试（含 package 打包测试）
└── publish/native/BlModTool.Cli  # .NET 8 AOT 原生可执行文件（~18.7 MB，无需 SDK 运行）
```

---

## 核心命令

### port-warband（战团 → Bannerlord XML 转换）

```bash
./publish/native/BlModTool.Cli port-warband \
  --native /path/to/Warband/Modules/Native \
  --output ./warband-port \
  --submodule --readme \
  --party-templates --settlements --quests --menus --dialog-states --scripts \
  --install --bannerlord-dir /path/to/Bannerlord \
  --module-name WarbandPort
```

支持选项：`--native` `--output` `--include-heroes` `--emit-kingdoms` `--keep-meshes` `--readme` `--submodule` `--install` `--bannerlord-dir` `--module-name` `--party-templates` `--settlements` `--quests` `--menus` `--dialog-states` `--scripts`

### package（模块打包分发）

```bash
./publish/native/BlModTool.Cli package \
  --input ./warband-port \
  --output ./WarbandPort.zip \
  --format zip
```

支持格式：`zip`、`mb`（mb 本质为 zip 重命名）。

### wizard（交互式向导）

```bash
./publish/native/BlModTool.Cli wizard
```

逐步交互收集所有参数，确认后执行 `port-warband`。

### test（运行 xunit 测试）

```bash
./publish/native/BlModTool.Cli test
```

### 其他

`decompile`、`api-snapshot`、`api-diff`、`build`、`clean`、`help`（通过 `run.sh` 或直接调用 CLI）。

---

## 测试结果

| 测试类型 | 数量 | 通过 | 失败 |
|----------|------|------|------|
| xunit 单元测试 | 46 | 46 | 0 |
| 断点测试 (`test_bp.sh`) | 30 | 30 | 0 |
| 构建警告 | 0 | — | — |
| 构建错误 | 0 | — | — |

---

## 原生可执行文件

- 路径：`publish/native/BlModTool.Cli`
- 大小：~18.7 MB
- 类型：.NET 8 AOT 自包含可执行文件（`--self-contained true`）
- 依赖：仅系统基础库（libz, libm 等），**不需要安装 .NET SDK**，直接在 Linux x64 上运行
- 运行示例：`./publish/native/BlModTool.Cli --help`

---

## 转换覆盖统计（基于 Mount&Blade 1.011 Native）

| 数据类型 | 源数量 | 转换数量 | 成功率 |
|----------|--------|----------|--------|
| 物品 (items) | 433 | 420 | 97.0% |
| 兵种 (troops) | 619 | 134 | 21.6%（其余为跳过/内部模板/英雄） |
| 阵营 (factions) | 31 | 18 | 58.1%（主文化） |
| 技能 (skills) | 42 | 42 | 100% |
| 党团模板 (party_templates) | 48 | 33 | 68.8% |
| 定居点 (settlements) | 148 | 148 | 100% |
| 任务 (quests) | 41 | 41 | 100% |
| 菜单 (menus) | 183 | 183 | 100% |
| 对话状态 (dialog_states) | 889 | 889 | 100% |
| 脚本 (scripts) | 354 | 354 | 100% |
| NPC 角色模板 | — | 252（14 角色 × 18 文化） | — |

---

## 使用约束与限制

- 反编译产物只供本地参考，不分发。
- 不碰 DRM/破解组件和联机部分。
- Warband 网格与 Bannerlord 不兼容，默认使用 Bannerlord 回退网格；可用 `--keep-meshes` 保留原始名（需自行提供资源）。
- 英雄兵种默认跳过，需 `--include-heroes`。
- 王国为骨架数据，无拥有者/定居点/氏族/纹章设置。
- 书籍、火器、子弹、`itm_no_item` 占位符被跳过。
- 脚本生成的 C# 代码为原始 opcode 序列，需模组作者自行实现 VM 解释器或映射到 TaleWorlds 行为 API。

---

## 环境要求

- Linux x64
- .NET 8 SDK（构建时）：`~/.dotnet`
- 构建临时目录：`/tmp/opencode` 或 `blmodtool/.tmp`
- 构建命令：`export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && export TMPDIR=/home/ou/Projects/re-bannerlord/blmodtool/.tmp`

---

## 仓库地址

https://github.com/ousaki410/bl_migration