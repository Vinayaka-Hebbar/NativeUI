using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies scoped <see cref="StyleSheet"/> behavior on <see cref="View"/> instances,
/// including inheritance, selector specificity, precedence, relative font lengths, and invalidation.
/// </summary>
internal static class ViewStyleSheetTests
{
    private static readonly Color s_red = Color.FromRgbBytes(255, 0, 0);
    private static readonly Color s_yellow = Color.FromRgbBytes(255, 255, 0);

    internal static void VerifyScopedApplicationAndDescendants()
    {
        StyleSheet cardSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Type<Text>().Class("title"))
                    .Style(new Style { Color = Colors.Blue, FontSize = 26 }),
                StyleRule.Selector(".card")
                    .Style(new Style { Padding = new Thickness(16) })
            }
        };

        using View card = new()
        {
            Class = "card",
            StyleSheet = cardSheet
        };

        Text cardTitle = new() { Class = "title" };
        Text cardBody = new() { Class = "body" };
        card.Children.Add(cardTitle);
        card.Children.Add(cardBody);

        using View siblingCard = new() { Class = "card" };
        Text siblingTitle = new() { Class = "title" };
        siblingCard.Children.Add(siblingTitle);

        using View container = new();
        container.Children.Add(card);
        container.Children.Add(siblingCard);

        Assert.Equal(Colors.Blue, cardTitle.ComputedStyle.TextColor);
        Assert.Equal(26.0, cardTitle.ComputedStyle.FontSize);
        Assert.Equal(new Thickness(16), card.ComputedStyle.Padding);

        Assert.Null(cardBody.ComputedStyle.TextColor);
        Assert.Null(siblingTitle.ComputedStyle.TextColor);
        Assert.Equal(Thickness.Zero, siblingCard.ComputedStyle.Padding);
    }

    internal static void VerifyOuterAndInnerSpecificityAndPrecedence()
    {
        StyleSheet outerSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Class("item"))
                    .Style(new Style { Color = s_red }),
                StyleRule.Selector(s => s.Type<Text>().Class("highlight"))
                    .Style(new Style { Color = Colors.Blue, FontSize = 28 })
            }
        };

        StyleSheet innerSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Class("item"))
                    .Style(new Style { Color = Colors.Green }),
                StyleRule.Selector(s => s.Class("highlight"))
                    .Style(new Style { Color = s_yellow, FontSize = 18 })
            }
        };

        using View outer = new() { StyleSheet = outerSheet };
        using View inner = new() { StyleSheet = innerSheet };
        Text item = new() { Class = "item" };
        Text highlight = new() { Class = "highlight" };

        inner.Children.Add(item);
        inner.Children.Add(highlight);
        outer.Children.Add(inner);

        Assert.Equal(Colors.Green, item.ComputedStyle.TextColor);

        Assert.Equal(Colors.Blue, highlight.ComputedStyle.TextColor);
        Assert.Equal(28.0, highlight.ComputedStyle.FontSize);
    }

    internal static void VerifyDeclarationOrderInSameSheet()
    {
        StyleSheet sheet = new()
        {
            Rules =
            {
                StyleRule.Selector(".badge")
                    .Style(new Style { Color = Colors.White, BackgroundColor = Colors.Blue }),
                StyleRule.Selector(".badge")
                    .Style(new Style { Color = Colors.Black })
            }
        };

        using View view = new() { StyleSheet = sheet };
        Text badge = new() { Class = "badge" };
        view.Children.Add(badge);

        Assert.Equal(Colors.Black, badge.ComputedStyle.TextColor);
        Assert.Equal(Colors.Blue, badge.ComputedStyle.BackgroundColor);
    }

    internal static void VerifyInlineOverridesViewStyleSheet()
    {
        StyleSheet sheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Type<Text>().Class("title"))
                    .Style(new Style { Color = Colors.Blue, FontSize = 26 })
            }
        };

        using View view = new() { StyleSheet = sheet };
        Text title = new()
        {
            Class = "title",
            Style = new Style { Color = s_red }
        };
        view.Children.Add(title);

        Assert.Equal(s_red, title.ComputedStyle.TextColor);
        Assert.Equal(26.0, title.ComputedStyle.FontSize);
    }

    internal static void VerifyAncestorSelectorMatching()
    {
        StyleSheet sheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Class("header").Descendant(d => d.Class("label")))
                    .Style(new Style { Color = Colors.Green }),
                StyleRule.Selector(".outside .label")
                    .Style(new Style { Color = s_red })
            }
        };

        using View container = new() { Class = "outside", StyleResolver = new StyleSheet() };
        using View root = new() { StyleSheet = sheet, StyleResolver = new StyleSheet() };
        using View header = new() { Class = "header" };
        Text labelInHeader = new() { Class = "label" };
        Text labelOutside = new() { Class = "label" };

        header.Children.Add(labelInHeader);
        root.Children.Add(header);
        root.Children.Add(labelOutside);
        container.Children.Add(root);

        Assert.Equal(Colors.Green, labelInHeader.ComputedStyle.TextColor);
        Assert.Null(labelOutside.ComputedStyle.TextColor);
    }

    internal static void VerifyRelativeFontLengthsUseEffectiveFontSize()
    {
        CssStyleSheet cssSheet = new(
            nameof(ScopedFontLengthView),
            string.Empty,
            [
                new CssRule(CssSelector.Class("title"), new Dictionary<string, CssValue>
                {
                    ["margin"] = new CssLengthValue(1, CssLengthUnit.Em)
                })
            ]);
        CssStyleRegistry.Register(cssSheet);

        StyleSheet viewSheet = new()
        {
            Rules =
            {
                StyleRule.Selector("view")
                    .Style(new Style { FontSize = 30 })
            }
        };

        using ScopedFontLengthView view = new()
        {
            StyleResolver = CssStyleResolver.Instance,
            StyleSheet = viewSheet
        };
        Text title = new() { Class = "title" };
        view.Children.Add(title);

        Assert.Equal(30.0, view.ComputedStyle.FontSize);
        Assert.Equal(new Thickness(30), title.ComputedStyle.Margin);
    }

    internal static void VerifyThemeVariablesResolution()
    {
        CssStyleSheet cssSheet = new(
            nameof(ThemeVarView),
            string.Empty,
            [
                new CssRule(CssSelector.Root(), new Dictionary<string, CssValue>
                {
                    ["--brand-color"] = new CssColorValue(Colors.Blue)
                })
            ]);
        CssStyleRegistry.Register(cssSheet);

        StyleSheet viewSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(".themed")
                    .Style(new Style
                    {
                        Color = ThemeValue<Color>.Variable("--brand-color")
                    })
            }
        };

        using ThemeVarView view = new()
        {
            StyleResolver = CssStyleResolver.Instance,
            StyleSheet = viewSheet
        };
        Text text = new() { Class = "themed" };
        view.Children.Add(text);

        Assert.Equal(Colors.Blue, text.ComputedStyle.TextColor);
    }

    internal static void VerifyStyleSheetReplacementAndRemoval()
    {
        StyleSheet sheetA = new()
        {
            Rules =
            {
                StyleRule.Selector(".label")
                    .Style(new Style { Color = s_red })
            }
        };

        StyleSheet sheetB = new()
        {
            Rules =
            {
                StyleRule.Selector(".label")
                    .Style(new Style { Color = Colors.Green })
            }
        };

        using View view = new() { StyleSheet = sheetA };
        Text label = new() { Class = "label" };
        view.Children.Add(label);

        Assert.Equal(s_red, label.ComputedStyle.TextColor);

        int invalidations = 0;
        label.VisualInvalidated += (_, _) => invalidations++;

        view.StyleSheet = sheetB;
        Assert.Equal(Colors.Green, label.ComputedStyle.TextColor);
        Assert.Equal(1, invalidations);

        view.StyleSheet = null;
        Assert.Null(label.ComputedStyle.TextColor);
        Assert.Equal(2, invalidations);

        view.StyleSheet = null;
        Assert.Equal(2, invalidations);
    }

    private sealed class ScopedFontLengthView : View;
    private sealed class ThemeVarView : View;
}
