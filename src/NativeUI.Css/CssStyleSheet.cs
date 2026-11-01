using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS stylesheet containing <see cref="CssRule"/> definitions,
/// root custom properties, and the raw CSS source for Web injection.
/// </summary>
public sealed class CssStyleSheet : StyleSheet
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
    /// Gets the ordered list of compiled CSS rules in this stylesheet.
    /// </summary>
    public new IReadOnlyList<CssRule> Rules { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssStyleSheet"/> class.
    /// </summary>
    public CssStyleSheet(string id, string rawCss, IReadOnlyList<CssRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(rawCss);
        ArgumentNullException.ThrowIfNull(rules);

        Id = id;
        RawCss = rawCss;
        Rules = rules;
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
        CssColorSchemeCondition scheme = Application.Current.CurrentTheme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
        CssVariables variables = CreateScopedVariables(element, scheme);
        return variables.ResolveColor(colorValue);
    }

    /// <summary>
    /// Populates a <see cref="CssVariables"/> instance with all <c>:root</c> custom properties active for the given <paramref name="colorScheme"/>.
    /// </summary>
    public CssVariables CreateRootVariables(
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Light,
        CssVariables? parent = null)
    {
        CssVariables variables = new(parent);
        for (int i = 0; i < Rules.Count; i++)
        {
            CssRule rule = Rules[i];
            if (!rule.IsRoot)
            {
                continue;
            }

            if (rule.ColorScheme != CssColorSchemeCondition.Any && rule.ColorScheme != colorScheme)
            {
                continue;
            }

            foreach (KeyValuePair<string, CssValue> kvp in rule.Declarations)
            {
                if (kvp.Key.StartsWith("--", StringComparison.Ordinal))
                {
                    variables.Set(kvp.Key, kvp.Value);
                }
            }
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

        CssColorSchemeCondition colorScheme = Application.Current.CurrentTheme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
        List<(CssRule Rule, int Order)> matching = CollectMatchingRules(element, colorScheme, CssPseudoElement.None);
        CssVariables scopedVariables = CreateScopedVariables(matching, colorScheme);

        ComputedStyle inheritedStyle = ComputedStyle.CreateInitial(element);
        Color? textColor = inheritedStyle.TextColor;
        Color? backgroundColor = null;
        double fontSize = inheritedStyle.FontSize;
        FontWeight fontWeight = inheritedStyle.FontWeight;
        string? fontFamily = inheritedStyle.FontFamily;
        TextAlign textAlign = inheritedStyle.TextAlign;
        Thickness padding = Thickness.Zero;
        Thickness margin = Thickness.Zero;
        double borderRadius = 0.0;
        double borderWidth = 0.0;
        Color? borderColor = null;
        double opacity = 1.0;
        WidthValue? width = inheritedStyle.Width;
        double? height = inheritedStyle.Height;
        Cursor cursor = inheritedStyle.Cursor;
        Overflow? overflowX = inheritedStyle.OverflowX;
        Overflow? overflowY = inheritedStyle.OverflowY;
        Color? scrollbarThumbColor = inheritedStyle.ScrollbarThumbColor;
        Color? scrollbarTrackColor = inheritedStyle.ScrollbarTrackColor;
        ScrollbarWidth scrollbarWidth = ScrollbarWidth.Auto;
        Position position = Position.Static;
        double? top = null;
        double? right = null;
        double? bottom = null;
        double? left = null;

        // Font-relative lengths use the winning font size, regardless of declaration order.
        foreach ((CssRule rule, _) in matching)
        {
            if (rule.Declarations.TryGetValue("font-size", out CssValue? fontSizeValue)
                && scopedVariables.Resolve(fontSizeValue) is CssLengthValue fontSizeLength)
            {
                fontSize = fontSizeLength.ToLogicalPixels(inheritedStyle.FontSize, inheritedStyle.FontSize);
            }
        }

        fontSize = element.Style?.FontSize ?? fontSize;

        foreach ((CssRule rule, _) in matching)
        {
            foreach (KeyValuePair<string, CssValue> kvp in rule.Declarations)
            {
                if (kvp.Key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                CssValue? resolved = scopedVariables.Resolve(kvp.Value);
                if (resolved is null)
                {
                    continue;
                }

                switch (kvp.Key)
                {
                    case "color":
                        if (resolved is CssColorValue fg)
                        {
                            textColor = fg.Color;
                        }
                        break;

                    case "background-color":
                    case "background":
                        if (resolved is CssColorValue bg)
                        {
                            backgroundColor = bg.Color;
                        }
                        break;

                    case "font-weight":
                        if (resolved is CssFontWeightValue fw)
                        {
                            fontWeight = fw.Weight;
                        }
                        break;

                    case "font-family":
                        if (resolved is CssStringValue ff)
                        {
                            fontFamily = ff.Value;
                        }
                        break;

                    case "text-align":
                        if (resolved is CssTextAlignValue ta)
                        {
                            textAlign = ta.Align;
                        }
                        break;

                    case "padding":
                        if (resolved is CssThicknessValue pad)
                        {
                            padding = pad.Thickness;
                        }
                        else if (resolved is CssLengthValue padLen)
                        {
                            padding = new Thickness(padLen.ToLogicalPixels(fontSize));
                        }
                        break;

                    case "margin":
                        if (resolved is CssThicknessValue mar)
                        {
                            margin = mar.Thickness;
                        }
                        else if (resolved is CssLengthValue marLen)
                        {
                            margin = new Thickness(marLen.ToLogicalPixels(fontSize));
                        }
                        break;

                    case "border-radius":
                        if (resolved is CssLengthValue br)
                        {
                            borderRadius = br.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "border-width":
                        if (resolved is CssLengthValue bw)
                        {
                            borderWidth = bw.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "border-color":
                        if (resolved is CssColorValue bc)
                        {
                            borderColor = bc.Color;
                        }
                        break;

                    case "opacity":
                        if (resolved is CssNumberValue op)
                        {
                            opacity = Math.Clamp(op.Value, 0.0, 1.0);
                        }
                        break;

                    case "scrollbar-color":
                        if (resolved is CssScrollbarColorValue scrollbarColor)
                        {
                            scrollbarThumbColor = (scopedVariables.Resolve(scrollbarColor.Thumb) as CssColorValue)?.Color;
                            scrollbarTrackColor = (scopedVariables.Resolve(scrollbarColor.Track) as CssColorValue)?.Color;
                        }
                        break;

                    case "scrollbar-width":
                        if (resolved is CssScrollbarWidthValue scrollbarWidthValue)
                        {
                            scrollbarWidth = scrollbarWidthValue.Width;
                        }
                        break;

                    case "overflow":
                        if (resolved is CssOverflowValue both)
                        {
                            overflowX = both.Overflow;
                            overflowY = both.Overflow;
                        }
                        break;

                    case "overflow-x":
                        if (resolved is CssOverflowValue x)
                        {
                            overflowX = x.Overflow;
                        }
                        break;

                    case "overflow-y":
                        if (resolved is CssOverflowValue y)
                        {
                            overflowY = y.Overflow;
                        }
                        break;

                    case "width":
                        if (resolved is CssLengthValue w)
                        {
                            width = w.ToLogicalPixels(fontSize);
                        }
                        else if (resolved is CssFitContentValue)
                        {
                            width = WidthValue.FitContent;
                        }
                        break;

                    case "height":
                        if (resolved is CssLengthValue h)
                        {
                            height = h.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "cursor":
                        if (resolved is CssCursorValue cur)
                        {
                            cursor = cur.Cursor;
                        }
                        break;

                    case "position":
                        if (resolved is CssPositionValue pos)
                        {
                            position = pos.Position;
                        }
                        break;

                    case "top":
                        if (resolved is CssLengthValue t)
                        {
                            top = t.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "right":
                        if (resolved is CssLengthValue r)
                        {
                            right = r.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "bottom":
                        if (resolved is CssLengthValue b)
                        {
                            bottom = b.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "left":
                        if (resolved is CssLengthValue l)
                        {
                            left = l.ToLogicalPixels(fontSize);
                        }
                        break;
                }
            }
        }

        ComputedStyle cssStyle = new()
        {
            TextColor = textColor,
            BackgroundColor = backgroundColor,
            FontSize = fontSize,
            FontWeight = fontWeight,
            FontFamily = fontFamily,
            TextAlign = textAlign,
            Padding = padding,
            Margin = margin,
            BorderRadius = borderRadius,
            BorderWidth = borderWidth,
            BorderColor = borderColor,
            Opacity = opacity,
            Width = width,
            Height = height,
            Cursor = cursor,
            OverflowX = overflowX,
            OverflowY = overflowY,
            ScrollbarThumbColor = scrollbarThumbColor,
            ScrollbarTrackColor = scrollbarTrackColor,
            ScrollbarWidth = scrollbarWidth,
            Position = position,
            Top = top,
            Right = right,
            Bottom = bottom,
            Left = left
        };
        return cssStyle;
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
        CssColorSchemeCondition colorScheme = Application.Current.CurrentTheme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
        List<(CssRule Rule, int Order)> selectionRules = CollectMatchingRules(element, colorScheme, CssPseudoElement.Selection);
        if (selectionRules.Count == 0)
        {
            return inherited;
        }

        List<(CssRule Rule, int Order)> elementRules = CollectMatchingRules(element, colorScheme, CssPseudoElement.None);
        CssVariables scopedVariables = CreateScopedVariables(elementRules, colorScheme);

        Color? textColor = inherited.TextColor;
        Color? backgroundColor = inherited.BackgroundColor;
        foreach ((CssRule rule, _) in selectionRules)
        {
            if (rule.Declarations.TryGetValue("color", out CssValue? color)
                && scopedVariables.Resolve(color) is CssColorValue foreground)
            {
                textColor = foreground.Color;
            }

            if (rule.Declarations.TryGetValue("background-color", out CssValue? background)
                && scopedVariables.Resolve(background) is CssColorValue selectionBackground)
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

    /// <summary>
    /// Returns rules for <paramref name="pseudoElement"/> that match the element, ordered by ascending specificity and then source order.
    /// </summary>
    private List<(CssRule Rule, int Order)> CollectMatchingRules(
        IUIElement element,
        CssColorSchemeCondition colorScheme,
        CssPseudoElement pseudoElement)
    {
        List<(CssRule Rule, int Order)> matching = [];
        for (int i = 0; i < Rules.Count; i++)
        {
            CssRule rule = Rules[i];
            if (rule.PseudoElement == pseudoElement && rule.Matches(element, colorScheme))
            {
                matching.Add((rule, i));
            }
        }

        matching.Sort(static (a, b) =>
        {
            int cmp = a.Rule.Specificity.CompareTo(b.Rule.Specificity);
            return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
        });

        return matching;
    }

    /// <summary>
    /// Populates a <see cref="CssVariables"/> instance with all variables active for <paramref name="element"/> in <paramref name="colorScheme"/>.
    /// </summary>
    public CssVariables CreateScopedVariables(
        IUIElement element,
        CssColorSchemeCondition colorScheme)
    {
        ArgumentNullException.ThrowIfNull(element);
        List<(CssRule Rule, int Order)> matching = CollectMatchingRules(element, colorScheme, CssPseudoElement.None);
        return CreateScopedVariables(matching, colorScheme);
    }

    private CssVariables CreateScopedVariables(
        List<(CssRule Rule, int Order)> matching,
        CssColorSchemeCondition colorScheme)
    {
        CssVariables scopedVariables = new(CreateRootVariables(colorScheme));
        foreach ((CssRule rule, _) in matching)
        {
            foreach (KeyValuePair<string, CssValue> kvp in rule.Declarations)
            {
                if (kvp.Key.StartsWith("--", StringComparison.Ordinal))
                {
                    scopedVariables.Set(kvp.Key, kvp.Value);
                }
            }
        }

        return scopedVariables;
    }
}
