using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NativeUI.Media;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Converts vector literals into typed constructor expressions during XAML compilation.
/// </summary>
internal sealed class VectorValueConverter(string typeName) : XamlValueConverter
{
    /// <inheritdoc />
    public override bool TryConvert(string literal, out ExpressionSyntax? value)
    {
        try
        {
            value = typeName switch
            {
                "NativeUI.Media.Geometry" or "NativeUI.Media.PathGeometry" => VectorCodeEmitter.Geometry(PathDataParser.Parse(literal)),
                "NativeUI.Media.ImageSource" => ObjectCreationExpression(IdentifierName("BitmapImage"))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(Text(literal))))),
                "NativeUI.Layout.Rect" => CreateCoordinates("Rect", literal, 4),
                "NativeUI.Layout.Point" => CreateCoordinates("Point", literal, 2),
                "NativeUI.Layout.Size" => CreateCoordinates("Size", literal, 2),
                "System.Collections.Immutable.ImmutableArray<NativeUI.Layout.Point>" => CreatePoints(literal),
                "NativeUI.Media.Brush" => CreateBrush(literal),
                _ => null
            };
            return value is not null;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            value = null;
            return false;
        }
    }

    private static ObjectCreationExpressionSyntax? CreateCoordinates(string type, string literal, int count)
    {
        string[] parts = literal.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != count)
        {
            return null;
        }

        List<ArgumentSyntax> arguments = [];
        foreach (string part in parts)
        {
            if (!TryParseNumber(part, out double number))
            {
                return null;
            }

            arguments.Add(Argument(Number(number)));
        }

        return ObjectCreationExpression(IdentifierName(type))
            .WithArgumentList(ArgumentList(SeparatedList(arguments)));
    }

    private static CollectionExpressionSyntax? CreatePoints(string literal)
    {
        string[] parts = literal.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length % 2 != 0)
        {
            return null;
        }

        List<CollectionElementSyntax> points = [];
        for (int index = 0; index < parts.Length; index += 2)
        {
            if (!TryParseNumber(parts[index], out double x) || !TryParseNumber(parts[index + 1], out double y))
            {
                return null;
            }

            points.Add(ExpressionElement(ObjectCreationExpression(IdentifierName("Point"))
                .WithArgumentList(ArgumentList(SeparatedList([Argument(Number(x)), Argument(Number(y))])))));
        }

        return CollectionExpression(SeparatedList(points));
    }

    private static ExpressionSyntax? CreateBrush(string literal)
    {
        if (literal is "None" or "none")
        {
            return LiteralExpression(SyntaxKind.NullLiteralExpression);
        }

        if (!XamlValueConverters.TryConvert(XamlPropertyKind.Color, literal, out ExpressionSyntax? color) || color is null)
        {
            return null;
        }

        return ObjectCreationExpression(IdentifierName("SolidColorBrush"))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(color))));
    }
}
