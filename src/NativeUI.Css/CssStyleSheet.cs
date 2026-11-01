using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS stylesheet containing <see cref="CssRule"/> definitions,
/// root custom properties, and the raw CSS source for Web injection.
/// </summary>
public class CssStyleSheet : StyleSheet
{
    // Rules declaring custom properties, in cascade order, which is the only part of the stylesheet that inheritance reads.
    private readonly CssRule[] _variableRules;
    private readonly CssVariables _lightRootVariables;
    private readonly CssVariables _darkRootVariables;

    /// <summary>
    /// Gets the unique identifier of this compiled stylesheet (typically the view or file name).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the browser CSS text injected on Web, with NativeUI element type selectors translated to HTML elements.
    /// </summary>
    public string RawCss { get; }

    /// <summary>
    /// Gets the compiled CSS rules in source order. <see cref="StyleSheet.Rules"/> holds only rules added from C# and is
    /// empty for a compiled stylesheet.
    /// </summary>
    public IReadOnlyList<CssRule> CssRules { get; }

    /// <summary>
    /// Gets the compiled <c>@keyframes</c> rules in this stylesheet.
    /// </summary>
    public ImmutableArray<CssKeyframes> Keyframes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssStyleSheet"/> class with a copy of <paramref name="rules"/>.
    /// </summary>
    public CssStyleSheet(string id, string rawCss, IReadOnlyList<CssRule> rules, ImmutableArray<CssKeyframes> keyframes = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(rawCss);
        ArgumentNullException.ThrowIfNull(rules);

        Id = id;
        RawCss = rawCss;
        CssRules = [.. rules];
        Keyframes = keyframes.IsDefault ? [] : keyframes;
        _variableRules = CreateVariableRules(CssRules);
        _lightRootVariables = CreateRootVariables(CssColorSchemeCondition.Light);
        _darkRootVariables = CreateRootVariables(CssColorSchemeCondition.Dark);
    }

    /// <inheritdoc />
    public override ComputedStyle ResolveStyle(IUIElement element)
    {
        return ComputeStyle(element);
    }

    /// <inheritdoc />
    public override SelectionStyle ResolveSelectionStyle(IUIElement element)
    {
        return ComputeSelectionStyle(element);
    }

    /// <inheritdoc />
    public override Color? ResolveColor(ThemeValue<Color> colorValue, IUIElement element)
    {
        return CreateScopedVariables(element, GetActiveColorScheme()).ResolveColor(colorValue);
    }

    /// <inheritdoc />
    public override IReadOnlyList<AnimationKeyframe>? ResolveKeyframes(string name, IUIElement element)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(element);

        // As in CSS, the last rule with a name replaces earlier ones.
        CssKeyframes? keyframes = null;
        foreach (CssKeyframes candidate in Keyframes)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                keyframes = candidate;
            }
        }

        if (keyframes is null)
        {
            return null;
        }

        CssVariables variables = CreateScopedVariables(element, GetActiveColorScheme());
        List<AnimationKeyframe> frames = [];
        foreach (CssKeyframe frame in keyframes.Frames)
        {
            frames.Add(ResolveKeyframe(frame, variables, element.ComputedStyle.FontSize));
        }

        frames.Sort(static (first, second) => first.Offset.CompareTo(second.Offset));
        return frames;
    }

    /// <summary>
    /// Populates a <see cref="CssVariables"/> instance with all <c>:root</c> custom properties active for the given <paramref name="colorScheme"/>.
    /// </summary>
    public CssVariables CreateRootVariables(
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Light,
        CssVariables? parent = null)
    {
        CssVariables variables = new(parent);
        foreach (CssRule rule in CssRules)
        {
            if (rule.IsRoot && rule.Media.Matches(colorScheme))
            {
                SetVariables(rule, variables);
            }
        }

        return variables;
    }

    /// <summary>
    /// Creates the custom properties in effect on <paramref name="element"/> for <paramref name="colorScheme"/>: the
    /// <c>:root</c> properties, overridden by those matching each ancestor from the outermost in, then by its own.
    /// </summary>
    public CssVariables CreateScopedVariables(IUIElement element, CssColorSchemeCondition colorScheme)
    {
        ArgumentNullException.ThrowIfNull(element);

        CssVariables variables = new(colorScheme == CssColorSchemeCondition.Dark ? _darkRootVariables : _lightRootVariables);
        if (_variableRules.Length > 0)
        {
            ApplyVariableRules(element, colorScheme, variables);
        }

        return variables;
    }

    /// <summary>
    /// Computes the cascaded <see cref="ComputedStyle"/> for an element using its CSS classes, theme, and inherited and inline styles.
    /// </summary>
    /// <param name="element">The element whose style is being computed.</param>
    public ComputedStyle ComputeStyle(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        CssColorSchemeCondition colorScheme = GetActiveColorScheme();
        List<(CssRule Rule, int Order)> matching = CollectMatchingRules(element, colorScheme, pseudoElement: null);
        CssVariables variables = CreateScopedVariables(element, colorScheme);

        ComputedStyle style = ComputedStyle.CreateInitial(element);
        double fontSize = ResolveFontSize(element, matching, variables, style.FontSize);
        style = style with { FontSize = fontSize };

        foreach ((CssRule rule, _) in matching)
        {
            foreach (KeyValuePair<string, CssValue> declaration in rule.Declarations)
            {
                if (!declaration.Key.StartsWith("--", StringComparison.Ordinal) &&
                    variables.Resolve(declaration.Value) is CssValue resolved)
                {
                    style = ApplyDeclaration(style, declaration.Key, resolved, fontSize, variables);
                }
            }
        }

        return style;
    }

    /// <summary>
    /// Computes the element's selected-text colors from <c>::selection</c> rules. Unspecified colors inherit from the
    /// parent's selection style, and variables resolve against the originating element.
    /// </summary>
    /// <param name="element">The element whose selection style is being computed.</param>
    public SelectionStyle ComputeSelectionStyle(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        SelectionStyle inherited = element.Parent?.SelectionStyle ?? SelectionStyle.Default;
        CssColorSchemeCondition colorScheme = GetActiveColorScheme();
        List<(CssRule Rule, int Order)> selectionRules = CollectMatchingRules(element, colorScheme, pseudoElement: "selection");
        if (selectionRules.Count == 0)
        {
            return inherited;
        }

        CssVariables variables = CreateScopedVariables(element, colorScheme);
        Color? textColor = inherited.TextColor;
        Color? backgroundColor = inherited.BackgroundColor;
        foreach ((CssRule rule, _) in selectionRules)
        {
            if (rule.Declarations.TryGetValue("color", out CssValue? color)
                && variables.Resolve(color) is CssColorValue foreground)
            {
                textColor = foreground.Color;
            }

            if (rule.Declarations.TryGetValue("background-color", out CssValue? background)
                && variables.Resolve(background) is CssColorValue selectionBackground)
            {
                backgroundColor = selectionBackground.Color;
            }
        }

        return new SelectionStyle
        {
            TextColor = textColor,
            BackgroundColor = backgroundColor
        };
    }

    private static CssColorSchemeCondition GetActiveColorScheme()
    {
        return Application.Current.CurrentTheme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
    }

    private static CssRule[] CreateVariableRules(IReadOnlyList<CssRule> rules)
    {
        List<(CssRule Rule, int Order)> variableRules = [];
        for (int i = 0; i < rules.Count; i++)
        {
            CssRule rule = rules[i];
            if (!rule.IsRoot && rule.PseudoElementName is null && DeclaresVariables(rule))
            {
                variableRules.Add((rule, i));
            }
        }

        variableRules.Sort(CompareCascadeOrder);

        CssRule[] ordered = new CssRule[variableRules.Count];
        for (int i = 0; i < ordered.Length; i++)
        {
            ordered[i] = variableRules[i].Rule;
        }

        return ordered;
    }

    private static bool DeclaresVariables(CssRule rule)
    {
        foreach (string property in rule.Declarations.Keys)
        {
            if (property.StartsWith("--", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Orders rules by ascending specificity and then source order, so a later rule overrides an earlier one.
    /// </summary>
    private static int CompareCascadeOrder((CssRule Rule, int Order) first, (CssRule Rule, int Order) second)
    {
        int specificity = first.Rule.Specificity.CompareTo(second.Rule.Specificity);
        return specificity != 0 ? specificity : first.Order.CompareTo(second.Order);
    }

    private static void SetVariables(CssRule rule, CssVariables variables)
    {
        foreach (KeyValuePair<string, CssValue> declaration in rule.Declarations)
        {
            if (declaration.Key.StartsWith("--", StringComparison.Ordinal))
            {
                variables.Set(declaration.Key, declaration.Value);
            }
        }
    }

    /// <summary>
    /// Resolves the font size that font-relative lengths use, which is the winning one regardless of declaration order.
    /// </summary>
    private static double ResolveFontSize(
        IUIElement element,
        List<(CssRule Rule, int Order)> matching,
        CssVariables variables,
        double inheritedFontSize)
    {
        double fontSize = inheritedFontSize;
        foreach ((CssRule rule, _) in matching)
        {
            if (rule.Declarations.TryGetValue("font-size", out CssValue? value)
                && variables.Resolve(value) is CssLengthValue length)
            {
                fontSize = length.ToLogicalPixels(inheritedFontSize, inheritedFontSize);
            }
        }

        return element.Style?.FontSize ?? fontSize;
    }

    private static AnimationKeyframe ResolveKeyframe(CssKeyframe frame, CssVariables variables, double fontSize)
    {
        ComputedStyle style = ComputedStyle.Default;
        AnimatableProperty properties = AnimatableProperty.None;
        foreach (KeyValuePair<string, CssValue> declaration in frame.Declarations)
        {
            if (variables.Resolve(declaration.Value) is CssValue resolved)
            {
                ComputedStyle applied = ApplyDeclaration(style, declaration.Key, resolved, fontSize, variables);
                if (!ReferenceEquals(applied, style))
                {
                    style = applied;
                    properties |= GetAnimatableProperty(declaration.Key);
                }
            }
        }

        return new AnimationKeyframe(frame.Offset, properties, style);
    }

    private static AnimatableProperty GetAnimatableProperty(string property)
    {
        return property switch
        {
            "opacity" => AnimatableProperty.Opacity,
            "transform" => AnimatableProperty.Transform,
            "color" => AnimatableProperty.Color,
            "background-color" or "background" => AnimatableProperty.BackgroundColor,
            "border-color" => AnimatableProperty.BorderColor,
            "width" => AnimatableProperty.Width,
            "height" => AnimatableProperty.Height,
            "top" => AnimatableProperty.Top,
            "right" => AnimatableProperty.Right,
            "bottom" => AnimatableProperty.Bottom,
            "left" => AnimatableProperty.Left,
            _ => AnimatableProperty.None
        };
    }

    /// <summary>
    /// Returns <paramref name="style"/> with one resolved declaration applied, or the same instance when the property is
    /// unknown or the value has the wrong type, which a variable can cause. <c>font-size</c> is resolved separately.
    /// </summary>
    private static ComputedStyle ApplyDeclaration(
        ComputedStyle style,
        string property,
        CssValue value,
        double fontSize,
        CssVariables variables)
    {
        switch (property, value)
        {
            case ("color", CssColorValue color):
                return style with { TextColor = color.Color };
            case ("background-color" or "background", CssColorValue background):
                return style with { BackgroundColor = background.Color };
            case ("font-weight", CssFontWeightValue fontWeight):
                return style with { FontWeight = fontWeight.Weight };
            case ("font-family", CssStringValue fontFamily):
                return style with { FontFamily = fontFamily.Value };
            case ("text-align", CssTextAlignValue textAlign):
                return style with { TextAlign = textAlign.Align };
            case ("padding", CssThicknessValue padding):
                return style with { Padding = padding.Thickness };
            case ("padding", CssLengthValue padding):
                return style with { Padding = new Thickness(padding.ToLogicalPixels(fontSize)) };
            case ("margin", CssThicknessValue margin):
                return style with { Margin = margin.Thickness };
            case ("margin", CssLengthValue margin):
                return style with { Margin = new Thickness(margin.ToLogicalPixels(fontSize)) };
            case ("border-radius", CssLengthValue borderRadius):
                return style with { BorderRadius = borderRadius.ToLogicalPixels(fontSize) };
            case ("border-width", CssLengthValue borderWidth):
                return style with { BorderWidth = borderWidth.ToLogicalPixels(fontSize) };
            case ("border-color", CssColorValue borderColor):
                return style with { BorderColor = borderColor.Color };
            case ("opacity", CssNumberValue opacity):
                return style with { Opacity = Math.Clamp(opacity.Value, 0.0, 1.0) };
            case ("scrollbar-color", CssScrollbarColorValue scrollbarColor):
                return style with
                {
                    ScrollbarThumbColor = (variables.Resolve(scrollbarColor.Thumb) as CssColorValue)?.Color,
                    ScrollbarTrackColor = (variables.Resolve(scrollbarColor.Track) as CssColorValue)?.Color
                };
            case ("scrollbar-width", CssScrollbarWidthValue scrollbarWidth):
                return style with { ScrollbarWidth = scrollbarWidth.Width };
            case ("overflow", CssOverflowValue overflow):
                return style with { OverflowX = overflow.Overflow, OverflowY = overflow.Overflow };
            case ("overflow-x", CssOverflowValue overflowX):
                return style with { OverflowX = overflowX.Overflow };
            case ("overflow-y", CssOverflowValue overflowY):
                return style with { OverflowY = overflowY.Overflow };
            case ("width", CssLengthValue width):
                return style with { Width = ToWidth(width, fontSize) };
            case ("width", CssFitContentValue):
                return style with { Width = WidthValue.FitContent };
            case ("width", CssAutoValue):
                return style with { Width = WidthValue.Auto };
            case ("height", CssLengthValue height):
                return style with { Height = height.ToLogicalPixels(fontSize) };
            case ("height", CssAutoValue):
                return style with { Height = null };
            case ("cursor", CssCursorValue cursor):
                return style with { Cursor = cursor.Cursor };
            case ("position", CssPositionValue position):
                return style with { Position = position.Position };
            case ("top", CssLengthValue top):
                return style with { Top = top.ToLogicalPixels(fontSize) };
            case ("right", CssLengthValue right):
                return style with { Right = right.ToLogicalPixels(fontSize) };
            case ("bottom", CssLengthValue bottom):
                return style with { Bottom = bottom.ToLogicalPixels(fontSize) };
            case ("left", CssLengthValue left):
                return style with { Left = left.ToLogicalPixels(fontSize) };
            case ("transform", CssTransformValue transform):
                return style with { Transform = transform.Transform };
            case ("transition", CssTransitionValue transition):
                return style with { Transitions = transition.Transitions };
            case ("animation", CssAnimationValue animation):
                return style with { Animation = animation.Animation };
            default:
                return style;
        }
    }

    private static WidthValue ToWidth(CssLengthValue length, double fontSize)
    {
        return length.Unit == CssLengthUnit.Percent
            ? WidthValue.Percent(length.Value)
            : WidthValue.Pixels(length.ToLogicalPixels(fontSize));
    }

    /// <summary>
    /// Sets the custom properties of <paramref name="element"/>'s ancestors, outermost first, and then its own.
    /// </summary>
    private void ApplyVariableRules(IUIElement element, CssColorSchemeCondition colorScheme, CssVariables variables)
    {
        if (element.Parent is IUIElement parent)
        {
            ApplyVariableRules(parent, colorScheme, variables);
        }

        foreach (CssRule rule in _variableRules)
        {
            if (rule.Matches(element, colorScheme))
            {
                SetVariables(rule, variables);
            }
        }
    }

    /// <summary>
    /// Returns rules for <paramref name="pseudoElement"/> that match the element, ordered by ascending specificity and then source order.
    /// </summary>
    private List<(CssRule Rule, int Order)> CollectMatchingRules(
        IUIElement element,
        CssColorSchemeCondition colorScheme,
        string? pseudoElement)
    {
        List<(CssRule Rule, int Order)> matching = [];
        for (int i = 0; i < CssRules.Count; i++)
        {
            CssRule rule = CssRules[i];
            bool pseudoMatch = pseudoElement is null
                ? rule.PseudoElementName is null
                : string.Equals(rule.PseudoElementName?.TrimStart(':'), pseudoElement, StringComparison.OrdinalIgnoreCase);

            if (pseudoMatch && rule.Matches(element, colorScheme))
            {
                matching.Add((rule, i));
            }
        }

        matching.Sort(CompareCascadeOrder);
        return matching;
    }
}
