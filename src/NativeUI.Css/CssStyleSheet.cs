using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS stylesheet containing <see cref="CssRule"/> definitions,
/// root custom properties, and the raw CSS source for Web injection.
/// </summary>
public sealed class CssStyleSheet
{
    /// <summary>
    /// Gets the unique identifier of this compiled stylesheet (typically the view or file name).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the raw CSS stylesheet text used when injecting native <c>&lt;style&gt;</c> tags on Web.
    /// </summary>
    public string RawCss { get; }

    /// <summary>
    /// Gets the ordered list of compiled CSS rules in this stylesheet.
    /// </summary>
    public IReadOnlyList<CssRule> Rules { get; }

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

        CssColorSchemeCondition colorScheme = element.ActualTheme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
        CssVariables effectiveVariables = CreateRootVariables(colorScheme);

        List<(CssRule Rule, int Order)> matching = [];
        for (int i = 0; i < Rules.Count; i++)
        {
            CssRule rule = Rules[i];
            if (rule.Matches(element.TagName, element.Classes, colorScheme))
            {
                matching.Add((rule, i));
            }
        }

        matching.Sort(static (a, b) =>
        {
            int cmp = a.Rule.Specificity.CompareTo(b.Rule.Specificity);
            return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
        });

        // Collect scoped custom property overrides first.
        CssVariables scopedVariables = new(effectiveVariables);
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

        ComputedStyle inheritedStyle = ComputedStyle.CreateInherited(element.Parent?.ComputedStyle);
        Color? textColor = inheritedStyle.TextColor;
        Color? backgroundColor = null;
        double fontSize = inheritedStyle.FontSize;
        FontWeight fontWeight = inheritedStyle.FontWeight;
        string? fontFamily = inheritedStyle.FontFamily;
        TextAlign textAlign = inheritedStyle.TextAlign;
        Thickness padding = Thickness.Zero;
        Thickness margin = Thickness.Zero;
        double borderRadius = 0.0;
        double opacity = 1.0;
        double? width = null;
        double? height = null;

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

                    case "opacity":
                        if (resolved is CssNumberValue op)
                        {
                            opacity = Math.Clamp(op.Value, 0.0, 1.0);
                        }
                        break;

                    case "width":
                        if (resolved is CssLengthValue w)
                        {
                            width = w.ToLogicalPixels(fontSize);
                        }
                        break;

                    case "height":
                        if (resolved is CssLengthValue h)
                        {
                            height = h.ToLogicalPixels(fontSize);
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
            Opacity = opacity,
            Width = width,
            Height = height
        };
        return cssStyle.ApplyInline(element.Style, scopedVariables.ResolveColor);
    }
}
