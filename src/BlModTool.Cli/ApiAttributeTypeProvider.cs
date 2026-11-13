using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;

namespace BlModTool.Cli;

public sealed class ApiAttributeTypeProvider : ICustomAttributeTypeProvider<string>
{
    private readonly ApiSignatureProvider _signatureProvider;
    private readonly Dictionary<string, PrimitiveTypeCode> _enumUnderlyingTypes;

    public ApiAttributeTypeProvider(MetadataReader reader, ApiSignatureProvider signatureProvider)
    {
        _signatureProvider = signatureProvider;
        _enumUnderlyingTypes = BuildEnumUnderlyingTypes(reader, signatureProvider);
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        => _signatureProvider.GetPrimitiveType(typeCode);

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => MetadataNames.GetTypeDefinitionName(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => MetadataNames.GetTypeReferenceName(reader, handle);

    public string GetTypeFromHandle(EntityHandle handle) => _signatureProvider.GetTypeFromHandle(handle);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetSystemType() => "System.Type";

    public bool IsSystemType(string type) => type == "System.Type";

    public string GetTypeFromSerializedName(string name)
    {
        if (name == null)
            return "null";

        var depth = 0;
        var end = name.Length;
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (character == '[') depth++;
            else if (character == ']') depth--;
            else if (character == ',' && depth == 0)
            {
                end = i;
                break;
            }
        }

        return name[..end].Trim().Replace('+', '.');
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(string type)
        => _enumUnderlyingTypes.GetValueOrDefault(type, PrimitiveTypeCode.Int32);

    private static Dictionary<string, PrimitiveTypeCode> BuildEnumUnderlyingTypes(
        MetadataReader reader, ApiSignatureProvider signatureProvider)
    {
        var result = new Dictionary<string, PrimitiveTypeCode>(StringComparer.Ordinal);

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(typeHandle);
            if (typeDef.BaseType.IsNil ||
                signatureProvider.GetTypeFromHandle(typeDef.BaseType) != "System.Enum")
                continue;

            foreach (var fieldHandle in typeDef.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                if (reader.GetString(field.Name) != "value__")
                    continue;

                var fieldType = field.DecodeSignature(signatureProvider, GenericContext.Empty);
                result[MetadataNames.GetTypeDefinitionName(reader, typeHandle)] = fieldType switch
                {
                    "System.Byte" => PrimitiveTypeCode.Byte,
                    "System.SByte" => PrimitiveTypeCode.SByte,
                    "System.Int16" => PrimitiveTypeCode.Int16,
                    "System.UInt16" => PrimitiveTypeCode.UInt16,
                    "System.Int32" => PrimitiveTypeCode.Int32,
                    "System.UInt32" => PrimitiveTypeCode.UInt32,
                    "System.Int64" => PrimitiveTypeCode.Int64,
                    "System.UInt64" => PrimitiveTypeCode.UInt64,
                    _ => PrimitiveTypeCode.Int32
                };
                break;
            }
        }

        return result;
    }
}

public static class ApiAttributeReader
{
    public static List<ApiAttribute> Read(MetadataReader reader, ApiAttributeTypeProvider provider,
        IEnumerable<CustomAttributeHandle> handles)
    {
        var result = new List<ApiAttribute>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in handles)
        {
            var attribute = reader.GetCustomAttribute(handle);
            var name = GetAttributeTypeName(reader, provider, attribute.Constructor);
            if (IsNoiseAttribute(name))
                continue;

            var apiAttribute = new ApiAttribute { Name = name };

            try
            {
                var value = attribute.DecodeValue(provider);
                foreach (var argument in value.FixedArguments)
                    apiAttribute.Arguments.Add(FormatValue(argument.Type, argument.Value));
                foreach (var argument in value.NamedArguments)
                    apiAttribute.Arguments.Add(argument.Name + "=" + FormatValue(argument.Type, argument.Value));
            }
            catch (Exception)
            {
                apiAttribute.Arguments.Add("<decode-failed>");
            }

            if (seen.Add(AttributeKey(apiAttribute)))
                result.Add(apiAttribute);
        }

        result.Sort((a, b) => string.CompareOrdinal(AttributeKey(a), AttributeKey(b)));
        return result;
    }

    public static string AttributeKey(ApiAttribute attribute)
        => attribute.Name + "(" + string.Join(",", attribute.Arguments) + ")";

    public static string AttributeDisplay(ApiAttribute attribute)
        => "[" + attribute.Name +
           (attribute.Arguments.Count > 0 ? "(" + string.Join(", ", attribute.Arguments) + ")" : "") + "]";

    private static string GetAttributeTypeName(MetadataReader reader, ApiAttributeTypeProvider provider,
        EntityHandle constructor)
    {
        switch (constructor.Kind)
        {
            case HandleKind.MethodDefinition:
            {
                var method = reader.GetMethodDefinition((MethodDefinitionHandle)constructor);
                return MetadataNames.GetTypeDefinitionName(reader, method.GetDeclaringType());
            }
            case HandleKind.MemberReference:
            {
                var memberReference = reader.GetMemberReference((MemberReferenceHandle)constructor);
                return provider.GetTypeFromHandle(memberReference.Parent);
            }
            default:
                return constructor.Kind.ToString();
        }
    }

    private static bool IsNoiseAttribute(string name)
    {
        return name.StartsWith("System.Runtime.CompilerServices.", StringComparison.Ordinal) ||
               name.StartsWith("System.Diagnostics.", StringComparison.Ordinal) ||
               name == "System.Reflection.DefaultMemberAttribute";
    }

    private static string FormatValue(string type, object? value)
    {
        if (value == null)
            return "null";

        if (type == "System.Type")
            return value is string typeName && typeName == "null"
                ? "null"
                : "typeof(" + value + ")";

        if (value is ImmutableArray<CustomAttributeTypedArgument<string>> array)
        {
            return "new[] { " +
                   string.Join(", ", array.Select(element => FormatValue(element.Type, element.Value))) +
                   " }";
        }

        return value switch
        {
            string text => "\"" + EscapeString(text) + "\"",
            char character => "'" + character + "'",
            bool boolean => boolean ? "true" : "false",
            _ => FormatNumberOrEnum(type, value)
        };
    }

    private static string FormatNumberOrEnum(string type, object value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        return type.StartsWith("System.", StringComparison.Ordinal) ||
               type.EndsWith("[]", StringComparison.Ordinal)
            ? text
            : "(" + type + ")" + text;
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
