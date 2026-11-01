using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Converts attribute text using the <c>XamlLiteralAttribute</c> patterns declared on the members of a value type.
/// </summary>
internal sealed class XamlLiteralConverter : XamlValueConverter
{
    private const string AttributeName = "NativeUI.Xaml.XamlLiteralAttribute";
    private const string Placeholder = "{0}";

    private readonly string _typeName;
    private readonly List<(ISymbol Member, string Pattern)> _literals;

    private XamlLiteralConverter(string typeName, List<(ISymbol Member, string Pattern)> literals)
    {
        _typeName = typeName;
        _literals = literals;
    }

    /// <summary>
    /// Creates the converter for <paramref name="type"/>, or returns null when it declares no valid literal member.
    /// </summary>
    public static XamlLiteralConverter? Create(INamedTypeSymbol type)
    {
        List<(ISymbol Member, string Pattern)> literals = [];
        foreach (ISymbol member in type.GetMembers())
        {
            foreach (AttributeData attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == AttributeName &&
                    attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is string pattern &&
                    IsValidMember(type, member, pattern))
                {
                    literals.Add((member, pattern));
                }
            }
        }

        return literals.Count == 0 ? null : new XamlLiteralConverter(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), literals);
    }

    /// <inheritdoc />
    public override bool TryConvert(string literal, out ExpressionSyntax? value)
    {
        foreach ((ISymbol member, string pattern) in _literals)
        {
            if (TryConvertMember(member, pattern, literal, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool IsValidMember(INamedTypeSymbol type, ISymbol member, string pattern)
    {
        if (!member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        bool hasPlaceholder = pattern.Contains(Placeholder);
        return member switch
        {
            IPropertySymbol property => !hasPlaceholder && SymbolEqualityComparer.Default.Equals(property.Type, type),
            IFieldSymbol field => !hasPlaceholder && SymbolEqualityComparer.Default.Equals(field.Type, type),
            IMethodSymbol method => SymbolEqualityComparer.Default.Equals(method.ReturnType, type) &&
                method.Parameters.Length == (hasPlaceholder ? 1 : 0) &&
                (!hasPlaceholder || XamlValueConverters.ForPrimitive(method.Parameters[0].Type) is not null),
            _ => false
        };
    }

    private bool TryConvertMember(ISymbol member, string pattern, string literal, out ExpressionSyntax? value)
    {
        value = null;
        int placeholder = pattern.IndexOf(Placeholder, StringComparison.Ordinal);
        if (placeholder < 0)
        {
            if (!string.Equals(pattern, literal, StringComparison.Ordinal))
            {
                return false;
            }

            value = member is IMethodSymbol ? Invoke(_typeName, member.Name) : MemberOf(_typeName, member.Name);
            return true;
        }

        string prefix = pattern.Substring(0, placeholder);
        string suffix = pattern.Substring(placeholder + Placeholder.Length);
        if (literal.Length < prefix.Length + suffix.Length ||
            !literal.StartsWith(prefix, StringComparison.Ordinal) ||
            !literal.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string argumentText = literal.Substring(prefix.Length, literal.Length - prefix.Length - suffix.Length);
        IMethodSymbol method = (IMethodSymbol)member;
        XamlValueConverter parameterConverter = XamlValueConverters.ForPrimitive(method.Parameters[0].Type)!;
        if (!parameterConverter.TryConvert(argumentText, out ExpressionSyntax? argument))
        {
            return false;
        }

        value = Invoke(_typeName, method.Name, argument!);
        return true;
    }
}
