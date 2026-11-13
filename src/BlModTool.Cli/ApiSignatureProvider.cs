using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace BlModTool.Cli;

public readonly record struct GenericContext(
    ImmutableArray<string> TypeParameters,
    ImmutableArray<string> MethodParameters)
{
    public static GenericContext Empty => new(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
}

public static class MetadataNames
{
    public static string StripArity(string name)
    {
        var index = name.IndexOf('`');
        return index >= 0 ? name[..index] : name;
    }

    public static string GetTypeDefinitionName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var name = StripArity(reader.GetString(typeDef.Name));
        var declaringType = typeDef.GetDeclaringType();
        if (!declaringType.IsNil)
            return GetTypeDefinitionName(reader, declaringType) + "." + name;

        var ns = reader.GetString(typeDef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    public static string GetTypeReferenceName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var typeRef = reader.GetTypeReference(handle);
        var name = StripArity(reader.GetString(typeRef.Name));

        if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
            return GetTypeReferenceName(reader, (TypeReferenceHandle)typeRef.ResolutionScope) + "." + name;

        var ns = reader.GetString(typeRef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    public static string GetNamespace(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var declaringType = typeDef.GetDeclaringType();
        if (!declaringType.IsNil)
            return GetNamespace(reader, declaringType);

        return reader.GetString(typeDef.Namespace);
    }
}

public sealed class ApiSignatureProvider : ISignatureTypeProvider<string, GenericContext>
{
    private readonly MetadataReader _reader;

    public ApiSignatureProvider(MetadataReader reader)
    {
        _reader = reader;
    }

    public string GetTypeFromHandle(EntityHandle handle)
        => GetTypeFromHandle(handle, GenericContext.Empty);

    public string GetTypeFromHandle(EntityHandle handle, GenericContext genericContext)
    {
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => MetadataNames.GetTypeDefinitionName(_reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => MetadataNames.GetTypeReferenceName(_reader, (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => _reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(this, genericContext),
            _ => handle.Kind.ToString()
        };
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.Boolean => "System.Boolean",
            PrimitiveTypeCode.Byte => "System.Byte",
            PrimitiveTypeCode.SByte => "System.SByte",
            PrimitiveTypeCode.Char => "System.Char",
            PrimitiveTypeCode.Int16 => "System.Int16",
            PrimitiveTypeCode.UInt16 => "System.UInt16",
            PrimitiveTypeCode.Int32 => "System.Int32",
            PrimitiveTypeCode.UInt32 => "System.UInt32",
            PrimitiveTypeCode.Int64 => "System.Int64",
            PrimitiveTypeCode.UInt64 => "System.UInt64",
            PrimitiveTypeCode.Single => "System.Single",
            PrimitiveTypeCode.Double => "System.Double",
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.Object => "System.Object",
            PrimitiveTypeCode.IntPtr => "System.IntPtr",
            PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
            PrimitiveTypeCode.Void => "System.Void",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _ => typeCode.ToString()
        };
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => MetadataNames.GetTypeDefinitionName(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => MetadataNames.GetTypeReferenceName(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, GenericContext genericContext,
        TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape)
        => shape.Rank <= 1
            ? elementType + "[*]"
            : elementType + "[" + new string(',', shape.Rank - 1) + "]";

    public string GetByReferenceType(string elementType) => "ref " + elementType;

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        => genericType + "<" + string.Join(", ", typeArguments) + ">";

    public string GetGenericMethodParameter(GenericContext genericContext, int index)
    {
        return !genericContext.MethodParameters.IsDefault &&
               (uint)index < (uint)genericContext.MethodParameters.Length
            ? genericContext.MethodParameters[index]
            : "!!" + index;
    }

    public string GetGenericTypeParameter(GenericContext genericContext, int index)
    {
        return !genericContext.TypeParameters.IsDefault &&
               (uint)index < (uint)genericContext.TypeParameters.Length
            ? genericContext.TypeParameters[index]
            : "!" + index;
    }

    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        var parameters = signature.ParameterTypes.Add(signature.ReturnType);
        return "delegate*<" + string.Join(", ", parameters) + ">";
    }

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;
}
