using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace BlModTool.Cli;

public static class ApiSnapshotBuilder
{
    public static ApiSnapshot Build(IEnumerable<string> dllPaths, IReadOnlyCollection<string> filters,
        string toolVersion)
    {
        var snapshot = new ApiSnapshot
        {
            ToolVersion = toolVersion,
            CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        var seenAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in dllPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var assembly = ReadAssembly(path, filters);
            if (assembly == null || !seenAssemblies.Add(assembly.Name))
                continue;

            snapshot.Assemblies.Add(assembly);
        }

        snapshot.Assemblies.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return snapshot;
    }

    public static ApiAssembly? ReadAssembly(string path, IReadOnlyCollection<string> filters)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return null;

        var reader = peReader.GetMetadataReader();
        if (!reader.IsAssembly)
            return null;

        var assemblyDef = reader.GetAssemblyDefinition();
        var assembly = new ApiAssembly
        {
            Name = reader.GetString(assemblyDef.Name),
            Version = assemblyDef.Version.ToString(),
            Path = Path.GetFullPath(path)
        };

        var provider = new ApiSignatureProvider(reader);
        var attributeProvider = new ApiAttributeTypeProvider(reader, provider);

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = ReadType(reader, provider, attributeProvider, typeHandle, filters);
            if (type != null)
                assembly.Types.Add(type);
        }

        assembly.Types.Sort((a, b) =>
        {
            var result = string.CompareOrdinal(a.Name, b.Name);
            return result != 0 ? result : a.TypeParameters.Count.CompareTo(b.TypeParameters.Count);
        });

        return assembly;
    }

    private static ApiType? ReadType(MetadataReader reader, ApiSignatureProvider provider,
        ApiAttributeTypeProvider attributeProvider, TypeDefinitionHandle handle,
        IReadOnlyCollection<string> filters)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        if (!IsExternallyVisible(reader, handle))
            return null;

        var ns = MetadataNames.GetNamespace(reader, handle);
        if (!NamespaceFilter.Matches(ns, filters))
            return null;

        var attributes = typeDef.Attributes;
        var typeParameters = ReadGenericParameters(reader, provider, typeDef.GetGenericParameters(),
            ImmutableArray<string>.Empty, methodLevel: false);
        var typeParameterNames = typeParameters.Select(parameter => parameter.Name).ToImmutableArray();
        var typeContext = new GenericContext(typeParameterNames, ImmutableArray<string>.Empty);

        var baseType = typeDef.BaseType.IsNil
            ? null
            : provider.GetTypeFromHandle(typeDef.BaseType, typeContext);
        var kind = GetTypeKind(attributes, baseType);

        var type = new ApiType
        {
            Name = MetadataNames.GetTypeDefinitionName(reader, handle),
            Namespace = ns,
            Kind = kind,
            Visibility = GetTypeVisibility(attributes),
            Abstract = (attributes & TypeAttributes.Abstract) != 0,
            Sealed = (attributes & TypeAttributes.Sealed) != 0,
            Static = kind == "class" &&
                     (attributes & TypeAttributes.Abstract) != 0 &&
                     (attributes & TypeAttributes.Sealed) != 0,
            BaseType = baseType,
            TypeParameters = typeParameters,
            Attributes = ApiAttributeReader.Read(reader, attributeProvider, typeDef.GetCustomAttributes())
        };

        foreach (var interfaceHandle in typeDef.GetInterfaceImplementations())
        {
            var implementation = reader.GetInterfaceImplementation(interfaceHandle);
            type.Interfaces.Add(provider.GetTypeFromHandle(implementation.Interface, typeContext));
        }
        type.Interfaces.Sort(StringComparer.Ordinal);

        type.Members.AddRange(ReadMembers(reader, provider, attributeProvider, typeDef, typeParameterNames));
        type.Members.Sort(CompareMembers);

        return type;
    }

    private static List<ApiMember> ReadMembers(MetadataReader reader, ApiSignatureProvider provider,
        ApiAttributeTypeProvider attributeProvider, TypeDefinition typeDef,
        ImmutableArray<string> typeParameters)
    {
        var members = new List<ApiMember>();
        var context = new GenericContext(typeParameters, ImmutableArray<string>.Empty);

        var propertyAccessors = new HashSet<MethodDefinitionHandle>();
        foreach (var propertyHandle in typeDef.GetProperties())
        {
            var accessors = reader.GetPropertyDefinition(propertyHandle).GetAccessors();
            AddIfNotNil(propertyAccessors, accessors.Getter);
            AddIfNotNil(propertyAccessors, accessors.Setter);
            foreach (var other in accessors.Others)
                propertyAccessors.Add(other);
        }

        var eventAccessors = new HashSet<MethodDefinitionHandle>();
        foreach (var eventHandle in typeDef.GetEvents())
        {
            var accessors = reader.GetEventDefinition(eventHandle).GetAccessors();
            AddIfNotNil(eventAccessors, accessors.Adder);
            AddIfNotNil(eventAccessors, accessors.Remover);
            AddIfNotNil(eventAccessors, accessors.Raiser);
            foreach (var other in accessors.Others)
                eventAccessors.Add(other);
        }

        foreach (var methodHandle in typeDef.GetMethods())
        {
            if (propertyAccessors.Contains(methodHandle) || eventAccessors.Contains(methodHandle))
                continue;

            var method = reader.GetMethodDefinition(methodHandle);
            var visibility = GetMethodVisibility(method.Attributes);
            if (visibility == null)
                continue;

            var name = reader.GetString(method.Name);
            if (name.StartsWith('<') || name == ".cctor")
                continue;

            var methodParameters = ReadGenericParameters(reader, provider, method.GetGenericParameters(),
                typeParameters, methodLevel: true);
            var methodTypeParameterNames = methodParameters.Select(parameter => parameter.Name).ToImmutableArray();
            var methodContext = new GenericContext(typeParameters, methodTypeParameterNames);
            var signature = method.DecodeSignature(provider, methodContext);
            var isConstructor = name == ".ctor";

            members.Add(new ApiMember
            {
                Kind = isConstructor ? "constructor" : "method",
                Name = name,
                Visibility = visibility,
                Static = (method.Attributes & MethodAttributes.Static) != 0,
                Abstract = (method.Attributes & MethodAttributes.Abstract) != 0,
                Virtual = (method.Attributes & MethodAttributes.Virtual) != 0 &&
                          (method.Attributes & MethodAttributes.Final) == 0,
                TypeParameters = methodParameters,
                Attributes = ApiAttributeReader.Read(reader, attributeProvider, method.GetCustomAttributes()),
                ReturnType = signature.ReturnType,
                Parameters = ReadParameters(reader, method.GetParameters(), signature.ParameterTypes)
            });
        }

        foreach (var fieldHandle in typeDef.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var visibility = GetFieldVisibility(field.Attributes);
            if (visibility == null)
                continue;

            var name = reader.GetString(field.Name);
            if (name.StartsWith('<'))
                continue;

            members.Add(new ApiMember
            {
                Kind = "field",
                Name = name,
                Visibility = visibility,
                Static = (field.Attributes & FieldAttributes.Static) != 0,
                ReadOnly = (field.Attributes & FieldAttributes.InitOnly) != 0,
                Const = (field.Attributes & FieldAttributes.Literal) != 0,
                Attributes = ApiAttributeReader.Read(reader, attributeProvider, field.GetCustomAttributes()),
                FieldType = field.DecodeSignature(provider, context),
                ConstantValue = ReadConstant(reader, field.GetDefaultValue())
            });
        }

        foreach (var propertyHandle in typeDef.GetProperties())
        {
            var property = reader.GetPropertyDefinition(propertyHandle);
            var accessors = property.GetAccessors();

            var getVisibility = accessors.Getter.IsNil
                ? null
                : GetMethodVisibility(reader.GetMethodDefinition(accessors.Getter).Attributes);
            var setVisibility = accessors.Setter.IsNil
                ? null
                : GetMethodVisibility(reader.GetMethodDefinition(accessors.Setter).Attributes);
            if (getVisibility == null && setVisibility == null)
                continue;

            var signature = property.DecodeSignature(provider, context);
            var accessorAttributes = GetAccessorAttributes(reader, accessors.Getter, accessors.Setter);
            var propertyParameters = !accessors.Getter.IsNil
                ? reader.GetMethodDefinition(accessors.Getter).GetParameters()
                : !accessors.Setter.IsNil
                    ? reader.GetMethodDefinition(accessors.Setter).GetParameters()
                    : default;

            var propertyAttributeHandles = new List<CustomAttributeHandle>();
            AddAttributeHandles(propertyAttributeHandles, property.GetCustomAttributes());
            if (!accessors.Getter.IsNil)
                AddAttributeHandles(propertyAttributeHandles,
                    reader.GetMethodDefinition(accessors.Getter).GetCustomAttributes());
            if (!accessors.Setter.IsNil)
                AddAttributeHandles(propertyAttributeHandles,
                    reader.GetMethodDefinition(accessors.Setter).GetCustomAttributes());

            members.Add(new ApiMember
            {
                Kind = "property",
                Name = reader.GetString(property.Name),
                Visibility = MaxVisibility(getVisibility, setVisibility),
                Static = (accessorAttributes & MethodAttributes.Static) != 0,
                Abstract = (accessorAttributes & MethodAttributes.Abstract) != 0,
                Virtual = (accessorAttributes & MethodAttributes.Virtual) != 0 &&
                          (accessorAttributes & MethodAttributes.Final) == 0,
                Attributes = ApiAttributeReader.Read(reader, attributeProvider, propertyAttributeHandles),
                PropertyType = signature.ReturnType,
                Parameters = ReadParameters(reader, propertyParameters, signature.ParameterTypes),
                HasGetter = getVisibility != null,
                HasSetter = setVisibility != null,
                GetVisibility = getVisibility,
                SetVisibility = setVisibility
            });
        }

        foreach (var eventHandle in typeDef.GetEvents())
        {
            var eventDef = reader.GetEventDefinition(eventHandle);
            var accessors = eventDef.GetAccessors();

            var addVisibility = accessors.Adder.IsNil
                ? null
                : GetMethodVisibility(reader.GetMethodDefinition(accessors.Adder).Attributes);
            var removeVisibility = accessors.Remover.IsNil
                ? null
                : GetMethodVisibility(reader.GetMethodDefinition(accessors.Remover).Attributes);
            if (addVisibility == null && removeVisibility == null)
                continue;

            var accessorAttributes = GetAccessorAttributes(reader, accessors.Adder, accessors.Remover);

            var eventAttributeHandles = new List<CustomAttributeHandle>();
            AddAttributeHandles(eventAttributeHandles, eventDef.GetCustomAttributes());
            if (!accessors.Adder.IsNil)
                AddAttributeHandles(eventAttributeHandles,
                    reader.GetMethodDefinition(accessors.Adder).GetCustomAttributes());
            if (!accessors.Remover.IsNil)
                AddAttributeHandles(eventAttributeHandles,
                    reader.GetMethodDefinition(accessors.Remover).GetCustomAttributes());

            members.Add(new ApiMember
            {
                Kind = "event",
                Name = reader.GetString(eventDef.Name),
                Visibility = MaxVisibility(addVisibility, removeVisibility),
                Static = (accessorAttributes & MethodAttributes.Static) != 0,
                Abstract = (accessorAttributes & MethodAttributes.Abstract) != 0,
                Virtual = (accessorAttributes & MethodAttributes.Virtual) != 0 &&
                          (accessorAttributes & MethodAttributes.Final) == 0,
                Attributes = ApiAttributeReader.Read(reader, attributeProvider, eventAttributeHandles),
                EventType = provider.GetTypeFromHandle(eventDef.Type, context),
                HasAdder = addVisibility != null,
                HasRemover = removeVisibility != null,
                HasRaiser = !accessors.Raiser.IsNil,
                AddVisibility = addVisibility,
                RemoveVisibility = removeVisibility
            });
        }

        return members;
    }

    private static MethodAttributes GetAccessorAttributes(MetadataReader reader,
        MethodDefinitionHandle primary, MethodDefinitionHandle secondary)
    {
        var handle = !primary.IsNil ? primary : secondary;
        return reader.GetMethodDefinition(handle).Attributes;
    }

    private static List<ApiParameter> ReadParameters(MetadataReader reader,
        ParameterHandleCollection parameterHandles, ImmutableArray<string> types)
    {
        var names = new Dictionary<int, string>();
        foreach (var parameterHandle in parameterHandles)
        {
            var parameter = reader.GetParameter(parameterHandle);
            if (parameter.SequenceNumber > 0)
                names[parameter.SequenceNumber] = reader.GetString(parameter.Name);
        }

        var result = new List<ApiParameter>(types.Length);
        for (var i = 0; i < types.Length; i++)
        {
            result.Add(new ApiParameter
            {
                Type = types[i],
                Name = names.GetValueOrDefault(i + 1, "")
            });
        }

        return result;
    }

    private static List<ApiGenericParameter> ReadGenericParameters(MetadataReader reader,
        ApiSignatureProvider provider, GenericParameterHandleCollection handles,
        ImmutableArray<string> outerTypeParameters, bool methodLevel)
    {
        var parameterHandles = new List<GenericParameterHandle>(handles.Count);
        var parameterNames = new List<string>(handles.Count);

        foreach (var handle in handles)
        {
            parameterHandles.Add(handle);
            parameterNames.Add(reader.GetString(reader.GetGenericParameter(handle).Name));
        }

        var context = methodLevel
            ? new GenericContext(outerTypeParameters, parameterNames.ToImmutableArray())
            : new GenericContext(parameterNames.ToImmutableArray(), ImmutableArray<string>.Empty);

        var result = new List<ApiGenericParameter>(parameterHandles.Count);
        foreach (var handle in parameterHandles)
        {
            var genericParameter = reader.GetGenericParameter(handle);
            var parameter = new ApiGenericParameter
            {
                Name = reader.GetString(genericParameter.Name)
            };

            var flags = genericParameter.Attributes;
            if ((flags & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                parameter.Flags.Add("class");
            if ((flags & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                parameter.Flags.Add("struct");
            if ((flags & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                parameter.Flags.Add("new()");
            if (((int)flags & 0x20) != 0)
                parameter.Flags.Add("allows ref struct");

            foreach (var constraintHandle in genericParameter.GetConstraints())
            {
                var constraint = reader.GetGenericParameterConstraint(constraintHandle);
                parameter.Constraints.Add(provider.GetTypeFromHandle(constraint.Type, context));
            }
            parameter.Constraints.Sort(StringComparer.Ordinal);

            result.Add(parameter);
        }

        return result;
    }

    private static void AddIfNotNil(HashSet<MethodDefinitionHandle> set, MethodDefinitionHandle handle)
    {
        if (!handle.IsNil)
            set.Add(handle);
    }

    private static void AddAttributeHandles(List<CustomAttributeHandle> target,
        CustomAttributeHandleCollection source)
    {
        foreach (var handle in source)
            target.Add(handle);
    }

    private static bool IsExternallyVisible(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var current = handle;
        while (!current.IsNil)
        {
            var typeDef = reader.GetTypeDefinition(current);
            var visibility = typeDef.Attributes & TypeAttributes.VisibilityMask;
            var declaringType = typeDef.GetDeclaringType();

            if (declaringType.IsNil)
            {
                if (visibility != TypeAttributes.Public)
                    return false;
            }
            else if (visibility != TypeAttributes.NestedPublic &&
                     visibility != TypeAttributes.NestedFamily &&
                     visibility != TypeAttributes.NestedFamORAssem)
            {
                return false;
            }

            current = declaringType;
        }

        return true;
    }

    private static string GetTypeKind(TypeAttributes attributes, string? baseType)
    {
        if ((attributes & TypeAttributes.Interface) != 0)
            return "interface";

        return baseType switch
        {
            "System.Enum" => "enum",
            "System.ValueType" => "struct",
            "System.MulticastDelegate" => "delegate",
            _ => "class"
        };
    }

    private static string GetTypeVisibility(TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.Public or TypeAttributes.NestedPublic => "public",
            TypeAttributes.NestedFamily => "protected",
            TypeAttributes.NestedFamORAssem => "protected internal",
            _ => "internal"
        };
    }

    private static string? GetMethodVisibility(MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) switch
        {
            MethodAttributes.Public => "public",
            MethodAttributes.Family => "protected",
            MethodAttributes.FamORAssem => "protected internal",
            _ => null
        };
    }

    private static string? GetFieldVisibility(FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) switch
        {
            FieldAttributes.Public => "public",
            FieldAttributes.Family => "protected",
            FieldAttributes.FamORAssem => "protected internal",
            _ => null
        };
    }

    private static string MaxVisibility(string? first, string? second)
    {
        return VisibilityRank(first) >= VisibilityRank(second)
            ? first ?? "internal"
            : second ?? "internal";
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

    private static string? ReadConstant(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
            return null;

        var constant = reader.GetConstant(handle);
        var blob = reader.GetBlobReader(constant.Value);

        object? value = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => blob.ReadBoolean(),
            ConstantTypeCode.Char => blob.ReadChar(),
            ConstantTypeCode.SByte => blob.ReadSByte(),
            ConstantTypeCode.Byte => blob.ReadByte(),
            ConstantTypeCode.Int16 => blob.ReadInt16(),
            ConstantTypeCode.UInt16 => blob.ReadUInt16(),
            ConstantTypeCode.Int32 => blob.ReadInt32(),
            ConstantTypeCode.UInt32 => blob.ReadUInt32(),
            ConstantTypeCode.Int64 => blob.ReadInt64(),
            ConstantTypeCode.UInt64 => blob.ReadUInt64(),
            ConstantTypeCode.Single => blob.ReadSingle(),
            ConstantTypeCode.Double => blob.ReadDouble(),
            ConstantTypeCode.String => blob.ReadUTF16(blob.Length),
            ConstantTypeCode.NullReference => null,
            _ => null
        };

        return value switch
        {
            null => null,
            char c => "'" + c + "'",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static int CompareMembers(ApiMember a, ApiMember b)
    {
        var result = string.CompareOrdinal(a.Kind, b.Kind);
        if (result != 0)
            return result;

        result = string.CompareOrdinal(a.Name, b.Name);
        if (result != 0)
            return result;

        result = a.TypeParameters.Count.CompareTo(b.TypeParameters.Count);
        if (result != 0)
            return result;

        return string.CompareOrdinal(ParameterKey(a), ParameterKey(b));
    }

    internal static string ParameterKey(ApiMember member)
        => string.Join("|", member.Parameters.Select(p => p.Type));
}
