using System.Text;

namespace BlModTool.Cli;

public sealed class ApiDiffEntry
{
    public string Kind { get; set; } = "";
    public string Severity { get; set; } = "";
    public bool Breaking { get; set; }
    public string Assembly { get; set; } = "";
    public string? Type { get; set; }
    public string Message { get; set; } = "";
}

public sealed class ApiDiffResult
{
    public string OldSnapshot { get; set; } = "";
    public string NewSnapshot { get; set; } = "";
    public int BreakingChanges { get; set; }
    public int NonBreakingChanges { get; set; }
    public List<ApiDiffEntry> Entries { get; set; } = new();
}

public static class ApiDiffEngine
{
    public static ApiDiffResult Compare(ApiSnapshot oldSnapshot, ApiSnapshot newSnapshot,
        string oldLabel, string newLabel)
    {
        var result = new ApiDiffResult
        {
            OldSnapshot = oldLabel,
            NewSnapshot = newLabel
        };

        var oldAssemblies = oldSnapshot.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var newAssemblies = newSnapshot.Assemblies.ToDictionary(a => a.Name, StringComparer.Ordinal);

        var assemblyNames = oldAssemblies.Keys
            .Union(newAssemblies.Keys)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var assemblyName in assemblyNames)
        {
            oldAssemblies.TryGetValue(assemblyName, out var oldAssembly);
            newAssemblies.TryGetValue(assemblyName, out var newAssembly);

            if (oldAssembly == null)
            {
                Add(result, "assembly", "additive", assemblyName, null,
                    $"新增程序集: {assemblyName}（{newAssembly!.Types.Count} 个类型）");
                continue;
            }

            if (newAssembly == null)
            {
                Add(result, "assembly", "breaking", assemblyName, null,
                    $"程序集已移除: {assemblyName}（{oldAssembly.Types.Count} 个类型）");
                continue;
            }

            if (!string.Equals(oldAssembly.Version, newAssembly.Version, StringComparison.Ordinal))
            {
                Add(result, "assembly-version", "changed", assemblyName, null,
                    $"程序集版本变更: {oldAssembly.Version} -> {newAssembly.Version}");
            }

            CompareTypes(result, oldAssembly, newAssembly);
        }

        result.BreakingChanges = result.Entries.Count(e => e.Breaking);
        result.NonBreakingChanges = result.Entries.Count - result.BreakingChanges;
        return result;
    }

    public static string FormatText(ApiDiffResult result, bool breakingOnly)
    {
        var builder = new StringBuilder();
        builder.AppendLine("API 差异报告");
        builder.AppendLine($"旧快照: {result.OldSnapshot}");
        builder.AppendLine($"新快照: {result.NewSnapshot}");
        builder.AppendLine($"破坏性变更: {result.BreakingChanges}");
        builder.AppendLine($"非破坏性变更: {result.NonBreakingChanges}");
        builder.AppendLine();

        if (result.Entries.Count == 0)
        {
            builder.AppendLine("两个快照的公共 API 完全一致。");
            return builder.ToString();
        }

        var entries = result.Entries.Where(entry => !breakingOnly || entry.Breaking).ToList();
        if (entries.Count == 0)
        {
            builder.AppendLine("没有符合过滤条件的差异。");
            return builder.ToString();
        }

        foreach (var group in entries.GroupBy(entry => entry.Assembly))
        {
            builder.AppendLine($"[{group.Key}]");
            foreach (var entry in group)
            {
                var label = entry.Severity switch
                {
                    "breaking" => "破坏",
                    "additive" => "新增",
                    _ => "变更"
                };
                builder.AppendLine($"  [{label}] {entry.Message}");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void CompareTypes(ApiDiffResult result, ApiAssembly oldAssembly, ApiAssembly newAssembly)
    {
        var oldTypes = new Dictionary<string, ApiType>(StringComparer.Ordinal);
        foreach (var type in oldAssembly.Types)
            oldTypes[TypeKey(type)] = type;

        var newTypes = new Dictionary<string, ApiType>(StringComparer.Ordinal);
        foreach (var type in newAssembly.Types)
            newTypes[TypeKey(type)] = type;

        var typeKeys = oldTypes.Keys
            .Union(newTypes.Keys)
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in typeKeys)
        {
            oldTypes.TryGetValue(key, out var oldType);
            newTypes.TryGetValue(key, out var newType);

            if (oldType == null)
            {
                Add(result, "type", "additive", oldAssembly.Name, TypeDisplay(newType!),
                    $"新增类型: {TypeDisplay(newType!)}");
                continue;
            }

            if (newType == null)
            {
                Add(result, "type", "breaking", oldAssembly.Name, TypeDisplay(oldType),
                    $"类型已移除: {TypeDisplay(oldType)}");
                continue;
            }

            CompareType(result, oldAssembly.Name, oldType, newType);
        }
    }

    private static void CompareType(ApiDiffResult result, string assemblyName, ApiType oldType, ApiType newType)
    {
        var typeName = TypeDisplay(oldType);

        if (!string.Equals(oldType.Kind, newType.Kind, StringComparison.Ordinal))
        {
            Add(result, "type", "breaking", assemblyName, typeName,
                $"类型种类变更: {typeName}: {oldType.Kind} -> {newType.Kind}");
        }

        CompareVisibility(result, "type", assemblyName, typeName, oldType.Visibility, newType.Visibility,
            "类型可见性", typeName);

        CompareFlag(result, assemblyName, typeName, oldType.Abstract, newType.Abstract,
            "类型", typeName, "abstract");
        CompareFlag(result, assemblyName, typeName, oldType.Sealed, newType.Sealed,
            "类型", typeName, "sealed");
        CompareFlag(result, assemblyName, typeName, oldType.Static, newType.Static,
            "类型", typeName, "static");

        if (!string.Equals(oldType.BaseType, newType.BaseType, StringComparison.Ordinal))
        {
            Add(result, "type", "breaking", assemblyName, typeName,
                $"基类变更: {typeName}: {oldType.BaseType ?? "(无)"} -> {newType.BaseType ?? "(无)"}");
        }

        var oldInterfaces = new HashSet<string>(oldType.Interfaces, StringComparer.Ordinal);
        var newInterfaces = new HashSet<string>(newType.Interfaces, StringComparer.Ordinal);

        foreach (var removed in oldInterfaces.Except(newInterfaces).OrderBy(x => x, StringComparer.Ordinal))
        {
            Add(result, "type", "breaking", assemblyName, typeName,
                $"接口移除: {typeName}: {removed}");
        }

        foreach (var added in newInterfaces.Except(oldInterfaces).OrderBy(x => x, StringComparer.Ordinal))
        {
            Add(result, "type", "additive", assemblyName, typeName,
                $"接口新增: {typeName}: {added}");
        }

        CompareAttributes(result, "type", assemblyName, typeName,
            oldType.Attributes, newType.Attributes, "类型");
        CompareGenericParameters(result, "type", assemblyName, typeName,
            oldType.TypeParameters, newType.TypeParameters, "类型");

        CompareMembers(result, assemblyName, oldType, newType);
    }

    private static void CompareMembers(ApiDiffResult result, string assemblyName,
        ApiType oldType, ApiType newType)
    {
        var oldMembers = new Dictionary<string, ApiMember>(StringComparer.Ordinal);
        foreach (var member in oldType.Members)
            oldMembers[MemberKey(member)] = member;

        var newMembers = new Dictionary<string, ApiMember>(StringComparer.Ordinal);
        foreach (var member in newType.Members)
            newMembers[MemberKey(member)] = member;

        var memberKeys = oldMembers.Keys
            .Union(newMembers.Keys)
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in memberKeys)
        {
            oldMembers.TryGetValue(key, out var oldMember);
            newMembers.TryGetValue(key, out var newMember);

            if (oldMember == null)
            {
                Add(result, "member", "additive", assemblyName, TypeDisplay(newType),
                    $"成员已新增: {MemberDisplay(newType, newMember!)}");
                continue;
            }

            if (newMember == null)
            {
                Add(result, "member", "breaking", assemblyName, TypeDisplay(oldType),
                    $"成员已移除: {MemberDisplay(oldType, oldMember)}");
                continue;
            }

            CompareMember(result, assemblyName, oldType, newType, oldMember, newMember);
        }
    }

    private static void CompareMember(ApiDiffResult result, string assemblyName,
        ApiType oldType, ApiType newType, ApiMember oldMember, ApiMember newMember)
    {
        var display = MemberDisplay(oldType, oldMember);

        CompareVisibility(result, "member", assemblyName, display, oldMember.Visibility, newMember.Visibility,
            "成员可见性", display);

        CompareFlag(result, assemblyName, display, oldMember.Static, newMember.Static,
            "成员", display, "static");
        CompareFlag(result, assemblyName, display, oldMember.Abstract, newMember.Abstract,
            "成员", display, "abstract");
        CompareFlag(result, assemblyName, display, oldMember.Virtual, newMember.Virtual,
            "成员", display, "virtual");

        var locator = MemberLocator(oldType, oldMember);

        CompareAttributes(result, "member", assemblyName, locator,
            oldMember.Attributes, newMember.Attributes, "成员");
        CompareGenericParameters(result, "member", assemblyName, locator,
            oldMember.TypeParameters, newMember.TypeParameters, "成员");

        if (oldMember.Kind == "method" &&
            !string.Equals(oldMember.ReturnType, newMember.ReturnType, StringComparison.Ordinal))
        {
            Add(result, "member", "breaking", assemblyName, display,
                $"返回类型变更: {locator}: {oldMember.ReturnType} -> {newMember.ReturnType}");
        }

        if (oldMember.Kind == "field")
        {
            if (!string.Equals(oldMember.FieldType, newMember.FieldType, StringComparison.Ordinal))
            {
                Add(result, "member", "breaking", assemblyName, display,
                    $"字段类型变更: {locator}: {oldMember.FieldType} -> {newMember.FieldType}");
            }

            if (oldMember.ReadOnly != newMember.ReadOnly)
            {
                Add(result, "member", newMember.ReadOnly ? "breaking" : "additive", assemblyName, display,
                    newMember.ReadOnly
                        ? $"字段变为 readonly: {locator}"
                        : $"字段取消 readonly: {locator}");
            }

            if (oldMember.Const && newMember.Const &&
                !string.Equals(oldMember.ConstantValue, newMember.ConstantValue, StringComparison.Ordinal))
            {
                Add(result, "member", "changed", assemblyName, display,
                    $"常量值变更: {locator}: {oldMember.ConstantValue} -> {newMember.ConstantValue}");
            }
        }

        if (oldMember.Kind == "property")
        {
            if (!string.Equals(oldMember.PropertyType, newMember.PropertyType, StringComparison.Ordinal))
            {
                Add(result, "member", "breaking", assemblyName, display,
                    $"属性类型变更: {locator}: {oldMember.PropertyType} -> {newMember.PropertyType}");
            }

            CompareAccessor(result, "属性", "getter", assemblyName, display,
                oldMember.HasGetter, newMember.HasGetter, oldMember.GetVisibility, newMember.GetVisibility);
            CompareAccessor(result, "属性", "setter", assemblyName, display,
                oldMember.HasSetter, newMember.HasSetter, oldMember.SetVisibility, newMember.SetVisibility);
        }

        if (oldMember.Kind == "event")
        {
            if (!string.Equals(oldMember.EventType, newMember.EventType, StringComparison.Ordinal))
            {
                Add(result, "member", "breaking", assemblyName, display,
                    $"事件类型变更: {locator}: {oldMember.EventType} -> {newMember.EventType}");
            }

            CompareAccessor(result, "事件", "adder", assemblyName, display,
                oldMember.HasAdder, newMember.HasAdder, oldMember.AddVisibility, newMember.AddVisibility);
            CompareAccessor(result, "事件", "remover", assemblyName, display,
                oldMember.HasRemover, newMember.HasRemover, oldMember.RemoveVisibility, newMember.RemoveVisibility);
        }
    }

    private static void CompareAccessor(ApiDiffResult result, string kind, string accessorName,
        string assemblyName, string display, bool oldPresent, bool newPresent,
        string? oldVisibility, string? newVisibility)
    {
        if (oldPresent && !newPresent)
        {
            Add(result, "member", "breaking", assemblyName, display,
                $"{kind} {accessorName} 已移除: {display}");
            return;
        }

        if (!oldPresent && newPresent)
        {
            Add(result, "member", "additive", assemblyName, display,
                $"{kind} {accessorName} 已新增: {display}");
            return;
        }

        if (oldPresent && newPresent)
        {
            CompareVisibility(result, "member", assemblyName, display, oldVisibility, newVisibility,
                $"{kind} {accessorName} 可见性", display);
        }
    }

    private static void CompareVisibility(ApiDiffResult result, string entryKind, string assemblyName,
        string target, string? oldVisibility, string? newVisibility, string what, string display)
    {
        if (string.Equals(oldVisibility, newVisibility, StringComparison.Ordinal))
            return;

        var oldRank = VisibilityRank(oldVisibility);
        var newRank = VisibilityRank(newVisibility);
        if (newRank < oldRank)
        {
            Add(result, entryKind, "breaking", assemblyName, target,
                $"{what}降低: {display}: {oldVisibility ?? "(无)"} -> {newVisibility ?? "(无)"}");
        }
        else if (newRank > oldRank)
        {
            Add(result, entryKind, "additive", assemblyName, target,
                $"{what}提升: {display}: {oldVisibility ?? "(无)"} -> {newVisibility ?? "(无)"}");
        }
    }

    private static void CompareFlag(ApiDiffResult result, string assemblyName, string target,
        bool oldValue, bool newValue, string scope, string display, string flag)
    {
        if (oldValue == newValue)
            return;

        var severity = flag switch
        {
            // static 变化两个方向都会破坏调用方语法。
            "static" => "breaking",
            "abstract" or "sealed" => newValue ? "breaking" : "additive",
            "virtual" => newValue ? "additive" : "breaking",
            _ => newValue ? "additive" : "breaking"
        };

        Add(result, scope, severity, assemblyName, target,
            $"{scope}{(newValue ? "新增" : "移除")} {flag}: {display}");
    }

    private static void CompareAttributes(ApiDiffResult result, string entryKind, string assemblyName,
        string target, List<ApiAttribute> oldAttributes, List<ApiAttribute> newAttributes,
        string ownerLabel)
    {
        var oldMap = new Dictionary<string, ApiAttribute>(StringComparer.Ordinal);
        foreach (var attribute in oldAttributes)
            oldMap[ApiAttributeReader.AttributeKey(attribute)] = attribute;

        var newMap = new Dictionary<string, ApiAttribute>(StringComparer.Ordinal);
        foreach (var attribute in newAttributes)
            newMap[ApiAttributeReader.AttributeKey(attribute)] = attribute;

        foreach (var key in oldMap.Keys.Except(newMap.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            Add(result, entryKind, "changed", assemblyName, target,
                $"{ownerLabel}移除特性: {target}: {ApiAttributeReader.AttributeDisplay(oldMap[key])}");
        }

        foreach (var key in newMap.Keys.Except(oldMap.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            Add(result, entryKind, "changed", assemblyName, target,
                $"{ownerLabel}新增特性: {target}: {ApiAttributeReader.AttributeDisplay(newMap[key])}");
        }
    }

    private static void CompareGenericParameters(ApiDiffResult result, string entryKind,
        string assemblyName, string target, List<ApiGenericParameter> oldParameters,
        List<ApiGenericParameter> newParameters, string ownerLabel)
    {
        var count = Math.Min(oldParameters.Count, newParameters.Count);
        for (var i = 0; i < count; i++)
        {
            var oldParameter = oldParameters[i];
            var newParameter = newParameters[i];
            var oldConstraints = ConstraintSet(oldParameter);
            var newConstraints = ConstraintSet(newParameter);

            var added = newConstraints.Except(oldConstraints).OrderBy(value => value, StringComparer.Ordinal).ToList();
            var removed = oldConstraints.Except(newConstraints).OrderBy(value => value, StringComparer.Ordinal).ToList();

            if (added.Count > 0)
            {
                Add(result, entryKind, "breaking", assemblyName, target,
                    $"{ownerLabel}泛型参数新增约束: {target} 的 {newParameter.Name}: {string.Join(", ", added)}");
            }

            if (removed.Count > 0)
            {
                Add(result, entryKind, "additive", assemblyName, target,
                    $"{ownerLabel}泛型参数移除约束: {target} 的 {newParameter.Name}: {string.Join(", ", removed)}");
            }
        }
    }

    private static HashSet<string> ConstraintSet(ApiGenericParameter parameter)
    {
        var result = new HashSet<string>(parameter.Flags, StringComparer.Ordinal);
        foreach (var constraint in parameter.Constraints)
            result.Add(constraint);
        return result;
    }

    private static void Add(ApiDiffResult result, string kind, string severity, string assemblyName,
        string? type, string message)
    {
        result.Entries.Add(new ApiDiffEntry
        {
            Kind = kind,
            Severity = severity,
            Breaking = severity == "breaking",
            Assembly = assemblyName,
            Type = type,
            Message = message
        });
    }

    private static string TypeKey(ApiType type) => type.Name + "`" + type.TypeParameters.Count;

    internal static string TypeDisplay(ApiType type)
    {
        return type.TypeParameters.Count == 0
            ? type.Name
            : type.Name + "<" +
              string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">";
    }

    private static string MemberKey(ApiMember member)
    {
        return member.Kind switch
        {
            "method" or "constructor" =>
                member.Kind + "|" + member.Name + "`" + member.TypeParameters.Count +
                "(" + ApiSnapshotBuilder.ParameterKey(member) + ")",
            "property" =>
                "property|" + member.Name + "(" + ApiSnapshotBuilder.ParameterKey(member) + ")",
            "field" => "field|" + member.Name,
            "event" => "event|" + member.Name,
            _ => member.Kind + "|" + member.Name
        };
    }

    private static string MemberLocator(ApiType owner, ApiMember member)
    {
        var ownerName = TypeDisplay(owner);
        var genericSuffix = member.TypeParameters.Count > 0
            ? "<" + string.Join(", ", member.TypeParameters.Select(parameter => parameter.Name)) + ">"
            : "";
        var parameters = "(" + FormatParameters(member.Parameters) + ")";

        return member.Kind switch
        {
            "constructor" => ownerName + parameters,
            "method" => ownerName + "." + member.Name + genericSuffix + parameters,
            "property" => ownerName + "." + member.Name +
                          (member.Parameters.Count > 0
                              ? "[" + FormatParameters(member.Parameters) + "]"
                              : ""),
            _ => ownerName + "." + member.Name
        };
    }

    private static string MemberDisplay(ApiType owner, ApiMember member)
    {
        var ownerName = TypeDisplay(owner);
        var genericSuffix = member.TypeParameters.Count > 0
            ? "<" + string.Join(", ", member.TypeParameters.Select(parameter => parameter.Name)) + ">"
            : "";
        var parameters = "(" + FormatParameters(member.Parameters) + ")";

        return member.Kind switch
        {
            "constructor" => ownerName + parameters,
            "method" => ownerName + "." + member.Name + genericSuffix + parameters +
                        " -> " + member.ReturnType,
            "property" => ownerName + "." + member.Name +
                          (member.Parameters.Count > 0
                              ? "[" + FormatParameters(member.Parameters) + "]"
                              : "") +
                          " { " + AccessorText(member) + " } -> " + member.PropertyType,
            "field" => ownerName + "." + member.Name + " : " + member.FieldType +
                       (member.ConstantValue != null ? " = " + member.ConstantValue : ""),
            "event" => ownerName + "." + member.Name + " : event " + member.EventType,
            _ => ownerName + "." + member.Name
        };
    }

    private static string FormatParameters(IEnumerable<ApiParameter> parameters)
    {
        return string.Join(", ", parameters.Select(parameter => parameter.Type));
    }

    private static string AccessorText(ApiMember member)
    {
        var parts = new List<string>();
        if (member.HasGetter)
            parts.Add("get");
        if (member.HasSetter)
            parts.Add("set");
        return string.Join("; ", parts);
    }

    private static int VisibilityRank(string? visibility)
    {
        return visibility switch
        {
            "public" => 3,
            "protected internal" => 2,
            "protected" => 1,
            "internal" => 0,
            _ => -1
        };
    }
}
