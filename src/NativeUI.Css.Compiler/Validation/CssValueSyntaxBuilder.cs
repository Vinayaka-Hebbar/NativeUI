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
    private delegate bool SyntaxBuilder(CssValueSyntaxNode node, out ExpressionSyntax? expression);

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
            return TryBuildVarReference(func, BuildFallback, out expression);
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
            "height" => TryBuildAutoOrLengthSyntax(valueNode, out expression),
            "min-width" or "min-height" => TryBuildAutoOrLengthSyntax(valueNode, out expression),
            "max-width" or "max-height" => TryBuildNoneOrLengthSyntax(valueNode, out expression),
            "font-size" or "gap" or "border-width" or "top" or "right" or "bottom" or "left" => TryBuildLengthSyntax(valueNode, out expression),
            "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius" => TryBuildLengthSyntax(valueNode, out expression),
            "border-radius" => TryBuildBorderRadiusSyntax(valueNode, out expression),
            "padding" or "margin" => TryBuildThicknessSyntax(valueNode, out expression),
            "font-weight" => TryBuildFontWeightSyntax(valueNode, out expression),
            "text-align" => TryBuildTextAlignSyntax(valueNode, out expression),
            "line-height" => TryBuildLineHeightSyntax(valueNode, out expression),
            "box-shadow" => TryBuildBoxShadowSyntax(valueNode, out expression),
            "opacity" => TryBuildOpacitySyntax(valueNode, out expression),
            "z-index" => TryBuildZIndexSyntax(valueNode, out expression),
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

        bool BuildFallback(CssValueSyntaxNode fallback, out ExpressionSyntax? fallbackExpression)
        {
            return TryBuildValueExpression(propertyName, fallback, out fallbackExpression, out _);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="propertyName"/> can appear in a <c>@keyframes</c> rule.
    /// </summary>
    public static bool IsAnimatableProperty(string propertyName)
    {
        return propertyName == "background" || s_animatableProperties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Builds a transform from translations followed by scales, the one order that <c>StyleTransform</c> represents,
    /// because a browser applies the listed functions in their source order. Consecutive translations add and
    /// consecutive scales multiply, which is their composition.
    /// </summary>
    private static bool TryBuildTransformSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssTransformValue", LiteralExpression(SyntaxKind.NullLiteralExpression));
            return true;
        }

        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> items))
        {
            return false;
        }

        double translateX = 0;
        double translateY = 0;
        double translateXPercent = 0;
        double translateYPercent = 0;
        double scaleX = 1;
        double scaleY = 1;
        double rotation = 0;
        bool hasScale = false;
        bool hasRotation = false;

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
                if (hasScale || hasRotation)
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
            else if (name == "rotate")
            {
                if (hasScale || args.Count != 1 || !TryReadAngleDegrees(args[0], out double angle))
                {
                    return false;
                }

                hasRotation = true;
                rotation += angle;
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
                if (name != "scaley")
                {
                    scaleX *= first;
                }

                if (name != "scalex")
                {
                    scaleY *= second;
                }
            }
            else
            {
                return false;
            }
        }

        List<ArgumentSyntax> transformArgs =
        [
            Argument(DoubleLiteral(translateX)),
            Argument(DoubleLiteral(translateY)),
            Argument(DoubleLiteral(translateXPercent)),
            Argument(DoubleLiteral(translateYPercent)),
            Argument(DoubleLiteral(scaleX)),
            Argument(DoubleLiteral(scaleY))
        ];
        if (rotation != 0)
        {
            transformArgs.Add(Argument(DoubleLiteral(rotation)));
        }

        ExpressionSyntax transform = ObjectCreationExpression(ParseTypeName("StyleTransform"))
            .WithArgumentList(ArgumentList(SeparatedList(transformArgs)));
        expression = BuildNewCssValue("CssTransformValue", transform);
        return true;
    }

    private static bool TryReadAngleDegrees(CssValueSyntaxNode node, out double degrees)
    {
        switch (node)
        {
            case CssNumberValueNode { Value: 0.0 }:
                degrees = 0;
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "deg", StringComparison.OrdinalIgnoreCase):
                degrees = dimension.Value;
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "rad", StringComparison.OrdinalIgnoreCase):
                degrees = dimension.Value * (180.0 / Math.PI);
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "turn", StringComparison.OrdinalIgnoreCase):
                degrees = dimension.Value * 360.0;
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "grad", StringComparison.OrdinalIgnoreCase):
                degrees = dimension.Value * 0.9;
                return true;
            default:
                degrees = 0;
                return false;
        }
    }

    /// <summary>
    /// Adds a translation length to the pixel or percentage offset.
    /// </summary>
    private static bool TryReadTranslation(CssValueSyntaxNode node, ref double pixels, ref double percent)
    {
        switch (node)
        {
            case CssNumberValueNode { Value: 0.0 }:
                return true;
            case CssPercentageValueNode percentage:
                percent += percentage.Value;
                return true;
            case CssDimensionValueNode dimension when string.Equals(dimension.Unit, "px", StringComparison.OrdinalIgnoreCase):
                pixels += dimension.Value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Builds one transition entry for each comma-separated value.
    /// </summary>
    private static bool TryBuildTransitionSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssTransitionValue", BuildImmutableArrayEmpty("TransitionSpec"));
            return true;
        }

        IReadOnlyList<CssValueSyntaxNode> values = node is CssValueListNode { Separator: CssValueSeparator.Comma } list
            ? list.Items
            : [node];
        if (values.Count == 0)
        {
            return false;
        }

        List<ExpressionSyntax> entries = [];
        foreach (CssValueSyntaxNode value in values)
        {
            if (!TryBuildTransitionEntry(value, out ExpressionSyntax? entry) || entry is null)
            {
                return false;
            }

            entries.Add(entry);
        }

        expression = BuildNewCssValue("CssTransitionValue", BuildImmutableArrayCreate(entries));
        return true;
    }

    /// <summary>
    /// Builds a transition from at most one property, a duration followed by an optional delay, and at most one easing,
    /// in any order.
    /// </summary>
    private static bool TryBuildTransitionEntry(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> components))
        {
            return false;
        }

        string? property = null;
        double? duration = null;
        double? delay = null;
        ExpressionSyntax? easing = null;
        foreach (CssValueSyntaxNode component in components)
        {
            if (TryReadTime(component, out double time))
            {
                if (!TryAddTime(time, ref duration, ref delay))
                {
                    return false;
                }
            }
            else if (TryBuildEasing(component, out ExpressionSyntax? parsedEasing))
            {
                if (easing is not null)
                {
                    return false;
                }

                easing = parsedEasing;
            }
            else if (component is CssKeywordValueNode propertyName && property is null)
            {
                property = NormalizeTransitionProperty(propertyName.Keyword);
                if (property is null)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        expression = BuildTransitionSpec(property, duration, delay, easing);
        return true;
    }

    /// <summary>
    /// Assigns the first time to the duration, which cannot be negative, and the second to the delay.
    /// </summary>
    private static bool TryAddTime(double time, ref double? duration, ref double? delay)
    {
        if (duration is null)
        {
            if (time < 0)
            {
                return false;
            }

            duration = time;
            return true;
        }

        if (delay is null)
        {
            delay = time;
            return true;
        }

        return false;
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
    /// Builds a single animation. As in the CSS shorthand, a component that several longhands accept is assigned to the
    /// first unassigned one in the order easing, iteration count, direction, fill mode, and name, so
    /// <c>reverse reverse 1s</c> runs the animation named <c>reverse</c> in reverse. A comma-separated list of animations
    /// is not supported.
    /// </summary>
    private static bool TryBuildAnimationSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssAnimationValue", LiteralExpression(SyntaxKind.NullLiteralExpression));
            return true;
        }

        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> components))
        {
            return false;
        }

        string? name = null;
        double? duration = null;
        double? delay = null;
        ExpressionSyntax? easing = null;
        double? iterations = null;
        string? direction = null;
        string? fillMode = null;

        foreach (CssValueSyntaxNode component in components)
        {
            if (TryReadTime(component, out double time))
            {
                if (!TryAddTime(time, ref duration, ref delay))
                {
                    return false;
                }
            }
            else if (easing is null && TryBuildEasing(component, out ExpressionSyntax? parsedEasing))
            {
                easing = parsedEasing;
            }
            else if (iterations is null && TryReadIterationCount(component, out double count))
            {
                iterations = count;
            }
            else if (direction is null && TryReadAnimationDirection(component, out string? parsedDirection))
            {
                direction = parsedDirection;
            }
            else if (fillMode is null && TryReadAnimationFillMode(component, out string? parsedFillMode))
            {
                fillMode = parsedFillMode;
            }
            else if (component is CssKeywordValueNode animationName && name is null)
            {
                name = animationName.Keyword;
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

        ExpressionSyntax iterationsExpression = iterations is double.PositiveInfinity
            ? MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                PredefinedType(Token(SyntaxKind.DoubleKeyword)),
                IdentifierName(nameof(double.PositiveInfinity)))
            : DoubleLiteral(iterations ?? 1);

        ExpressionSyntax animation = ObjectCreationExpression(ParseTypeName("AnimationSpec"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(StringLiteral(name)),
                Argument(DoubleLiteral(duration ?? 0)),
                Argument(DoubleLiteral(delay ?? 0)),
                Argument(easing ?? BuildEasingMember("Ease")),
                Argument(iterationsExpression),
                Argument(BuildEnumMember("AnimationDirection", direction ?? "Normal")),
                Argument(BuildEnumMember("AnimationFillMode", fillMode ?? "None"))
            ])));
        expression = BuildNewCssValue("CssAnimationValue", animation);
        return true;
    }

    /// <summary>
    /// Reads a non-negative iteration count or <c>infinite</c>.
    /// </summary>
    private static bool TryReadIterationCount(CssValueSyntaxNode node, out double count)
    {
        switch (node)
        {
            case CssNumberValueNode { Value: >= 0 } number:
                count = number.Value;
                return true;
            case CssKeywordValueNode { Keyword: var keyword } when string.Equals(keyword, "infinite", StringComparison.OrdinalIgnoreCase):
                count = double.PositiveInfinity;
                return true;
            default:
                count = 0;
                return false;
        }
    }

    private static bool TryReadAnimationDirection(CssValueSyntaxNode node, out string? member)
    {
        member = node is CssKeywordValueNode keyword
            ? keyword.Keyword.ToLowerInvariant() switch
            {
                "normal" => "Normal",
                "reverse" => "Reverse",
                "alternate" => "Alternate",
                "alternate-reverse" => "AlternateReverse",
                _ => null
            }
            : null;
        return member is not null;
    }

    private static bool TryReadAnimationFillMode(CssValueSyntaxNode node, out string? member)
    {
        member = node is CssKeywordValueNode keyword
            ? keyword.Keyword.ToLowerInvariant() switch
            {
                "forwards" => "Forwards",
                "backwards" => "Backwards",
                "both" => "Both",
                _ => null
            }
            : null;
        return member is not null;
    }

    /// <summary>
    /// Gets the space-separated components of one value. Fails for an empty list and for a comma-separated list of values.
    /// </summary>
    private static bool TryGetComponents(CssValueSyntaxNode node, out IReadOnlyList<CssValueSyntaxNode> components)
    {
        switch (node)
        {
            case CssValueListNode { Separator: CssValueSeparator.Comma } or CssValueListNode { Items.Count: 0 }:
                components = [];
                return false;
            case CssValueListNode list:
                components = list.Items;
                return true;
            default:
                components = [node];
                return true;
        }
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

        // The x-coordinates of both control points must lie in [0, 1].
        if (node is CssFunctionValueNode
            {
                Arguments: [
                    CssNumberValueNode { Value: >= 0 and <= 1 } x1,
                    CssNumberValueNode y1,
                    CssNumberValueNode { Value: >= 0 and <= 1 } x2,
                    CssNumberValueNode y2]
            } function &&
            string.Equals(function.Name, "cubic-bezier", StringComparison.OrdinalIgnoreCase))
        {
            expression = ObjectCreationExpression(ParseTypeName("Easing"))
                .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
                [
                    Argument(DoubleLiteral(x1.Value)),
                    Argument(DoubleLiteral(y1.Value)),
                    Argument(DoubleLiteral(x2.Value)),
                    Argument(DoubleLiteral(y2.Value))
                ])));
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
            CssValueListNode { Separator: CssValueSeparator.Comma } list => string.Join(", ", list.Items.Select(FormatValueSummary)),
            CssValueListNode list => string.Join(" ", list.Items.Select(FormatValueSummary)),
            _ => node.ToString()
        };
    }

    /// <summary>
    /// Builds a <c>var()</c> reference whose optional fallback is built by <paramref name="buildFallback"/>.
    /// </summary>
    private static bool TryBuildVarReference(
        CssFunctionValueNode func,
        SyntaxBuilder buildFallback,
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

        if (!buildFallback(func.Arguments[1], out ExpressionSyntax? fallbackExpr) || fallbackExpr is null)
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

        return TryBuildAutoOrLengthSyntax(node, out expression);
    }

    private static bool TryBuildAutoOrLengthSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode { Keyword: var keyword } &&
            string.Equals(keyword, "auto", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssAutoValue");
            return true;
        }

        return TryBuildLengthSyntax(node, out expression);
    }

    private static bool TryBuildNoneOrLengthSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode { Keyword: var keyword } &&
            string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssNoneValue");
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

    /// <summary>
    /// Builds a <c>padding</c> or <c>margin</c> value of one to four edges. Edges in logical pixels build a
    /// <c>CssThicknessValue</c>; edges with other units or <c>var()</c> references build a <c>CssEdgesValue</c>.
    /// </summary>
    private static bool TryBuildThicknessSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> items) || items.Count is < 1 or > 4)
        {
            return false;
        }

        ExpressionSyntax[] edges = new ExpressionSyntax[items.Count];
        bool allPixels = true;
        for (int i = 0; i < items.Count; i++)
        {
            if (!TryBuildEdgeSyntax(items[i], out ExpressionSyntax? edge) || edge is null)
            {
                return false;
            }

            edges[i] = edge;
            allPixels &= TryReadPixels(items[i], out _);
        }

        if (allPixels)
        {
            expression = BuildThicknessValue(items);
            return true;
        }

        (ExpressionSyntax top, ExpressionSyntax right, ExpressionSyntax bottom, ExpressionSyntax left) = ExpandEdges(edges);
        expression = BuildNewCssValue("CssEdgesValue", top, right, bottom, left);
        return true;
    }

    /// <summary>
    /// Builds a <c>border-radius</c> of one to four lengths. A single length stays a length. Two to four lengths expand in
    /// the order of <c>padding</c> edges, which matches the CSS corner order from top-left.
    /// </summary>
    private static bool TryBuildBorderRadiusSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> items) || items.Count is < 1 or > 4)
        {
            return false;
        }

        return items.Count == 1
            ? TryBuildLengthSyntax(items[0], out expression)
            : TryBuildThicknessSyntax(node, out expression);
    }

    /// <summary>
    /// Builds a length or a <c>var()</c> reference whose fallback is itself an edge.
    /// </summary>
    private static bool TryBuildEdgeSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssFunctionValueNode func && string.Equals(func.Name, "var", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildVarReference(func, TryBuildEdgeSyntax, out expression);
        }

        return TryBuildLengthSyntax(node, out expression);
    }

    private static bool TryReadPixels(CssValueSyntaxNode node, out double pixels)
    {
        switch (node)
        {
            case CssNumberValueNode { Value: 0.0 }:
                pixels = 0.0;
                return true;
            case CssDimensionValueNode dim when string.Equals(dim.Unit, "px", StringComparison.OrdinalIgnoreCase):
                pixels = dim.Value;
                return true;
            default:
                pixels = 0.0;
                return false;
        }
    }

    private static ExpressionSyntax BuildThicknessValue(IReadOnlyList<CssValueSyntaxNode> items)
    {
        ExpressionSyntax[] pixels = new ExpressionSyntax[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            TryReadPixels(items[i], out double value);
            pixels[i] = DoubleLiteral(value);
        }

        (ExpressionSyntax top, ExpressionSyntax right, ExpressionSyntax bottom, ExpressionSyntax left) = ExpandEdges(pixels);
        ObjectCreationExpressionSyntax thicknessCreation = ObjectCreationExpression(
            ParseTypeName("Thickness"))
            .WithArgumentList(ArgumentList(SeparatedList<ArgumentSyntax>(
            [
                Argument(top),
                Argument(right),
                Argument(bottom),
                Argument(left)
            ])));

        return BuildNewCssValue("CssThicknessValue", thicknessCreation);
    }

    /// <summary>
    /// Expands one to four edge values to top, right, bottom, and left, as the CSS box shorthands do.
    /// </summary>
    private static (T Top, T Right, T Bottom, T Left) ExpandEdges<T>(T[] values)
    {
        return values.Length switch
        {
            1 => (values[0], values[0], values[0], values[0]),
            2 => (values[0], values[1], values[0], values[1]),
            3 => (values[0], values[1], values[2], values[1]),
            _ => (values[0], values[1], values[2], values[3])
        };
    }

    private static bool TryBuildFontWeightSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        string? weightMember = node switch
        {
            CssNumberValueNode num => num.Value switch
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

        if (node is CssValueListNode { Separator: CssValueSeparator.Space, Items.Count: 2 } list &&
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
            "sticky" => "Sticky",
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
                "auto" => "Auto",
                "pointer" or "hand" => "Pointer",
                "default" or "arrow" => "Default",
                "text" => "IBeam",
                "not-allowed" => "NotAllowed",
                "crosshair" => "Crosshair",
                "col-resize" => "ColumnResize",
                "row-resize" => "RowResize",
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

    /// <summary>
    /// Builds a <c>box-shadow</c>: the <c>none</c> keyword, or comma-separated shadows.
    /// </summary>
    private static bool TryBuildBoxShadowSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "none", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssBoxShadowValue", BuildImmutableArrayEmpty("CssShadow"));
            return true;
        }

        IReadOnlyList<CssValueSyntaxNode> values = node is CssValueListNode { Separator: CssValueSeparator.Comma } list
            ? list.Items
            : [node];
        if (values.Count == 0)
        {
            return false;
        }

        List<ExpressionSyntax> shadows = [];
        foreach (CssValueSyntaxNode value in values)
        {
            if (!TryBuildShadowSyntax(value, out ExpressionSyntax? shadow) || shadow is null)
            {
                return false;
            }

            shadows.Add(shadow);
        }

        expression = BuildNewCssValue("CssBoxShadowValue", BuildImmutableArrayCreate(shadows));
        return true;
    }

    /// <summary>
    /// Builds one shadow from two to four consecutive lengths (offsets, blur, spread), at most one color, and at most one
    /// <c>inset</c> keyword. The color and the keyword may come before or after the lengths. A <c>var()</c> component is
    /// the color.
    /// </summary>
    private static bool TryBuildShadowSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        expression = null;
        if (!TryGetComponents(node, out IReadOnlyList<CssValueSyntaxNode> items))
        {
            return false;
        }

        List<ExpressionSyntax> lengths = [];
        ExpressionSyntax? color = null;
        bool isInset = false;
        bool lengthsEnded = false;
        foreach (CssValueSyntaxNode item in items)
        {
            if (item is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "inset", StringComparison.OrdinalIgnoreCase))
            {
                if (isInset)
                {
                    return false;
                }

                isInset = true;
                lengthsEnded = lengths.Count > 0;
                continue;
            }

            if (item is not CssPercentageValueNode && TryBuildLengthSyntax(item, out ExpressionSyntax? length) && length is not null)
            {
                if (lengthsEnded || lengths.Count == 4)
                {
                    return false;
                }

                lengths.Add(length);
                continue;
            }

            if (color is not null || !TryBuildValueExpression("color", item, out color, out _) || color is null)
            {
                return false;
            }

            lengthsEnded = lengths.Count > 0;
        }

        if (lengths.Count < 2)
        {
            return false;
        }

        while (lengths.Count < 4)
        {
            lengths.Add(BuildNewCssValue("CssLengthValue", DoubleLiteral(0), BuildEnumMember("CssLengthUnit", "Px")));
        }

        expression = BuildNewCssValue(
            "CssShadow",
            lengths[0],
            lengths[1],
            lengths[2],
            lengths[3],
            color ?? LiteralExpression(SyntaxKind.NullLiteralExpression),
            LiteralExpression(isInset ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression));
        return true;
    }

    /// <summary>
    /// Builds a <c>line-height</c>: the <c>normal</c> keyword, a non-negative number that multiplies the font size, or a
    /// length or percentage of the font size.
    /// </summary>
    private static bool TryBuildLineHeightSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "normal", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssStringValue", StringLiteral("normal"));
            return true;
        }

        if (node is CssNumberValueNode number)
        {
            expression = number.Value >= 0.0 ? BuildNewCssValue("CssNumberValue", DoubleLiteral(number.Value)) : null;
            return expression is not null;
        }

        return TryBuildLengthSyntax(node, out expression);
    }

    /// <summary>
    /// Builds an integer <c>z-index</c>, or zero for <c>auto</c>.
    /// </summary>
    private static bool TryBuildZIndexSyntax(CssValueSyntaxNode node, out ExpressionSyntax? expression)
    {
        if (node is CssNumberValueNode num && num.Value == Math.Floor(num.Value))
        {
            expression = BuildNewCssValue("CssNumberValue", DoubleLiteral(num.Value));
            return true;
        }

        if (node is CssKeywordValueNode { Keyword: var keyword } && string.Equals(keyword, "auto", StringComparison.OrdinalIgnoreCase))
        {
            expression = BuildNewCssValue("CssNumberValue", DoubleLiteral(0));
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
