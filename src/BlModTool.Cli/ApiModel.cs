using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlModTool.Cli;

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = WarbandJsonContext.Default
    };
}

public sealed class ApiSnapshot
{
    public const string CurrentSchemaVersion = "2";

    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Tool { get; set; } = "BlModTool";
    public string ToolVersion { get; set; } = "";
    public string CreatedUtc { get; set; } = "";
    public List<ApiAssembly> Assemblies { get; set; } = new();
}

public sealed class ApiAssembly
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Path { get; set; } = "";
    public List<ApiType> Types { get; set; } = new();
}

public sealed class ApiType
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Visibility { get; set; } = "";
    public bool Abstract { get; set; }
    public bool Sealed { get; set; }
    public bool Static { get; set; }
    public string? BaseType { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public List<ApiGenericParameter> TypeParameters { get; set; } = new();
    public List<ApiAttribute> Attributes { get; set; } = new();
    public List<ApiMember> Members { get; set; } = new();
}

public sealed class ApiGenericParameter
{
    public string Name { get; set; } = "";
    public List<string> Constraints { get; set; } = new();
    public List<string> Flags { get; set; } = new();
}

public sealed class ApiAttribute
{
    public string Name { get; set; } = "";
    public List<string> Arguments { get; set; } = new();
}

public sealed class ApiMember
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Visibility { get; set; } = "";
    public bool Static { get; set; }
    public bool Abstract { get; set; }
    public bool Virtual { get; set; }
    public bool ReadOnly { get; set; }
    public bool Const { get; set; }
    public List<ApiGenericParameter> TypeParameters { get; set; } = new();
    public List<ApiAttribute> Attributes { get; set; } = new();
    public string? ReturnType { get; set; }
    public List<ApiParameter> Parameters { get; set; } = new();

    public string? FieldType { get; set; }
    public string? PropertyType { get; set; }
    public string? EventType { get; set; }

    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string? GetVisibility { get; set; }
    public string? SetVisibility { get; set; }

    public bool HasAdder { get; set; }
    public bool HasRemover { get; set; }
    public bool HasRaiser { get; set; }
    public string? AddVisibility { get; set; }
    public string? RemoveVisibility { get; set; }

    public string? ConstantValue { get; set; }
}

public sealed class ApiParameter
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}
