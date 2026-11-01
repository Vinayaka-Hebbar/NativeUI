using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Css.Compiler;

/// <summary>
/// Translates <see cref="CssValueSyntaxNode"/> AST nodes into Roslyn <see cref="ExpressionSyntax"/> trees
/// using <see cref="SyntaxFactory"/>.
/// </summary>
internal static class CssValueSyntaxBuilder
{
    public static bool TryBuildValueExpression(
        string propertyName,
        CssValueSyntaxNode valueNode,
        out ExpressionSyntax? expression,
        out ParsedCssColor? colorLiteral)
    {
        expression = null;
        colorLiteral = null;

        if (valueNode is CssFunctionValueNode func &&
            string.Equals(func.Name, "var", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildVarReference(propertyName, func, out expression);
        }

        if (propertyName.StartsWith("--", StringComparison.Ordinal))
        {
            if (CssColorParser.TryEvaluate(valueNode, out ParsedCssColor customColor))
            {
                colorLiteral = customColor;
                expression = BuildCssColorValueSyntax(customColor);
                return true;
            }

            if (TryBuildLengthSyntax(valueNode, out expression) ||
                TryBuildNumberSyntax(valueNode, out expression))
            {
                return true;
            }

            if (valueNode is CssKeywordValueNode kw)
            {
                expression = BuildNewCssValue("CssStringValue", StringLiteral(kw.Keyword));
                return true;
            }

            if (valueNode is CssStringValueNode str)
            {
                expression = BuildNewCssValue("CssStringValue", StringLiteral(str.Value));
                return true;
            }

            return false;
        }

        return propertyName switch
        {
            "color" or "background-color" or "background" or "border-color" => TryBuildColorSyntax(valueNode, out expression, out colorLiteral),
            "width" => TryBuildWidthSyntax(valueNode, out expression),
            "font-size" or "border-radius" or "border-width" or "height" => TryBuildLengthSyntax(valueNode, out expression),
            "padding" or "margin" => TryBuildThicknessSyntax(valueNode, out expression),
            "font-weight" => TryBuildFontWeightSyntax(valueNode, out expression),
            "text-align" => TryBuildTextAlignSyntax(valueNode, out expression),
            "opacity" => TryBuildOpacitySyntax(valueNode, out expression),
            "font-family" => TryBuildFontFamilySyntax(valueNode, out expression),
            "cursor" => TryBuildCursorSyntax(valueNode, out expression),
            "overflow" or "overflow-x" or "overflow-y" => TryBuildOverflowSyntax(valueNode, out expression),
            "scrollbar-color" => TryBuildScrollbarColorSyntax(valueNode, out expression),
            "scrollbar-width" => TryBuildScrollbarWidthSyntax(valueNode, out expression),
            _ => false
        };
    }

    public static ExpressionSyntax BuildColorFactoryInvocation(ParsedCssColor color)
    {
        string methodName = color.Space switch
        {
            ParsedColorSpace.Oklch => "FromOklch",
            ParsedColorSpace.Oklab => "FromOklab",
            ParsedColorSpace.Hsl => "FromHsl",
            _ => "FromSrgb"
        };

        ExpressionSyntax colorType = ParseTypeName("Color");
        MemberAccessExpressionSyntax memberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            colorType,
            IdentifierName(methodName));

        return InvocationExpression(memberAccess)
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(DoubleLiteral(color.C1)),
                Argument(DoubleLiteral(color.C2)),
                Argument(DoubleLiteral(color.C3)),
                Argument(DoubleLiteral(color.Alpha))
            ])));
    }

    public static string FormatValueSummary(CssValueSyntaxNode node)
    {
        return node switch
        {
            CssKeywordValueNode kw => kw.Keyword,
            CssNumberValueNode num => num.Value.ToString(CultureInfo.InvariantCulture),
            CssDimensionValueNode dim => $"{dim.Value.ToString(CultureInfo.InvariantCulture)}{dim.Unit}",
            CssPercentageValueNode pct => $"{pct.Value.ToString(CultureInfo.InvariantCulture)}%",
            CssHexColorValueNode hex => hex.Hex,
            CssStringValueNode str => str.Value,
            CssFunctionValueNode func => $"{func.Name}(...)",
            CssValueListNode list => string.Join(" ", list.Items.Select(FormatValueSummary)),
            _ => node.ToString()
        };
    }

    private static bool TryBuildVarReference(
        string propertyName,
        CssFunctionValueNode func,
        out ExpressionSyntax? expression)
    {
        expression = null;
        if (func.Arguments.Count is < 1 or > 2)
        {
            return false;
        }

        if (func.Arguments[0] is not CssKeywordValueNode varIdent ||
            !varIdent.Keyword.StartsWith("--", StringComparison.Ordinal) ||
            varIdent.Keyword.Length <= 2)
        {
            return false;
        }

        if (func.Arguments.Count == 1)
        {
            expression = BuildNewCssValue("CssVariableReference", StringLiteral(varIdent.Keyword));
            return true;
        }

        if (!TryBuildValueExpression(propertyName, func.Arguments[1], out ExpressionSyntax? fallbackExpr, out _) ||
            fallbackExpr is null)
        {
            return false;
        }

        expression = BuildNewCssValue("CssVariableReference", StringLiteral(varIdent.Keyword), fallbackExpr);
        return true;
    }

    private static bool TryBuildColorSyntax(
        CssValueSyntaxNode node,
        out ExpressionSyntax? expression,
        out ParsedCssColor? colorLiteral)
    {
        if (CssColorParser.TryEvaluate(node, out ParsedCssColor parsed))
        {
            colorLiteral = parsed;
            expression = BuildCssColorValueSyntax(parsed);
            return true;
        }

        expression = null;
        colorLiteral = null;
        return false;
    }

    private static ExpressionSyntax BuildCssColorValueSyntax(ParsedCssColor color)
    {
        return BuildNewCssValue("CssColorValue", BuildColorFactoryInvocation(color));
    }

    private static bool TryBuildWidthSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode { Keyword: var keyword } &&
            string.Equals(keyword, "fit-content", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssFitContentValue");
            return true;
        }

        return TryBuildLengthSyntax(node, out expression);
    }

    private static bool TryBuildLengthSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        switch (node)
        {
            case CssNumberValueNode { Value: 0.0 }:
                expression = BuildNewCssValue(
                    "CssLengthValue",
                    DoubleLiteral(0),
                    BuildEnumMember("CssLengthUnit", "Px"));
                return true;

            case CssPercentageValueNode pct:
                expression = BuildNewCssValue(
                    "CssLengthValue",
                    DoubleLiteral(pct.Value),
                    BuildEnumMember("CssLengthUnit", "Percent"));
                return true;

            case CssDimensionValueNode dim:
                string? unitEnum = dim.Unit.ToLowerInvariant() switch
                {
                    "px" => "Px",
                    "rem" => "Rem",
                    "em" => "Em",
                    _ => null
                };
                if (unitEnum is null)
                {
                    return false;
                }

                expression = BuildNewCssValue(
                    "CssLengthValue",
                    DoubleLiteral(dim.Value),
                    BuildEnumMember("CssLengthUnit", unitEnum));
                return true;

            default:
                return false;
        }
    }

    private static bool TryBuildThicknessSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        IReadOnlyList<CssValueSyntaxNode> items = node is CssValueListNode list ? list.Items : [node];
        if (items.Count is < 1 or > 4)
        {
            return false;
        }

        double[] pxValues = new double[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is CssNumberValueNode { Value: 0.0 })
            {
                pxValues[i] = 0.0;
            }
            else if (items[i] is CssDimensionValueNode dim && string.Equals(dim.Unit, "px", StringComparison.OrdinalIgnoreCase))
            {
                pxValues[i] = dim.Value;
            }
            else
            {
                return false;
            }
        }

        (double top, double right, double bottom, double left) = items.Count switch
        {
            1 => (pxValues[0], pxValues[0], pxValues[0], pxValues[0]),
            2 => (pxValues[0], pxValues[1], pxValues[0], pxValues[1]),
            3 => (pxValues[0], pxValues[1], pxValues[2], pxValues[1]),
            _ => (pxValues[0], pxValues[1], pxValues[2], pxValues[3])
        };

        ObjectCreationExpressionSyntax thicknessCreation = ObjectCreationExpression(
            ParseTypeName("Thickness"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(DoubleLiteral(top)),
                Argument(DoubleLiteral(right)),
                Argument(DoubleLiteral(bottom)),
                Argument(DoubleLiteral(left))
            ])));

        expression = BuildNewCssValue("CssThicknessValue", thicknessCreation);
        return true;
    }

    private static bool TryBuildFontWeightSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        string? weightMember = node switch
        {
            CssNumberValueNode num => ((int)num.Value) switch
            {
                100 => "Thin",
                300 => "Light",
                400 => "Normal",
                500 => "Medium",
                600 => "SemiBold",
                700 => "Bold",
                800 => "ExtraBold",
                900 => "Black",
                _ => null
            },
            CssKeywordValueNode kw => kw.Keyword.ToLowerInvariant() switch
            {
                "thin" => "Thin",
                "light" => "Light",
                "normal" or "regular" => "Normal",
                "medium" => "Medium",
                "semibold" or "semi-bold" => "SemiBold",
                "bold" => "Bold",
                "extrabold" or "extra-bold" => "ExtraBold",
                "black" => "Black",
                _ => null
            },
            _ => null
        };

        if (weightMember is null)
        {
            expression = null;
            return false;
        }

        expression = BuildNewCssValue(
            "CssFontWeightValue",
            BuildEnumMember("FontWeight", weightMember));
        return true;
    }

    private static bool TryBuildTextAlignSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode kw)
        {
            string? align = kw.Keyword.ToLowerInvariant() switch
            {
                "left" or "start" => "Left",
                "center" => "Center",
                "right" or "end" => "Right",
                _ => null
            };
            if (align is not null)
            {
                expression = BuildNewCssValue(
                    "CssTextAlignValue",
                    BuildEnumMember("TextAlign", align));
                return true;
            }
        }

        expression = null;
        return false;
    }

    private static bool TryBuildScrollbarColorSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } &&
            string.Equals(keyword, "auto", StringComparison.OrdinalIgnoreCase))
        {
            ExpressionSyntax none = LiteralExpression(SyntaxKind.NullLiteralExpression);
            expression = BuildNewCssValue("CssScrollbarColorValue", none, none);
            return true;
        }

        if (node is CssValueListNode { Items.Count: 2 } list &&
            TryBuildValueExpression("color", list.Items[0], out ExpressionSyntax? thumb, out _) && thumb is not null &&
            TryBuildValueExpression("color", list.Items[1], out ExpressionSyntax? track, out _) && track is not null)
        {
            expression = BuildNewCssValue("CssScrollbarColorValue", thumb, track);
            return true;
        }

        return false;
    }

    private static bool TryBuildScrollbarWidthSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is not CssKeywordValueNode keyword)
        {
            return false;
        }

        string? width = keyword.Keyword.ToLowerInvariant() switch
        {
            "auto" => "Auto",
            "thin" => "Thin",
            "none" => "None",
            _ => null
        };

        if (width is null)
        {
            return false;
        }

        expression = BuildNewCssValue("CssScrollbarWidthValue", BuildEnumMember("ScrollbarWidth", width));
        return true;
    }

    private static bool TryBuildOverflowSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is not CssKeywordValueNode keyword)
        {
            return false;
        }

        string? overflow = keyword.Keyword.ToLowerInvariant() switch
        {
            "visible" => "Visible",
            "hidden" => "Hidden",
            "auto" => "Auto",
            "scroll" => "Scroll",
            _ => null
        };

        if (overflow is null)
        {
            return false;
        }

        expression = BuildNewCssValue("CssOverflowValue", BuildEnumMember("Overflow", overflow));
        return true;
    }

    private static bool TryBuildCursorSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode kw)
        {
            string? cursor = kw.Keyword.ToLowerInvariant() switch
            {
                "pointer" or "hand" => "Pointer",
                "default" or "arrow" => "Default",
                "text" => "IBeam",
                "not-allowed" => "NotAllowed",
                "crosshair" => "Crosshair",
                "wait" => "Wait",
                _ => null
            };

            if (cursor is not null)
            {
                expression = BuildNewCssValue(
                    "CssCursorValue",
                    BuildEnumMember("Cursor", cursor));
                return true;
            }
        }

        expression = null;
        return false;
    }

    private static bool TryBuildOpacitySyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssNumberValueNode num && num.Value is >= 0.0 and <= 1.0)
        {
            expression = BuildNewCssValue("CssNumberValue", DoubleLiteral(num.Value));
            return true;
        }

        expression = null;
        return false;
    }

    private static bool TryBuildNumberSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssNumberValueNode num)
        {
            expression = BuildNewCssValue("CssNumberValue", DoubleLiteral(num.Value));
            return true;
        }

        expression = null;
        return false;
    }

    private static bool TryBuildFontFamilySyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        switch (node)
        {
            case CssStringValueNode str when !string.IsNullOrWhiteSpace(str.Value):
                expression = BuildNewCssValue("CssStringValue", StringLiteral(str.Value));
                return true;
            case CssKeywordValueNode kw when !string.IsNullOrWhiteSpace(kw.Keyword):
                expression = BuildNewCssValue("CssStringValue", StringLiteral(kw.Keyword));
                return true;
            default:
                expression = null;
                return false;
        }
    }

    private static ObjectCreationExpressionSyntax BuildNewCssValue(
        string cssValueTypeName,
        params ExpressionSyntax[] arguments)
    {
        TypeSyntax typeSyntax = ParseTypeName(cssValueTypeName);
        return ObjectCreationExpression(typeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
    }

    private static MemberAccessExpressionSyntax BuildEnumMember(string enumTypeName, string memberName)
    {
        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseTypeName(enumTypeName),
            IdentifierName(memberName));
    }

    internal static LiteralExpressionSyntax StringLiteral(string value)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value));
    }

    internal static LiteralExpressionSyntax DoubleLiteral(double value)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
    }
}
