using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Media.Animation;
using NativeUI.Styling;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS stylesheet containing <see cref="CssRule"/> definitions,
/// root custom properties, and the raw CSS source for Web injection.
/// </summary>
public class CssStyleSheet : StyleSheet
{
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

    /// <inheritdoc />
    public override bool HasRules => CssRules.Count > 0 || base.HasRules;

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
    /// Creates the read-only custom properties computed for <paramref name="element"/>: its winning declarations under
    /// <paramref name="colorScheme"/>, inheriting the computed custom properties of its parent's
    /// <see cref="IUIElement.ComputedStyle"/>.
    /// </summary>
    public CssVariables CreateScopedVariables(IUIElement element, CssColorSchemeCondition colorScheme)
    {
        ArgumentNullException.ThrowIfNull(element);

        List<(CssRule Rule, int Order)> matching = CollectMatchingRules(element, colorScheme, pseudoElement: null);
        return CreateCustomProperties(element, matching);
    }

    /// <summary>
    /// Computes the cascaded <see cref="ComputedStyle"/> for an element using its CSS classes, theme, and inherited and inline styles.
    /// Custom properties and other declarations share one cascade over the application stylesheet and this stylesheet.
    /// </summary>
    /// <param name="element">The element whose style is being computed.</param>
    public ComputedStyle ComputeStyle(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        CssColorSchemeCondition colorScheme = GetActiveColorScheme();
        List<(CssRule Rule, int Order)> matching = CollectMatchingRules(element, colorScheme, pseudoElement: null);
        CssVariables variables = CreateCustomProperties(element, matching);
        ComputedStyle style = ComputedStyle.CreateInitial(element) with { CustomProperties = variables };
        double fontSize = ResolveFontSize(element, matching, variables, style.FontSize);
        style = style with { FontSize = fontSize };

        foreach ((CssRule rule, _) in matching)
        {
            foreach (KeyValuePair<string, CssValue> declaration in rule.Declarations)
            {
                if (!IsCustomProperty(declaration.Key) &&
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

    private static bool IsCustomProperty(string property)
    {
        return property.StartsWith("--", StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the element's custom properties from its winning declarations, or returns the inherited ones when it
    /// declares none. Returns the element's current custom properties when they are unchanged, which keeps an
    /// unchanged <see cref="ComputedStyle"/> equal to its previous snapshot.
    /// </summary>
    /// <param name="element">The element whose custom properties are computed.</param>
    /// <param name="matching">The element's matching rules in ascending cascade order.</param>
    private static CssVariables CreateCustomProperties(IUIElement element, List<(CssRule Rule, int Order)> matching)
    {
        CssVariables inherited = element.Parent?.ComputedStyle.CustomProperties as CssVariables ?? CssVariables.Empty;
        CssVariables? declared = null;

        // Visiting rules from the highest precedence lets the first declaration of each name win.
        for (int i = matching.Count - 1; i >= 0; i--)
        {
            foreach (KeyValuePair<string, CssValue> declaration in matching[i].Rule.Declarations)
            {
                if (IsCustomProperty(declaration.Key))
                {
                    declared ??= new CssVariables(inherited, isReadOnly: true);
                    declared.Declare(declaration.Key, declaration.Value);
                }
            }
        }

        if (declared is null)
        {
            return inherited;
        }

        if (element.ComputedStyle.CustomProperties is CssVariables current && current.HasSameDeclarations(declared))
        {
            return current;
        }

        return declared;
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
            if (IsCustomProperty(declaration.Key))
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
            case ("line-height", CssNumberValue multiple):
                return style with { LineHeight = LineHeight.Multiple(multiple.Value) };
            case ("line-height", CssLengthValue length):
                return style with { LineHeight = LineHeight.Pixels(length.ToLogicalPixels(fontSize, fontSize)) };
            case ("line-height", CssStringValue { Value: "normal" }):
                return style with { LineHeight = LineHeight.Normal };
            case ("padding", CssThicknessValue padding):
                return style with { Padding = padding.Thickness };
            case ("padding", CssLengthValue padding):
                return style with { Padding = new Thickness(padding.ToLogicalPixels(fontSize)) };
            case ("padding", CssEdgesValue padding) when TryResolveThickness(padding, fontSize, variables, out Thickness thickness):
                return style with { Padding = thickness };
            case ("margin", CssThicknessValue margin):
                return style with { Margin = margin.Thickness };
            case ("margin", CssLengthValue margin):
                return style with { Margin = new Thickness(margin.ToLogicalPixels(fontSize)) };
            case ("margin", CssEdgesValue margin) when TryResolveThickness(margin, fontSize, variables, out Thickness thickness):
                return style with { Margin = thickness };
            case ("gap", CssLengthValue gap):
                return style with { Gap = gap.ToLogicalPixels(fontSize) };
            case ("border-radius", CssLengthValue borderRadius):
                return style with { BorderRadius = borderRadius.ToLogicalPixels(fontSize) };
            case ("border-radius", CssThicknessValue borderRadius):
                return style with { BorderRadius = ToCornerRadius(borderRadius.Thickness) };
            case ("border-radius", CssEdgesValue borderRadius) when TryResolveThickness(borderRadius, fontSize, variables, out Thickness corners):
                return style with { BorderRadius = ToCornerRadius(corners) };
            case ("border-top-left-radius", CssLengthValue radius):
                return style with { BorderRadius = style.BorderRadius with { TopLeft = radius.ToLogicalPixels(fontSize) } };
            case ("border-top-right-radius", CssLengthValue radius):
                return style with { BorderRadius = style.BorderRadius with { TopRight = radius.ToLogicalPixels(fontSize) } };
            case ("border-bottom-right-radius", CssLengthValue radius):
                return style with { BorderRadius = style.BorderRadius with { BottomRight = radius.ToLogicalPixels(fontSize) } };
            case ("border-bottom-left-radius", CssLengthValue radius):
                return style with { BorderRadius = style.BorderRadius with { BottomLeft = radius.ToLogicalPixels(fontSize) } };
            case ("border-width", CssLengthValue borderWidth):
                return style with { BorderWidth = borderWidth.ToLogicalPixels(fontSize) };
            case ("border-color", CssColorValue borderColor):
                return style with { BorderColor = borderColor.Color };
            case ("box-shadow", CssBoxShadowValue boxShadow):
                return style with { BoxShadows = ResolveBoxShadows(boxShadow, fontSize, variables) };
            case ("z-index", CssNumberValue zIndex):
                return style with { ZIndex = (int)zIndex.Value };
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
            case ("min-width", CssLengthValue minWidth):
                return style with { MinWidth = ToWidth(minWidth, fontSize) };
            case ("min-width", CssAutoValue):
                return style with { MinWidth = null };
            case ("max-width", CssLengthValue maxWidth):
                return style with { MaxWidth = ToWidth(maxWidth, fontSize) };
            case ("max-width", CssNoneValue):
                return style with { MaxWidth = null };
            case ("min-height", CssLengthValue minHeight):
                return style with { MinHeight = minHeight.ToLogicalPixels(fontSize) };
            case ("min-height", CssAutoValue):
                return style with { MinHeight = null };
            case ("max-height", CssLengthValue maxHeight):
                return style with { MaxHeight = maxHeight.ToLogicalPixels(fontSize) };
            case ("max-height", CssNoneValue):
                return style with { MaxHeight = null };
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

    /// <summary>
    /// Resolves each edge to a length in logical pixels. Fails when any edge is invalid or is not a length, which makes
    /// the declaration invalid at computed-value time.
    /// </summary>
    private static ImmutableArray<BoxShadow> ResolveBoxShadows(CssBoxShadowValue value, double fontSize, CssVariables variables)
    {
        ImmutableArray<BoxShadow>.Builder shadows = ImmutableArray.CreateBuilder<BoxShadow>(value.Shadows.Length);
        foreach (CssShadow shadow in value.Shadows)
        {
            Color? color = shadow.Color is CssValue specified ? (variables.Resolve(specified) as CssColorValue)?.Color : null;
            shadows.Add(new BoxShadow(
                shadow.OffsetX.ToLogicalPixels(fontSize),
                shadow.OffsetY.ToLogicalPixels(fontSize),
                Math.Max(0, shadow.BlurRadius.ToLogicalPixels(fontSize)),
                shadow.SpreadRadius.ToLogicalPixels(fontSize),
                color,
                shadow.IsInset));
        }

        return shadows.MoveToImmutable();
    }

    /// <summary>
    /// Converts the four values of a <c>border-radius</c>, which expand in the order of <c>padding</c> edges, to corners:
    /// top to top-left, right to top-right, bottom to bottom-right, and left to bottom-left.
    /// </summary>
    private static CornerRadius ToCornerRadius(Thickness values)
    {
        return new CornerRadius(values.Top, values.Right, values.Bottom, values.Left);
    }

    private static bool TryResolveThickness(CssEdgesValue edges, double fontSize, CssVariables variables, out Thickness thickness)
    {
        if (variables.Resolve(edges.Top) is CssLengthValue top &&
            variables.Resolve(edges.Right) is CssLengthValue right &&
            variables.Resolve(edges.Bottom) is CssLengthValue bottom &&
            variables.Resolve(edges.Left) is CssLengthValue left)
        {
            thickness = new Thickness(
                top.ToLogicalPixels(fontSize),
                right.ToLogicalPixels(fontSize),
                bottom.ToLogicalPixels(fontSize),
                left.ToLogicalPixels(fontSize));
            return true;
        }

        thickness = default;
        return false;
    }

    private static WidthValue ToWidth(CssLengthValue length, double fontSize)
    {
        return length.Unit == CssLengthUnit.Percent
            ? WidthValue.Percent(length.Value)
            : WidthValue.Pixels(length.ToLogicalPixels(fontSize));
    }

    /// <summary>
    /// Returns rules for <paramref name="pseudoElement"/> that match the element, ordered by ascending specificity and
    /// then source order. The rules of the application stylesheet, which <see cref="IUIElement.StyleSheetId"/> names,
    /// precede this stylesheet's rules in source order, so this stylesheet wins ties as it does when both are adopted
    /// by the browser.
    /// </summary>
    private List<(CssRule Rule, int Order)> CollectMatchingRules(
        IUIElement element,
        CssColorSchemeCondition colorScheme,
        string? pseudoElement)
    {
        List<(CssRule Rule, int Order)> matching = [];
        int order = 0;
        if (element.StyleSheetId is string scopeId &&
            CssStyleRegistry.GetOrDefault(scopeId) is CssStyleSheet applicationSheet &&
            !ReferenceEquals(applicationSheet, this))
        {
            applicationSheet.AddMatchingRules(element, colorScheme, pseudoElement, matching, ref order);
        }

        AddMatchingRules(element, colorScheme, pseudoElement, matching, ref order);
        matching.Sort(CompareCascadeOrder);
        return matching;
    }

    private void AddMatchingRules(
        IUIElement element,
        CssColorSchemeCondition colorScheme,
        string? pseudoElement,
        List<(CssRule Rule, int Order)> matching,
        ref int order)
    {
        foreach (CssRule rule in CssRules)
        {
            bool pseudoMatch = pseudoElement is null
                ? rule.PseudoElementName is null
                : string.Equals(rule.PseudoElementName?.TrimStart(':'), pseudoElement, StringComparison.OrdinalIgnoreCase);

            if (pseudoMatch && rule.Matches(element, colorScheme))
            {
                matching.Add((rule, order));
            }

            order++;
        }
    }
}
