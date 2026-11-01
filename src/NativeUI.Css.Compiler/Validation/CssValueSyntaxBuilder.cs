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
    private static readonly Dictionary<string, string> s_animatableProperties = new(StringComparer.Ordinal)
    {
        ["opacity"] = "Opacity",
        ["transform"] = "Transform",
        ["color"] = "Color",
        ["background-color"] = "BackgroundColor",
        ["border-color"] = "BorderColor",
        ["width"] = "Width",
        ["height"] = "Height",
        ["top"] = "Top",
        ["right"] = "Right",
        ["bottom"] = "Bottom",
        ["left"] = "Left"
    };

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
            "height" => TryBuildHeightSyntax(valueNode, out expression),
            "font-size" or "border-radius" or "border-width" or "top" or "right" or "bottom" or "left" => TryBuildLengthSyntax(valueNode, out expression),
            "padding" or "margin" => TryBuildThicknessSyntax(valueNode, out expression),
            "font-weight" => TryBuildFontWeightSyntax(valueNode, out expression),
            "text-align" => TryBuildTextAlignSyntax(valueNode, out expression),
            "opacity" => TryBuildOpacitySyntax(valueNode, out expression),
            "font-family" => TryBuildFontFamilySyntax(valueNode, out expression),
            "cursor" => TryBuildCursorSyntax(valueNode, out expression),
            "overflow" or "overflow-x" or "overflow-y" => TryBuildOverflowSyntax(valueNode, out expression),
            "scrollbar-color" => TryBuildScrollbarColorSyntax(valueNode, out expression),
            "scrollbar-width" => TryBuildScrollbarWidthSyntax(valueNode, out expression),
            "position" => TryBuildPositionSyntax(valueNode, out expression),
            "transform" => TryBuildTransformSyntax(valueNode, out expression),
            "transition" => TryBuildTransitionSyntax(valueNode, out expression),
            "animation" => TryBuildAnimationSyntax(valueNode, out expression),
            _ => false
        };
    }

    /// <summary>
    /// Returns whether <paramref name="propertyName"/> can appear in a <c>@keyframes</c> rule.
    /// </summary>
    public static bool IsAnimatableProperty(string propertyName)
    {
        return propertyName == "background" || s_animatableProperties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Builds a transform from a translation followed by a scale, the one order that <c>StyleTransform</c>
    /// represents, because a browser applies the listed functions in their source order.
    /// </summary>
    private static bool TryBuildTransformSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssTransformValue", LiteralExpression(SyntaxKind.NullLiteralExpression));
            return true;
        }

        IReadOnlyList<CssValueSyntaxNode> items = node is CssValueListNode list ? list.Items : [node];
        double translateX = 0;
        double translateY = 0;
        double translateXPercent = 0;
        double translateYPercent = 0;
        double scaleX = 1;
        double scaleY = 1;
        bool hasScale = false;

        foreach (CssValueSyntaxNode item in items)
        {
            if (item is not CssFunctionValueNode function)
            {
                return false;
            }

            string name = function.Name.ToLowerInvariant();
            IReadOnlyList<CssValueSyntaxNode> args = function.Arguments;
            if (name is "translate" or "translatex" or "translatey")
            {
                if (hasScale)
                {
                    return false;
                }

                int expected = name == "translate" ? args.Count : 1;
                if (expected is < 1 or > 2 || args.Count != expected)
                {
                    return false;
                }

                if (name != "translatey" && !TryReadTranslation(args[0], ref translateX, ref translateXPercent))
                {
                    return false;
                }

                CssValueSyntaxNode? vertical = name == "translatey" ? args[0] : (args.Count == 2 ? args[1] : null);
                if (vertical is not null && !TryReadTranslation(vertical, ref translateY, ref translateYPercent))
                {
                    return false;
                }
            }
            else if (name is "scale" or "scalex" or "scaley")
            {
                hasScale = true;
                if (args.Count == 0 || args.Count > (name == "scale" ? 2 : 1) ||
                    args.Any(static arg => arg is not CssNumberValueNode))
                {
                    return false;
                }

                double first = ((CssNumberValueNode)args[0]).Value;
                double second = args.Count == 2 ? ((CssNumberValueNode)args[1]).Value : first;
                scaleX = name == "scaley" ? scaleX : first;
                scaleY = name == "scalex" ? scaleY : second;
            }
            else
            {
                return false;
            }
        }

        ExpressionSyntax transform = ObjectCreationExpression(ParseTypeName("StyleTransform"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(DoubleLiteral(translateX)),
                Argument(DoubleLiteral(translateY)),
                Argument(DoubleLiteral(translateXPercent)),
                Argument(DoubleLiteral(translateYPercent)),
                Argument(DoubleLiteral(scaleX)),
                Argument(DoubleLiteral(scaleY))
            ])));
        expression = BuildNewCssValue("CssTransformValue", transform);
        return true;
    }

    private static bool TryReadTranslation(CssValueSyntaxNode node, ref double pixels, ref double percent)
    {
        switch (node)
        {
            case CssNumberValueNode { Value: 0.0 }:
                return true;
            case CssPercentageValueNode percentage:
                percent = percentage.Value;
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "px", StringComparison.OrdinalIgnoreCase):
                pixels = dimension.Value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Builds transition entries. The parser removes commas, so each property name starts a new entry.
    /// </summary>
    private static bool TryBuildTransitionSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        ExpressionSyntax emptyTransitions = BuildImmutableArrayEmpty("TransitionSpec");
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssTransitionValue", emptyTransitions);
            return true;
        }

        IReadOnlyList<CssValueSyntaxNode> items = node is CssValueListNode list ? list.Items : [node];
        List<ExpressionSyntax> entries = [];
        string? property = null;
        double? duration = null;
        double? delay = null;
        ExpressionSyntax? easing = null;
        bool hasEntry = false;

        foreach (CssValueSyntaxNode item in items)
        {
            if (TryReadTime(item, out double time))
            {
                if (delay is not null)
                {
                    return false;
                }

                if (duration is null)
                {
                    duration = time;
                }
                else
                {
                    delay = time;
                }

                hasEntry = true;
            }
            else if (TryBuildEasing(item, out ExpressionSyntax? parsedEasing))
            {
                if (easing is not null)
                {
                    return false;
                }

                easing = parsedEasing;
                hasEntry = true;
            }
            else if (item is CssKeywordValueNode propertyName)
            {
                if (property is not null || duration is not null)
                {
                    entries.Add(BuildTransitionSpec(property, duration, delay, easing));
                    (duration, delay, easing) = (null, null, null);
                }

                property = NormalizeTransitionProperty(propertyName.Keyword);
                if (property is null)
                {
                    return false;
                }

                hasEntry = true;
            }
            else
            {
                return false;
            }
        }

        if (!hasEntry)
        {
            return false;
        }

        entries.Add(BuildTransitionSpec(property, duration, delay, easing));
        expression = BuildNewCssValue("CssTransitionValue", BuildImmutableArrayCreate(entries));
        return true;
    }

    private static string? NormalizeTransitionProperty(string name)
    {
        string normalized = name.ToLowerInvariant();
        if (normalized == "background")
        {
            normalized = "background-color";
        }

        return s_animatableProperties.ContainsKey(normalized) || normalized == "all" ? normalized : null;
    }

    private static ExpressionSyntax BuildTransitionSpec(string? property, double? duration, double? delay, ExpressionSyntax? easing)
    {
        string member = property is null or "all" ? "All" : s_animatableProperties[property];
        return ObjectCreationExpression(ParseTypeName("TransitionSpec"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(BuildEnumMember("AnimatableProperty", member)),
                Argument(DoubleLiteral(duration ?? 0)),
                Argument(DoubleLiteral(delay ?? 0)),
                Argument(easing ?? BuildEasingMember("Ease"))
            ])));
    }

    /// <summary>
    /// Builds a single animation. A comma-separated list of animations is not supported.
    /// </summary>
    private static bool TryBuildAnimationSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssAnimationValue", LiteralExpression(SyntaxKind.NullLiteralExpression));
            return true;
        }

        IReadOnlyList<CssValueSyntaxNode> items = node is CssValueListNode list ? list.Items : [node];
        string? name = null;
        double? duration = null;
        double? delay = null;
        ExpressionSyntax? easing = null;
        double iterations = 1;
        string direction = "Normal";
        string fillMode = "None";

        foreach (CssValueSyntaxNode item in items)
        {
            if (TryReadTime(item, out double time))
            {
                if (delay is not null)
                {
                    return false;
                }

                if (duration is null)
                {
                    duration = time;
                }
                else
                {
                    delay = time;
                }
            }
            else if (TryBuildEasing(item, out ExpressionSyntax? parsedEasing))
            {
                easing = parsedEasing;
            }
            else if (item is CssNumberValueNode { Value: > 0 } count)
            {
                iterations = count.Value;
            }
            else if (item is CssKeywordValueNode word)
            {
                switch (word.Keyword.ToLowerInvariant())
                {
                    case "infinite":
                        iterations = double.PositiveInfinity;
                        break;
                    case "normal":
                        direction = "Normal";
                        break;
                    case "reverse":
                        direction = "Reverse";
                        break;
                    case "alternate":
                        direction = "Alternate";
                        break;
                    case "alternate-reverse":
                        direction = "AlternateReverse";
                        break;
                    case "forwards":
                        fillMode = "Forwards";
                        break;
                    case "backwards":
                        fillMode = "Backwards";
                        break;
                    case "both":
                        fillMode = "Both";
                        break;
                    default:
                        if (name is not null)
                        {
                            return false;
                        }

                        name = word.Keyword;
                        break;
                }
            }
            else
            {
                return false;
            }
        }

        if (name is null)
        {
            return false;
        }

        ExpressionSyntax iterationsExpression = double.IsPositiveInfinity(iterations)
            ? MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                PredefinedType(Token(SyntaxKind.DoubleKeyword)),
                IdentifierName(nameof(double.PositiveInfinity)))
            : DoubleLiteral(iterations);

        ExpressionSyntax animation = ObjectCreationExpression(ParseTypeName("AnimationSpec"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(StringLiteral(name)),
                Argument(DoubleLiteral(duration ?? 0)),
                Argument(DoubleLiteral(delay ?? 0)),
                Argument(easing ?? BuildEasingMember("Ease")),
                Argument(iterationsExpression),
                Argument(BuildEnumMember("AnimationDirection", direction)),
                Argument(BuildEnumMember("AnimationFillMode", fillMode))
            ])));
        expression = BuildNewCssValue("CssAnimationValue", animation);
        return true;
    }

    private static bool TryReadTime(CssValueSyntaxNode node, out double milliseconds)
    {
        milliseconds = 0;
        if (node is not CssDimensionValueNode dimension)
        {
            return false;
        }

        switch (dimension.Unit.ToLowerInvariant())
        {
            case "ms":
                milliseconds = dimension.Value;
                return true;
            case "s":
                milliseconds = dimension.Value * 1000;
                return true;
            default:
                return false;
        }
    }

    private static bool TryBuildEasing(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode keyword)
        {
            string? member = keyword.Keyword.ToLowerInvariant() switch
            {
                "linear" => "Linear",
                "ease" => "Ease",
                "ease-in" => "EaseIn",
                "ease-out" => "EaseOut",
                "ease-in-out" => "EaseInOut",
                _ => null
            };
            if (member is null)
            {
                return false;
            }

            expression = BuildEasingMember(member);
            return true;
        }

        if (node is CssFunctionValueNode { Arguments.Count: 4 } function &&
            string.Equals(function.Name, "cubic-bezier", StringComparison.OrdinalIgnoreCase) &&
            function.Arguments.All(static arg => arg is CssNumberValueNode))
        {
            expression = ObjectCreationExpression(ParseTypeName("Easing"))
                .WithArgumentList(ArgumentList(SeparatedList(function.Arguments.Select(
                    static arg => Argument(DoubleLiteral(((CssNumberValueNode)arg).Value))))));
            return true;
        }

        return false;
    }

    private static MemberAccessExpressionSyntax BuildEasingMember(string member)
    {
        return BuildEnumMember("Easing", member);
    }

    private static ExpressionSyntax BuildImmutableArrayEmpty(string elementType)
    {
        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseTypeName($"ImmutableArray<{elementType}>"),
            IdentifierName("Empty"));
    }

    private static ExpressionSyntax BuildImmutableArrayCreate(IEnumerable<ExpressionSyntax> items)
    {
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("ImmutableArray"),
                IdentifierName("Create")))
            .WithArgumentList(ArgumentList(SeparatedList(items.Select(Argument))));
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

        return TryBuildHeightSyntax(node, out expression);
    }

    private static bool TryBuildHeightSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode { Keyword: var keyword } &&
            string.Equals(keyword, "auto", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssAutoValue");
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

    private static bool TryBuildPositionSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is not CssKeywordValueNode keyword)
        {
            return false;
        }

        string? position = keyword.Keyword.ToLowerInvariant() switch
        {
            "static" => "Static",
            "relative" => "Relative",
            "absolute" => "Absolute",
            "fixed" => "Fixed",
            _ => null
        };

        if (position is null)
        {
            return false;
        }

        expression = BuildNewCssValue("CssPositionValue", BuildEnumMember("Position", position));
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
