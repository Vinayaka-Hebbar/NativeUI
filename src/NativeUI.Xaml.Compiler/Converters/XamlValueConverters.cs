using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Provides the converter for each <see cref="XamlPropertyKind"/> that has a literal form.
/// </summary>
internal static class XamlValueConverters
{
    private static readonly Dictionary<XamlPropertyKind, XamlValueConverter> s_converters = new()
    {
        [XamlPropertyKind.String] = new StringConverter(),
        [XamlPropertyKind.Boolean] = new BooleanConverter(),
        [XamlPropertyKind.Int32] = new Int32Converter(NumberStyles.Integer),
        [XamlPropertyKind.AttachedInt32] = new Int32Converter(NumberStyles.None),
        [XamlPropertyKind.Int64] = new Int64Converter(),
        [XamlPropertyKind.Double] = new DoubleConverter(),
        [XamlPropertyKind.StyleLength] = new DoubleConverter(),
        [XamlPropertyKind.StyleThickness] = new ThicknessConverter(),
        [XamlPropertyKind.Orientation] = new EnumConverter("Orientation", "Vertical", "Horizontal"),
        [XamlPropertyKind.PortalSide] = new EnumConverter("PortalSide", "Top", "Right", "Bottom", "Left"),
        [XamlPropertyKind.Alignment] = new EnumConverter("Alignment", "Start", "Center", "End"),
        [XamlPropertyKind.Position] = new EnumConverter("Position", "Static", "Relative", "Absolute", "Fixed"),
        [XamlPropertyKind.Vector3] = new Vector3Converter(),
        [XamlPropertyKind.Color] = new ColorConverter(),
        [XamlPropertyKind.GridLengthList] = new GridLengthListConverter(),
        [XamlPropertyKind.ClassList] = new ClassListConverter()
    };

    /// <summary>
    /// Attempts to convert <paramref name="literal"/> for a property of the given <paramref name="kind"/>.
    /// </summary>
    public static bool TryConvert(XamlPropertyKind kind, string literal, out ExpressionSyntax? value)
    {
        value = null;
        return s_converters.TryGetValue(kind, out XamlValueConverter? converter) && converter.TryConvert(literal, out value);
    }

    /// <summary>
    /// Returns the converter for a property of the given <paramref name="type"/>, or null when the type has no literal form.
    /// </summary>
    public static XamlValueConverter? ForType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        XamlValueConverter? primitive = ForPrimitive(type);
        if (primitive is not null)
        {
            return primitive;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        if (namedType.TypeKind == TypeKind.Enum)
        {
            List<string> members = [];
            foreach (ISymbol member in namedType.GetMembers())
            {
                if (member is IFieldSymbol { IsConst: true })
                {
                    members.Add(member.Name);
                }
            }

            return new EnumConverter(namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), [.. members]);
        }

        return namedType.ToDisplayString() switch
        {
            "NativeUI.Media.Color" => s_converters[XamlPropertyKind.Color],
            "System.Numerics.Vector3" => s_converters[XamlPropertyKind.Vector3],
            _ => XamlLiteralConverter.Create(namedType)
        };
    }

    /// <summary>
    /// Returns the converter for a <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, or
    /// <see cref="double"/>, or null for any other type.
    /// </summary>
    public static XamlValueConverter? ForPrimitive(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => s_converters[XamlPropertyKind.String],
            SpecialType.System_Boolean => s_converters[XamlPropertyKind.Boolean],
            SpecialType.System_Int32 => s_converters[XamlPropertyKind.Int32],
            SpecialType.System_Int64 => s_converters[XamlPropertyKind.Int64],
            SpecialType.System_Double => s_converters[XamlPropertyKind.Double],
            _ => null
        };
    }

    private sealed class StringConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = Text(literal);
            return true;
        }
    }

    private sealed class BooleanConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (!bool.TryParse(literal, out bool result))
            {
                return false;
            }

            value = LiteralExpression(result ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
            return true;
        }
    }

    private sealed class Int32Converter(NumberStyles styles) : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (!int.TryParse(literal, styles, CultureInfo.InvariantCulture, out int result))
            {
                return false;
            }

            value = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(result));
            return true;
        }
    }

    private sealed class Int64Converter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (!long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                return false;
            }

            value = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(result));
            return true;
        }
    }

    private sealed class DoubleConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (!TryParseNumber(literal, out double result))
            {
                return false;
            }

            value = Number(result);
            return true;
        }
    }

    private sealed class EnumConverter(string type, params string[] members) : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (Array.IndexOf(members, literal) < 0)
            {
                return false;
            }

            value = MemberOf(type, literal);
            return true;
        }
    }

    /// <summary>
    /// Converts one, two (vertical, horizontal), or four (top, right, bottom, left) comma-separated numbers.
    /// </summary>
    private sealed class ThicknessConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            string[] tokens = SplitList(literal);
            if (tokens.Length is not (1 or 2 or 4))
            {
                return false;
            }

            ArgumentSyntax[] arguments = new ArgumentSyntax[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryParseNumber(tokens[i], out double component))
                {
                    return false;
                }

                arguments[i] = Argument(Number(component));
            }

            value = ObjectCreationExpression(IdentifierName("Thickness"))
                .WithArgumentList(ArgumentList(SeparatedList(arguments)));
            return true;
        }
    }

    /// <summary>
    /// Converts three comma-separated numbers.
    /// </summary>
    private sealed class Vector3Converter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            string[] tokens = SplitList(literal);
            if (tokens.Length != 3)
            {
                return false;
            }

            ArgumentSyntax[] arguments = new ArgumentSyntax[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryParseNumber(tokens[i], out double component))
                {
                    return false;
                }

                arguments[i] = Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((float)component)));
            }

            value = ObjectCreationExpression(ParseTypeName("global::System.Numerics.Vector3"))
                .WithArgumentList(ArgumentList(SeparatedList(arguments)));
            return true;
        }
    }

    /// <summary>
    /// Converts <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, or a member name of <c>Colors</c>.
    /// </summary>
    private sealed class ColorConverter : XamlValueConverter
    {
        // The members of NativeUI.Media.Colors, which the compiler cannot reference.
        private static readonly HashSet<string> s_namedColors = new(StringComparer.Ordinal)
        {
            "Transparent",
            "Black",
            "White",
            "Blue",
            "Indigo",
            "Green",
            "Red"
        };

        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            if (s_namedColors.Contains(literal))
            {
                value = MemberOf("Colors", literal);
                return true;
            }

            if (literal.Length is not (4 or 5 or 7 or 9) || literal[0] != '#')
            {
                return false;
            }

            int channelLength = literal.Length <= 5 ? 1 : 2;
            int channelCount = (literal.Length - 1) / channelLength;
            ExpressionSyntax[] channels = new ExpressionSyntax[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                string channel = literal.Substring(1 + (i * channelLength), channelLength);
                string expanded = channelLength == 1 ? channel + channel : channel;
                if (!int.TryParse(expanded, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int component))
                {
                    return false;
                }

                channels[i] = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(component));
            }

            value = Invoke("Color", "FromRgbBytes", channels);
            return true;
        }
    }

    /// <summary>
    /// Converts a comma-separated list of <c>Auto</c>, <c>*</c>, <c>2*</c>, or a number of pixels.
    /// </summary>
    private sealed class GridLengthListConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            value = null;
            string[] tokens = SplitList(literal);
            CollectionElementSyntax[] elements = new CollectionElementSyntax[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryConvertLength(tokens[i], out ExpressionSyntax? length))
                {
                    return false;
                }

                elements[i] = ExpressionElement(length!);
            }

            value = CollectionExpression(SeparatedList(elements));
            return true;
        }

        private static bool TryConvertLength(string token, out ExpressionSyntax? length)
        {
            length = null;
            if (string.Equals(token, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                length = MemberOf("GridLength", "Auto");
                return true;
            }

            if (token == "*")
            {
                length = Invoke("GridLength", "Star");
                return true;
            }

            bool isStar = token.EndsWith("*", StringComparison.Ordinal);
            string number = isStar ? token.Substring(0, token.Length - 1) : token;
            if (!TryParseNumber(number, out double result) || result < 0)
            {
                return false;
            }

            length = Invoke("GridLength", isStar ? "Star" : "Pixels", Number(result));
            return true;
        }
    }

    /// <summary>
    /// Converts a whitespace-separated list of class names.
    /// </summary>
    private sealed class ClassListConverter : XamlValueConverter
    {
        public override bool TryConvert(string literal, out ExpressionSyntax? value)
        {
            string[] names = XamlValidator.SplitClassNames(literal);
            CollectionElementSyntax[] elements = new CollectionElementSyntax[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                elements[i] = ExpressionElement(Text(names[i]));
            }

            value = CollectionExpression(SeparatedList(elements));
            return true;
        }
    }
}
