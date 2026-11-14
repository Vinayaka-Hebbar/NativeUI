using Microsoft.CodeAnalysis;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Resolves XAML elements and attributes to the types and properties of a <see cref="Compilation"/>.
/// </summary>
/// <remarks>
/// An XML namespace is <c>using:Clr.Namespace</c>, <c>clr-namespace:Clr.Namespace</c>, or a URI that an assembly maps with
/// <c>XmlnsDefinitionAttribute</c>. An empty namespace is <see cref="XamlParser.NativeUINamespace"/>.
/// </remarks>
internal sealed class XamlTypeResolver
{
    private const string XmlnsDefinitionAttributeName = "NativeUI.Xaml.XmlnsDefinitionAttribute";

    private readonly Compilation _compilation;
    private readonly Dictionary<string, List<(IAssemblySymbol Assembly, string ClrNamespace)>> _definitions = new(StringComparer.Ordinal);

    public XamlTypeResolver(Compilation compilation)
    {
        _compilation = compilation;
        AddDefinitions(compilation.Assembly);
        foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            AddDefinitions(assembly);
        }
    }

    /// <summary>
    /// Returns the CLR namespace named directly by <paramref name="xmlNamespace"/>, or null when it is a URI.
    /// </summary>
    public static string? ReadClrNamespace(string xmlNamespace)
    {
        const string UsingPrefix = "using:";
        const string ClrNamespacePrefix = "clr-namespace:";

        if (xmlNamespace.StartsWith(UsingPrefix, StringComparison.Ordinal))
        {
            return xmlNamespace.Substring(UsingPrefix.Length);
        }

        if (!xmlNamespace.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string clrNamespace = xmlNamespace.Substring(ClrNamespacePrefix.Length);
        int assemblySeparator = clrNamespace.IndexOf(';');
        return assemblySeparator < 0 ? clrNamespace : clrNamespace.Substring(0, assemblySeparator);
    }

    /// <summary>
    /// Returns whether a member's value can be bound, which requires a static <c>NameProperty</c> member on its type.
    /// </summary>
    public static bool IsBindable(IPropertySymbol property)
    {
        return IsBindable(property.ContainingType, property.Name);
    }

    /// <summary>
    /// Returns whether a member's value can be bound, which requires a static <c>NameProperty</c> member on <paramref name="ownerType"/>.
    /// </summary>
    public static bool IsBindable(INamedTypeSymbol ownerType, string name)
    {
        foreach (ISymbol member in ownerType.GetMembers($"{name}Property"))
        {
            if (member.IsStatic)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the accessible type named <paramref name="typeName"/> in <paramref name="xmlNamespace"/>, or null.
    /// </summary>
    public INamedTypeSymbol? Resolve(string xmlNamespace, string typeName)
    {
        string? clrNamespace = ReadClrNamespace(xmlNamespace);
        if (clrNamespace is not null)
        {
            return AsAccessible(_compilation.GetTypeByMetadataName($"{clrNamespace}.{typeName}"));
        }

        string uri = string.IsNullOrEmpty(xmlNamespace) ? XamlParser.NativeUINamespace : xmlNamespace;
        if (!_definitions.TryGetValue(uri, out List<(IAssemblySymbol Assembly, string ClrNamespace)>? definitions))
        {
            return null;
        }

        foreach ((IAssemblySymbol assembly, string definedNamespace) in definitions)
        {
            INamedTypeSymbol? type = AsAccessible(assembly.GetTypeByMetadataName($"{definedNamespace}.{typeName}"));
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns whether generated code can create <paramref name="type"/> with an accessible parameterless constructor.
    /// </summary>
    public bool IsCreatable(INamedTypeSymbol type)
    {
        if (type.IsAbstract || type.TypeKind != TypeKind.Class)
        {
            return false;
        }

        foreach (IMethodSymbol constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 0 && _compilation.IsSymbolAccessibleWithin(constructor, _compilation.Assembly))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the instance property named <paramref name="name"/> that generated code can assign on
    /// <paramref name="type"/> or its base types, or null.
    /// </summary>
    public IPropertySymbol? FindSettableProperty(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { IsStatic: false, SetMethod: { IsInitOnly: false } setter } property &&
                    _compilation.IsSymbolAccessibleWithin(setter, _compilation.Assembly))
                {
                    return property;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the static attached property setter <c>SetName(UIElement, T)</c> on the NativeUI type named
    /// <paramref name="ownerName"/> when <paramref name="targetType"/> derives from <c>UIElement</c>, or null.
    /// </summary>
    public IMethodSymbol? FindAttachedSetter(string ownerName, string propertyName, INamedTypeSymbol targetType)
    {
        INamedTypeSymbol? ownerType = Resolve(XamlParser.NativeUINamespace, ownerName);
        INamedTypeSymbol? uiElementType = Resolve(XamlParser.NativeUINamespace, "UIElement");
        if (ownerType is null ||
            uiElementType is null ||
            !_compilation.ClassifyCommonConversion(targetType, uiElementType).IsImplicit)
        {
            return null;
        }

        string setterName = $"Set{propertyName}";
        for (INamedTypeSymbol? current = ownerType; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(setterName))
            {
                if (member is IMethodSymbol { IsStatic: true, ReturnsVoid: true, Parameters.Length: 2 } method &&
                    method.Parameters[0].RefKind == RefKind.None &&
                    method.Parameters[1].RefKind == RefKind.None &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, uiElementType) &&
                    _compilation.IsSymbolAccessibleWithin(method, _compilation.Assembly))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private void AddDefinitions(IAssemblySymbol assembly)
    {
        foreach (AttributeData attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != XmlnsDefinitionAttributeName ||
                attribute.ConstructorArguments.Length != 2 ||
                attribute.ConstructorArguments[0].Value is not string xmlNamespace ||
                attribute.ConstructorArguments[1].Value is not string clrNamespace)
            {
                continue;
            }

            if (!_definitions.TryGetValue(xmlNamespace, out List<(IAssemblySymbol Assembly, string ClrNamespace)>? definitions))
            {
                definitions = [];
                _definitions[xmlNamespace] = definitions;
            }

            definitions.Add((assembly, clrNamespace));
        }
    }

    private INamedTypeSymbol? AsAccessible(INamedTypeSymbol? type)
    {
        return type is not null && _compilation.IsSymbolAccessibleWithin(type, _compilation.Assembly) ? type : null;
    }
}
