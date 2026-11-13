using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using BlModTool.Cli;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

const string toolName = "BlModTool";
const string version = "0.5.0-m10";

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

if (args.Contains("--version") || args.Contains("-v"))
{
    Console.WriteLine($"{toolName} v{version}");
    Console.WriteLine("Bannerlord Modder Toolkit — static analysis & porting tools");
    return 0;
}

var command = args[0];
switch (command)
{
    case "decompile":
        return RunDecompile(args);
    case "api-snapshot":
        return RunApiSnapshot(args);
    case "api-diff":
        return RunApiDiff(args);
    case "port-warband":
    case "port-items":
        return RunPortWarband(args);
    case "test":
        return RunTest(args);
    case "package":
        return RunPackage(args);
    case "wizard":
        return RunWizard(args);
    default:
        Console.WriteLine($"未知命令: {command}");
        PrintHelp();
        return 1;
}

int RunDecompile(string[] args)
{
    string? input = null;
    string? outDir = null;
    bool recursive = false;
    bool verbose = false;
    var filters = new List<string>();

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--input" or "-i":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个路径参数。");
                    return 1;
                }
                input = args[++i];
                break;
            case "--out" or "-o":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个目录参数。");
                    return 1;
                }
                outDir = args[++i];
                break;
            case "--recursive" or "-r":
                recursive = true;
                break;
            case "--filter" or "-f":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个命名空间前缀。");
                    return 1;
                }
                foreach (var part in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    filters.Add(part);
                break;
            case "--verbose":
                verbose = true;
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    if (string.IsNullOrEmpty(input))
    {
        Console.Error.WriteLine("错误: decompile 命令需要 --input <dll路径>");
        PrintHelp();
        return 1;
    }

    var inputFile = new FileInfo(input);
    if (!inputFile.Exists)
    {
        Console.Error.WriteLine($"错误: 文件不存在: {input}");
        return 1;
    }

    var outputRoot = outDir ?? Path.Combine(Directory.GetCurrentDirectory(), "decompiled", Path.GetFileNameWithoutExtension(inputFile.Name));

    var assemblies = ResolveInputs(inputFile, recursive);
    Console.WriteLine($"[decompile] 输入: {inputFile.FullName}");
    Console.WriteLine($"[decompile] 输出根目录: {Path.GetFullPath(outputRoot)}");
    Console.WriteLine($"[decompile] 程序集: {assemblies.Count} 个{(recursive ? "（递归）" : "")}");
    if (filters.Count > 0)
        Console.WriteLine($"[decompile] 命名空间过滤: {string.Join(", ", filters)}");

    int totalWritten = 0;
    int totalFailed = 0;
    foreach (var assemblyPath in assemblies)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assemblyOut = recursive ? Path.Combine(outputRoot, assemblyName) : outputRoot;
        try
        {
            var (written, failed) = DecompileAssembly(assemblyPath, assemblyOut, filters, verbose);
            totalWritten += written;
            totalFailed += failed;
        }
        catch (Exception ex)
        {
            totalFailed++;
            Console.Error.WriteLine($"[decompile] 程序集失败: {assemblyName}: {ex.Message}");
        }
    }

    Console.WriteLine($"[decompile] 全部完成: {assemblies.Count} 个程序集，{totalWritten} 个类型" +
                      (totalFailed > 0 ? $"，{totalFailed} 个失败。" : "。"));
    return totalFailed > 0 ? 2 : 0;
}

List<string> ResolveInputs(FileInfo inputFile, bool recursive)
{
    var result = new List<string>();
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var queue = new Queue<string>();
    var probeDir = inputFile.DirectoryName ?? Directory.GetCurrentDirectory();

    queue.Enqueue(inputFile.FullName);

    while (queue.Count > 0)
    {
        var dllPath = queue.Dequeue();
        var simpleName = Path.GetFileNameWithoutExtension(dllPath);
        if (!visited.Add(simpleName))
            continue;

        result.Add(dllPath);

        if (!recursive)
            continue;

        foreach (var referenceName in GetAssemblyReferences(dllPath))
        {
            // 只跟进 TaleWorlds 自身的引用，避免把系统库/第三方库一起反编译。
            if (!referenceName.StartsWith("TaleWorlds.", StringComparison.Ordinal))
                continue;
            if (visited.Contains(referenceName))
                continue;

            var candidate = Path.Combine(probeDir, referenceName + ".dll");
            if (File.Exists(candidate))
                queue.Enqueue(candidate);
        }
    }

    return result;
}

(int Written, int Failed) DecompileAssembly(string dllPath, string outputDir, List<string> filters, bool verbose)
{
    Console.WriteLine($"[decompile] {Path.GetFileName(dllPath)}");

    var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings());
    if (decompiler.TypeSystem.MainModule is not MetadataModule metadataModule)
    {
        Console.Error.WriteLine("  错误: 无法读取程序集元数据。");
        return (0, 1);
    }

    int written = 0;
    int failed = 0;
    int filtered = 0;
    var usedNamesByDir = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var typeDef in metadataModule.TypeDefinitions)
    {
        var ns = typeDef.Namespace ?? string.Empty;
        if (!NamespaceFilter.Matches(ns, filters))
        {
            filtered++;
            continue;
        }

        try
        {
            var code = decompiler.DecompileTypeAsString(typeDef.FullTypeName);

            // 相对类型名：去掉命名空间前缀，嵌套类型保留 Outer.Inner 形式。
            var relativeName = typeDef.FullName;
            if (ns.Length > 0 && relativeName.StartsWith(ns + ".", StringComparison.Ordinal))
                relativeName = relativeName[(ns.Length + 1)..];

            // 泛型类型带 arity 后缀，避免 Foo 与 Foo<T> 输出成同名文件。
            if (typeDef.TypeParameterCount > 0)
                relativeName += "`" + typeDef.TypeParameterCount;

            var typeDir = ns.Length > 0
                ? Path.Combine(outputDir, ns.Replace('.', Path.DirectorySeparatorChar))
                : outputDir;
            Directory.CreateDirectory(typeDir);

            if (!usedNamesByDir.TryGetValue(typeDir, out var usedNames))
                usedNamesByDir[typeDir] = usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var uniqueName = MakeUniqueName(SanitizeFileName(relativeName), usedNames);
            var filePath = Path.Combine(typeDir, uniqueName + ".cs");
            File.WriteAllText(filePath, code ?? string.Empty);

            written++;
            if (verbose)
                Console.WriteLine($"  [{written}] {typeDef.FullName}");
        }
        catch (Exception ex)
        {
            failed++;
            Console.Error.WriteLine($"  [跳过] {typeDef.FullName}: {ex.Message}");
        }
    }

    Console.WriteLine($"  完成: {written} 个类型" +
                      (filtered > 0 ? $"，{filtered} 个被过滤" : "") +
                      (failed > 0 ? $"，{failed} 个失败" : "") + "。");
    return (written, failed);
}

int RunApiSnapshot(string[] args)
{
    string? input = null;
    string? outFile = null;
    bool recursive = false;
    bool verbose = false;
    var filters = new List<string>();

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--input" or "-i":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个路径参数。");
                    return 1;
                }
                input = args[++i];
                break;
            case "--out" or "-o":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个文件参数。");
                    return 1;
                }
                outFile = args[++i];
                break;
            case "--recursive" or "-r":
                recursive = true;
                break;
            case "--filter" or "-f":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个命名空间前缀。");
                    return 1;
                }
                foreach (var part in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    filters.Add(part);
                break;
            case "--verbose":
                verbose = true;
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    if (string.IsNullOrEmpty(input))
    {
        Console.Error.WriteLine("错误: api-snapshot 命令需要 --input <dll或目录>");
        return 1;
    }

    List<string> dllPaths;
    string defaultName;

    if (Directory.Exists(input))
    {
        dllPaths = new DirectoryInfo(input)
            .EnumerateFiles("TaleWorlds*.dll", SearchOption.TopDirectoryOnly)
            .Select(file => file.FullName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        defaultName = "api-snapshot";

        if (dllPaths.Count == 0)
        {
            Console.Error.WriteLine($"错误: 目录中没有 TaleWorlds*.dll: {input}");
            return 1;
        }
    }
    else
    {
        var inputFile = new FileInfo(input);
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"错误: 文件或目录不存在: {input}");
            return 1;
        }

        dllPaths = ResolveInputs(inputFile, recursive);
        defaultName = Path.GetFileNameWithoutExtension(inputFile.Name);
    }

    var outputPath = Path.GetFullPath(outFile ??
        Path.Combine(Directory.GetCurrentDirectory(), "snapshots", defaultName + ".json"));

    Console.WriteLine($"[api-snapshot] 输入: {input}（{dllPaths.Count} 个 DLL）");
    Console.WriteLine($"[api-snapshot] 输出: {outputPath}");
    if (filters.Count > 0)
        Console.WriteLine($"[api-snapshot] 命名空间过滤: {string.Join(", ", filters)}");

    var snapshot = ApiSnapshotBuilder.Build(dllPaths, filters, version);
    if (snapshot.Assemblies.Count == 0)
    {
        Console.Error.WriteLine("错误: 没有找到可读取的托管程序集。");
        return 1;
    }

    foreach (var assembly in snapshot.Assemblies)
    {
        var memberCount = assembly.Types.Sum(type => type.Members.Count);
        Console.WriteLine($"  {assembly.Name} {assembly.Version}: {assembly.Types.Count} 个类型，{memberCount} 个成员");

        if (verbose)
        {
            foreach (var type in assembly.Types)
                Console.WriteLine($"    {ApiDiffEngine.TypeDisplay(type)}");
        }
    }

    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);

    File.WriteAllText(outputPath, JsonSerializer.Serialize(snapshot, ApiJson.Options));
    var bytes = new FileInfo(outputPath).Length;
    var totalTypes = snapshot.Assemblies.Sum(assembly => assembly.Types.Count);
    Console.WriteLine($"[api-snapshot] 完成: {snapshot.Assemblies.Count} 个程序集，{totalTypes} 个类型，{bytes} 字节。");
    return 0;
}

int RunApiDiff(string[] args)
{
    string? oldPath = null;
    string? newPath = null;
    bool jsonOutput = false;
    bool breakingOnly = false;
    bool failOnBreaking = false;

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--old":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个快照文件。");
                    return 1;
                }
                oldPath = args[++i];
                break;
            case "--new":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个快照文件。");
                    return 1;
                }
                newPath = args[++i];
                break;
            case "--json":
                jsonOutput = true;
                break;
            case "--breaking-only":
                breakingOnly = true;
                break;
            case "--fail-on-breaking":
                failOnBreaking = true;
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
    {
        Console.Error.WriteLine("错误: api-diff 命令需要 --old <旧快照.json> 和 --new <新快照.json>");
        return 1;
    }

    if (!File.Exists(oldPath))
    {
        Console.Error.WriteLine($"错误: 文件不存在: {oldPath}");
        return 1;
    }

    if (!File.Exists(newPath))
    {
        Console.Error.WriteLine($"错误: 文件不存在: {newPath}");
        return 1;
    }

    try
    {
        var oldSnapshot = LoadSnapshot(oldPath);
        var newSnapshot = LoadSnapshot(newPath);
        var result = ApiDiffEngine.Compare(oldSnapshot, newSnapshot,
            Path.GetFullPath(oldPath), Path.GetFullPath(newPath));

        if (jsonOutput)
        {
            var entries = breakingOnly
                ? result.Entries.Where(entry => entry.Breaking).ToList()
                : result.Entries;
            var view = new
            {
                result.OldSnapshot,
                result.NewSnapshot,
                result.BreakingChanges,
                result.NonBreakingChanges,
                Entries = entries
            };
            Console.WriteLine(JsonSerializer.Serialize(view, ApiJson.Options));
        }
        else
            Console.Write(ApiDiffEngine.FormatText(result, breakingOnly));

        return failOnBreaking && result.BreakingChanges > 0 ? 1 : 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"错误: 无法比较快照: {ex.Message}");
        return 1;
    }
}

int RunPortWarband(string[] args)
{
    string? nativeDir = null;
    string? outputDir = null;
    var options = new WarbandPortOptions();

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--native":
            case "-n":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个战团 Native 目录。");
                    return 1;
                }
                nativeDir = args[++i];
                break;
            case "--output":
            case "-o":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个输出目录。");
                    return 1;
                }
                outputDir = args[++i];
                break;
            case "--include-heroes":
                options.IncludeHeroes = true;
                break;
            case "--emit-kingdoms":
                options.EmitKingdoms = true;
                break;
            case "--keep-meshes":
                options.KeepMeshes = true;
                break;
            case "--readme":
                options.GenerateReadme = true;
                break;
            case "--submodule":
                options.GenerateSubModule = true;
                break;
            case "--install":
                options.Install = true;
                break;
            case "--bannerlord-dir":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个 Bannerlord 安装目录。");
                    return 1;
                }
                options.BannerlordDir = args[++i];
                break;
            case "--module-name":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个模块名称。");
                    return 1;
                }
                options.ModuleName = args[++i];
                break;
            case "--party-templates":
                options.GeneratePartyTemplates = true;
                break;
            case "--settlements":
                options.GenerateSettlements = true;
                break;
            case "--quests":
                options.GenerateQuests = true;
                break;
            case "--menus":
                options.GenerateMenus = true;
                break;
            case "--dialog-states":
                options.GenerateDialogStates = true;
                break;
            case "--scripts":
                options.GenerateScripts = true;
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    if (string.IsNullOrEmpty(nativeDir))
    {
        Console.Error.WriteLine("错误: port-warband 命令需要 --native <战团 Native 目录>");
        return 1;
    }

    nativeDir = Path.GetFullPath(nativeDir);
    if (!Directory.Exists(nativeDir))
    {
        Console.Error.WriteLine($"错误: 目录不存在: {nativeDir}");
        return 1;
    }

    var itemsPath = Path.Combine(nativeDir, "item_kinds1.txt");
    var troopsPath = Path.Combine(nativeDir, "troops.txt");
    var factionsPath = Path.Combine(nativeDir, "factions.txt");
    var skillsPath = Path.Combine(nativeDir, "skills.txt");
    var partyTemplatesPath = Path.Combine(nativeDir, "party_templates.txt");
    var partiesPath = Path.Combine(nativeDir, "parties.txt");
    var questsPath = Path.Combine(nativeDir, "quests.txt");
    var menusPath = Path.Combine(nativeDir, "menus.txt");
    var dialogStatesPath = Path.Combine(nativeDir, "dialog_states.txt");
    var scriptsPath = Path.Combine(nativeDir, "scripts.txt");
    var requiredPaths = new[] { itemsPath, troopsPath, factionsPath, skillsPath };
    foreach (var path in requiredPaths)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"错误: 文件不存在: {path}");
            return 1;
        }
    }

    if (options.GeneratePartyTemplates && !File.Exists(partyTemplatesPath))
    {
        Console.Error.WriteLine($"错误: --party-templates 需要文件: {partyTemplatesPath}");
        return 1;
    }

    if (options.GenerateSettlements && !File.Exists(partiesPath))
    {
        Console.Error.WriteLine($"错误: --settlements 需要文件: {partiesPath}");
        return 1;
    }

    if (options.GenerateQuests && !File.Exists(questsPath))
    {
        Console.Error.WriteLine($"错误: --quests 需要文件: {questsPath}");
        return 1;
    }

    if (options.GenerateMenus && !File.Exists(menusPath))
    {
        Console.Error.WriteLine($"错误: --menus 需要文件: {menusPath}");
        return 1;
    }

    if (options.GenerateDialogStates && !File.Exists(dialogStatesPath))
    {
        Console.Error.WriteLine($"错误: --dialog-states 需要文件: {dialogStatesPath}");
        return 1;
    }

    if (options.GenerateScripts && !File.Exists(scriptsPath))
    {
        Console.Error.WriteLine($"错误: --scripts 需要文件: {scriptsPath}");
        return 1;
    }

    var outputRoot = Path.GetFullPath(outputDir ??
        Path.Combine(Directory.GetCurrentDirectory(), "warband-port"));
    Console.WriteLine($"[port-warband] 输入: {nativeDir}");
    Console.WriteLine($"[port-warband] 输出: {outputRoot}");

    try
    {
        var module = WarbandReader.ReadModule(itemsPath, troopsPath, factionsPath, skillsPath,
            options.GeneratePartyTemplates ? partyTemplatesPath : null,
            options.GenerateSettlements ? partiesPath : null,
            options.GenerateQuests ? questsPath : null,
            options.GenerateMenus ? menusPath : null,
            options.GenerateDialogStates ? dialogStatesPath : null,
            options.GenerateScripts ? scriptsPath : null);
        Console.WriteLine($"[port-warband] 读取: {module.Items.Count} 物品，{module.Troops.Count} 兵种，{module.Factions.Count} 阵营");
        if (options.GeneratePartyTemplates)
            Console.WriteLine($"[port-warband] 读取: {module.PartyTemplates.Count} 党团模板");
        if (options.GenerateSettlements)
            Console.WriteLine($"[port-warband] 读取: {module.Parties.Count} 队伍，{module.Parties.Count(p => p.IsSettlement)} 定居点");
        if (options.GenerateQuests)
            Console.WriteLine($"[port-warband] 读取: {module.Quests.Count} 任务");
        if (options.GenerateMenus)
            Console.WriteLine($"[port-warband] 读取: {module.Menus.Count} 菜单");
        if (options.GenerateDialogStates)
            Console.WriteLine($"[port-warband] 读取: {module.DialogStates.Count} 对话状态");
        if (options.GenerateScripts)
            Console.WriteLine($"[port-warband] 读取: {module.Scripts.Count} 脚本");

        var result = WarbandPort.Convert(module, outputRoot, options, toolName);
        var report = result.Report;

        Console.WriteLine($"[port-warband] 物品: {report.Counts.ItemsConverted}/{report.Counts.ItemsRead} -> {RelativePath(result.ItemsFile, outputRoot)}");
        Console.WriteLine($"[port-warband] 兵种: {report.Counts.TroopsConverted}/{report.Counts.TroopsRead} -> {RelativePath(result.NpcFile, outputRoot)}");
        Console.WriteLine($"[port-warband] 文化: {report.Counts.CulturesGenerated} -> {RelativePath(result.CulturesFile, outputRoot)}");
        if (report.Counts.SkillsRead > 0)
            Console.WriteLine($"[port-warband] 技能: {report.Counts.SkillsConverted}/{report.Counts.SkillsRead} -> {RelativePath(result.SkillsFile!, outputRoot)}");
        if (report.Counts.PartyTemplatesRead > 0)
            Console.WriteLine($"[port-warband] 党团模板: {report.Counts.PartyTemplatesConverted}/{report.Counts.PartyTemplatesRead} -> {RelativePath(result.PartyTemplatesFile!, outputRoot)}");
        if (report.Counts.SettlementsRead > 0)
            Console.WriteLine($"[port-warband] 定居点: {report.Counts.SettlementsConverted}/{report.Counts.SettlementsRead} -> {RelativePath(result.SettlementsFile!, outputRoot)}");
        if (report.Counts.QuestsRead > 0)
            Console.WriteLine($"[port-warband] 任务: {report.Counts.QuestsConverted}/{report.Counts.QuestsRead} -> {RelativePath(result.QuestsFile!, outputRoot)}");
        if (report.Counts.MenusRead > 0)
            Console.WriteLine($"[port-warband] 菜单: {report.Counts.MenusConverted}/{report.Counts.MenusRead} -> {RelativePath(result.MenusFile!, outputRoot)}");
        if (report.Counts.DialogStatesRead > 0)
            Console.WriteLine($"[port-warband] 对话状态: {report.Counts.DialogStatesConverted}/{report.Counts.DialogStatesRead} -> {RelativePath(result.DialogStatesFile!, outputRoot)}");
        if (report.Counts.ScriptsRead > 0)
            Console.WriteLine($"[port-warband] 脚本: {report.Counts.ScriptsConverted}/{report.Counts.ScriptsRead} -> {RelativePath(result.ScriptsFile!, outputRoot)}");
        if (result.KingdomsFile != null)
            Console.WriteLine($"[port-warband] 王国: {report.Counts.KingdomsGenerated} -> {RelativePath(result.KingdomsFile, outputRoot)}");
        Console.WriteLine($"[port-warband] 装备条目: {report.Counts.EquipmentEntries}，升级链接: {report.Counts.UpgradeLinks}");
        Console.WriteLine($"[port-warband] 跳过: {report.Counts.ItemsSkipped} 物品，{report.Counts.TroopsSkipped} 兵种");
        if (report.Counts.MissingReferences > 0)
            Console.WriteLine($"[port-warband] 缺失/无效引用: {report.Counts.MissingReferences}");
        if (report.Counts.Warnings > 0)
        {
            Console.WriteLine($"[port-warband] 警告: {report.Counts.Warnings}");
            foreach (var warning in report.Warnings.Take(5))
                Console.WriteLine($"  - {warning}");
            if (report.Warnings.Count > 5)
                Console.WriteLine("  - ...");
        }
        Console.WriteLine($"[port-warband] 报告: {RelativePath(result.ReportFile, outputRoot)}（{new FileInfo(result.ReportFile).Length} 字节）");
        if (result.InstalledModuleDir != null)
            Console.WriteLine($"[port-warband] 已安装: {result.InstalledModuleDir}");
        if (result.ModuleInfoFile != null)
            Console.WriteLine($"[port-warband] ModuleInfo: {RelativePath(result.ModuleInfoFile, outputRoot)}");
        if (result.LanguagesDir != null)
            Console.WriteLine($"[port-warband] 语言: {RelativePath(result.LanguagesDir, outputRoot)}");
        Console.WriteLine("[port-warband] 完成。");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"错误: 转换失败: {ex.Message}");
        return 1;
    }
}

int RunTest(string[] args)
{
    string? testProject = null;
    string? configuration = "Release";
    string? filter = null;

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--project" or "-p":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个测试项目路径。");
                    return 1;
                }
                testProject = args[++i];
                break;
            case "--configuration" or "-c":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个配置名。");
                    return 1;
                }
                configuration = args[++i];
                break;
            case "--filter" or "-f":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个测试过滤器。");
                    return 1;
                }
                filter = args[++i];
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    // 默认测试项目路径：尝试多个候选位置
    var candidateProjects = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "blmodtool", "tests", "BlModTool.Cli.Tests", "BlModTool.Cli.Tests.csproj"),
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "BlModTool.Cli.Tests", "BlModTool.Cli.Tests.csproj"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "BlModTool.Cli.Tests", "BlModTool.Cli.Tests.csproj"),
    };
    foreach (var candidate in candidateProjects)
    {
        if (candidate == null)
            continue;
        var full = Path.GetFullPath(candidate!);
        if (File.Exists(full))
        {
            testProject = full;
            break;
        }
    }
    if (testProject == null)
    {
        Console.Error.WriteLine("错误: 找不到默认测试项目路径。请使用 --project 指定。");
        return 1;
    }
    testProject = Path.GetFullPath(testProject);

    if (!File.Exists(testProject))
    {
        Console.Error.WriteLine($"错误: 测试项目不存在: {testProject}");
        return 1;
    }

    Console.WriteLine($"[test] 测试项目: {testProject}");
    Console.WriteLine($"[test] 配置: {configuration}");

    // 设置必要的环境变量（与构建脚本一致）
    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
    var dotnetExe = string.IsNullOrEmpty(dotnetRoot)
        ? "dotnet"
        : Path.Combine(dotnetRoot, "dotnet");

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = dotnetExe,
        Arguments = $"test \"{testProject}\" -c {configuration}" + (filter != null ? $" --filter \"{filter}\"" : ""),
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Directory.GetCurrentDirectory()
    };

    // 设置必要的环境变量（与构建脚本一致）
    if (!string.IsNullOrEmpty(dotnetRoot))
        psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
    var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
    if (!string.IsNullOrEmpty(tmpDir))
        psi.EnvironmentVariables["TMPDIR"] = tmpDir;

    using var process = System.Diagnostics.Process.Start(psi);
    if (process == null)
    {
        Console.Error.WriteLine("错误: 无法启动 dotnet test 进程。");
        return 1;
    }

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    Console.Write(output);
    if (!string.IsNullOrEmpty(error))
        Console.Error.Write(error);

    return process.ExitCode;
}

int RunPackage(string[] args)
{
    string? inputDir = null;
    string? outputFile = null;
    string format = "zip";

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--input" or "-i":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个模块目录。");
                    return 1;
                }
                inputDir = args[++i];
                break;
            case "--output" or "-o":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要一个输出文件路径。");
                    return 1;
                }
                outputFile = args[++i];
                break;
            case "--format" or "-f":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"错误: {arg} 需要 zip 或 mb。");
                    return 1;
                }
                format = args[++i].ToLowerInvariant();
                if (format != "zip" && format != "mb")
                {
                    Console.Error.WriteLine($"错误: 不支持的格式: {format}，仅支持 zip 或 mb。");
                    return 1;
                }
                break;
            default:
                Console.Error.WriteLine($"错误: 未知参数: {arg}");
                return 1;
        }
    }

    if (string.IsNullOrEmpty(inputDir))
    {
        Console.Error.WriteLine("错误: package 命令需要 --input <模块目录>");
        return 1;
    }

    inputDir = Path.GetFullPath(inputDir);
    if (!Directory.Exists(inputDir))
    {
        Console.Error.WriteLine($"错误: 目录不存在: {inputDir}");
        return 1;
    }

    if (string.IsNullOrEmpty(outputFile))
    {
        var ext = format == "mb" ? ".mb" : ".zip";
        outputFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Path.GetFileName(inputDir)}{ext}");
    }
    outputFile = Path.GetFullPath(outputFile);

    var outputDir = Path.GetDirectoryName(outputFile);
    if (!string.IsNullOrEmpty(outputDir))
        Directory.CreateDirectory(outputDir);

    Console.WriteLine($"[package] 输入: {inputDir}");
    Console.WriteLine($"[package] 输出: {outputFile}");
    Console.WriteLine($"[package] 格式: {format}");

    try
    {
        if (File.Exists(outputFile))
            File.Delete(outputFile);

        WarbandPort.PackageModule(inputDir, outputFile);

        var info = new FileInfo(outputFile);
        Console.WriteLine($"[package] 打包完成: {info.Length:N0} 字节，{Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories).Length} 个文件");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"错误: 打包失败: {ex.Message}");
        return 1;
    }
}

int RunWizard(string[] args)
{
    Console.WriteLine($"BlModTool v{version} — 交互式向导");
    Console.WriteLine("将引导你完成 Warband → Bannerlord 模组转换。");
    Console.WriteLine();

    var options = new WarbandPortOptions();
    var cliArgs = new List<string> { "port-warband" };

    // 1. Native 目录
    string? nativeDir = null;
    while (true)
    {
        Console.Write("战团 Native 目录 [/home/ou/Projects/re-mb-1011/Modules/Native]: ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input))
            input = "/home/ou/Projects/re-mb-1011/Modules/Native";
        if (Directory.Exists(input))
        {
            nativeDir = Path.GetFullPath(input);
            break;
        }
        Console.Error.WriteLine($"  目录不存在: {input}");
    }
    cliArgs.Add($"--native");
    cliArgs.Add(nativeDir);

    // 2. 输出目录
    Console.Write("输出目录 [./warband-port]: ");
    var outInput = Console.ReadLine()?.Trim();
    var outputDir = string.IsNullOrEmpty(outInput) ? "./warband-port" : outInput;
    outputDir = Path.GetFullPath(outputDir);
    cliArgs.Add($"--output");
    cliArgs.Add(outputDir);

    // 3. 模块名称
    Console.Write("模块名称 [WarbandPort]: ");
    var moduleName = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(moduleName))
        moduleName = "WarbandPort";
    options.ModuleName = moduleName;

    // 4. 功能选项
    bool AskYesNo(string prompt, bool defaultValue)
    {
        var defText = defaultValue ? "Y/n" : "y/N";
        Console.Write($"{prompt} [{defText}]: ");
        var ans = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(ans))
            return defaultValue;
        return ans.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               ans.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    if (AskYesNo("生成 SubModule.xml?", true)) { options.GenerateSubModule = true; cliArgs.Add("--submodule"); }
    if (AskYesNo("生成 README.md?", true)) { options.GenerateReadme = true; cliArgs.Add("--readme"); }
    if (AskYesNo("转换党团模板 (party_templates)?", true)) { options.GeneratePartyTemplates = true; cliArgs.Add("--party-templates"); }
    if (AskYesNo("转换定居点 (settlements)?", true)) { options.GenerateSettlements = true; cliArgs.Add("--settlements"); }
    if (AskYesNo("转换任务 (quests)?", true)) { options.GenerateQuests = true; cliArgs.Add("--quests"); }
    if (AskYesNo("转换菜单 (menus)?", true)) { options.GenerateMenus = true; cliArgs.Add("--menus"); }
    if (AskYesNo("转换对话状态 (dialog_states)?", true)) { options.GenerateDialogStates = true; cliArgs.Add("--dialog-states"); }
    if (AskYesNo("生成脚本代码 (scripts)?", true)) { options.GenerateScripts = true; cliArgs.Add("--scripts"); }
    if (AskYesNo("包含英雄兵种 (--include-heroes)?", false)) { options.IncludeHeroes = true; cliArgs.Add("--include-heroes"); }
    if (AskYesNo("生成王国骨架 (--emit-kingdoms)?", false)) { options.EmitKingdoms = true; cliArgs.Add("--emit-kingdoms"); }
    if (AskYesNo("保留原始网格名 (--keep-meshes)?", false)) { options.KeepMeshes = true; cliArgs.Add("--keep-meshes"); }

    // 5. 安装选项
    if (AskYesNo("安装到 Bannerlord Modules 目录?", false))
    {
        Console.Write("Bannerlord 安装目录: ");
        var bannerlordDir = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(bannerlordDir) && Directory.Exists(bannerlordDir))
        {
            options.Install = true;
            options.BannerlordDir = Path.GetFullPath(bannerlordDir);
            cliArgs.Add($"--install");
            cliArgs.Add($"--bannerlord-dir");
            cliArgs.Add(options.BannerlordDir);
            cliArgs.Add($"--module-name");
            cliArgs.Add(moduleName);
        }
        else
        {
            Console.Error.WriteLine("  目录无效，跳过安装。");
        }
    }

    Console.WriteLine();
    Console.WriteLine("汇总参数:");
    Console.WriteLine($"  {string.Join(" ", cliArgs)}");
    Console.WriteLine();

    if (!AskYesNo("确认开始转换?", true))
    {
        Console.WriteLine("已取消。");
        return 0;
    }

    Console.WriteLine();
    return RunPortWarband(cliArgs.ToArray());
}

static string RelativePath(string path, string root)
    => Path.GetRelativePath(root, path).Replace('\\', '/');

static ApiSnapshot LoadSnapshot(string path)
{
    var json = File.ReadAllText(path);
    using var document = JsonDocument.Parse(json);
    var schemaVersion = document.RootElement.TryGetProperty("schemaVersion", out var schemaElement)
        ? schemaElement.GetString()
        : null;

    if (schemaVersion != ApiSnapshot.CurrentSchemaVersion)
    {
        throw new InvalidDataException(
            $"快照 schemaVersion={schemaVersion ?? "(无)"}，当前工具需要 {ApiSnapshot.CurrentSchemaVersion}。" +
            "请用当前版本重新运行 api-snapshot。");
    }

    var snapshot = JsonSerializer.Deserialize<ApiSnapshot>(json, ApiJson.Options);
    if (snapshot == null)
        throw new InvalidDataException("快照内容为空或格式无效。");

    return snapshot;
}

void PrintHelp()
{
    Console.WriteLine($"{toolName} v{version}");
    Console.WriteLine("Bannerlord Modder Toolkit — static analysis & porting tools");
    Console.WriteLine();
    Console.WriteLine("用法:");
    Console.WriteLine("  BlModTool.Cli [命令] [选项]");
    Console.WriteLine();
    Console.WriteLine("命令:");
    Console.WriteLine("  decompile   反编译 TaleWorlds DLL 为参考 C# 源码 (M1)");
    Console.WriteLine("    --input, -i <路径>     要反编译的 DLL 路径（必填）");
    Console.WriteLine("    --out, -o <目录>       输出根目录（默认 ./decompiled/<程序集名>）");
    Console.WriteLine("    --recursive, -r        递归处理同目录下被引用的 TaleWorlds.*.dll");
    Console.WriteLine("    --filter, -f <前缀>    只输出指定命名空间（可逗号分隔或重复使用）");
    Console.WriteLine("    --verbose              打印每个反编译的类型");
    Console.WriteLine("  api-snapshot  导出公共 API 快照为 JSON (M2)");
    Console.WriteLine("    --input, -i <路径>     单个 DLL 或包含 TaleWorlds*.dll 的目录（必填）");
    Console.WriteLine("    --out, -o <文件>       输出 JSON（默认 ./snapshots/<名称>.json）");
    Console.WriteLine("    --recursive, -r        单 DLL 输入时递归处理引用链");
    Console.WriteLine("    --filter, -f <前缀>    只导出指定命名空间");
    Console.WriteLine("    --verbose              列出导出的类型");
    Console.WriteLine("  api-diff      比较两个 API 快照并报告破坏性变更 (M2)");
    Console.WriteLine("    --old <快照.json>      旧版本快照（必填）");
    Console.WriteLine("    --new <快照.json>      新版本快照（必填）");
    Console.WriteLine("    --breaking-only        只显示破坏性变更");
    Console.WriteLine("    --json                 输出 JSON 格式");
    Console.WriteLine("    --fail-on-breaking     存在破坏性变更时退出码为 1");
    Console.WriteLine("  port-warband  战团(WB)物品/兵种/阵营/技能数据转霸主(BL) XML (M3-M10)");
    Console.WriteLine("    --native, -n <目录>    战团 Native 目录（包含 item_kinds1.txt/troops.txt/factions.txt/skills.txt，必填）");
    Console.WriteLine("    --output, -o <目录>    输出模块目录（默认 ./warband-port）");
    Console.WriteLine("    --include-heroes       包含英雄兵种（默认跳过）");
    Console.WriteLine("    --emit-kingdoms        生成骨架 spkingdoms.xml（默认不生成）");
    Console.WriteLine("    --keep-meshes          保留战团原始网格名（默认使用霸主回退网格）");
    Console.WriteLine("    --readme               生成 README.md（包含分类统计与 NPC 角色模板）");
    Console.WriteLine("    --submodule            生成 SubModule.xml（注册 Items/SPCultures/NPCCharacters/Skills）");
    Console.WriteLine("    --install              安装模块到 Bannerlord Modules 目录（需 --bannerlord-dir）");
    Console.WriteLine("    --bannerlord-dir <目录> Bannerlord 安装根目录（--install 时必填）");
    Console.WriteLine("    --module-name <名称>   模块名称（默认 WarbandPort）");
    Console.WriteLine("    --party-templates      转换战团 party_templates.txt 为霸王 partyTemplates.xml");
    Console.WriteLine("    --settlements          转换战团 parties.txt 为霸王 settlements.xml（城镇/城堡/村庄）");
    Console.WriteLine("    --quests               转换战团 quests.txt 为霸王 quests.xml");
    Console.WriteLine("    --menus                转换战团 menus.txt 为霸王 menus.xml");
    Console.WriteLine("    --dialog-states        转换战团 dialog_states.txt 为霸王 dialog_states.xml");
    Console.WriteLine("    --scripts              生成脚本 C# 代码 (scripts.txt → warband_scripts.cs) (M16)");
    Console.WriteLine("  package   将模块目录打包为 .zip/.mb 分发包 (M17)");
    Console.WriteLine("    --input, -i <目录>     要打包的模块目录（必填）");
    Console.WriteLine("    --output, -o <文件>    输出打包文件路径（默认 ./<目录名>.zip）");
    Console.WriteLine("    --format, -f <zip|mb>  打包格式，mb 本质为 zip 重命名（默认 zip）");
    Console.WriteLine("  wizard    交互式向导，逐步收集参数并执行 port-warband (M18)");
    Console.WriteLine("  test                     运行 xunit 端到端测试 (M4/M8)");
    Console.WriteLine("    --project, -p <路径>   测试项目路径（自动探测）");
    Console.WriteLine("    --configuration, -c    构建配置（默认 Release）");
    Console.WriteLine("    --filter, -f <表达式>  xunit 测试过滤器");
    Console.WriteLine();
    Console.WriteLine("选项:");
    Console.WriteLine("  --version, -v   显示版本");
    Console.WriteLine("  --help, -h      显示帮助");
}

static List<string> GetAssemblyReferences(string path)
{
    var result = new List<string>();

    using var stream = File.OpenRead(path);
    using var peReader = new PEReader(stream);
    if (!peReader.HasMetadata)
        return result;

    var reader = peReader.GetMetadataReader();
    foreach (var handle in reader.AssemblyReferences)
    {
        var reference = reader.GetAssemblyReference(handle);
        result.Add(reader.GetString(reference.Name));
    }

    return result;
}

static string MakeUniqueName(string baseName, HashSet<string> usedNames)
{
    if (usedNames.Add(baseName))
        return baseName;

    for (int i = 2; ; i++)
    {
        var candidate = $"{baseName}_{i}";
        if (usedNames.Add(candidate))
            return candidate;
    }
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var sb = new StringBuilder();
    foreach (var c in name)
    {
        if (Array.IndexOf(invalid, c) >= 0 || c == '<' || c == '>' || c == ':' || c == '"' || c == '/' || c == '\\' || c == '|' || c == '?' || c == '*')
            sb.Append('_');
        else
            sb.Append(c);
    }
    return sb.ToString();
}
