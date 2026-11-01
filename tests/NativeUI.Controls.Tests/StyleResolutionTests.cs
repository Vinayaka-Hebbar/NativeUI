using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies style precedence, inheritance, relative font sizing, and incremental invalidation.
/// </summary>
internal static class StyleResolutionTests
{
    internal static void VerifyInheritanceAndOverrides()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(StyleResolutionView), string.Empty,
        [
            Rule("parent", new Dictionary<string, CssValue>
            {
                ["font-family"] = new CssStringValue("Arial"),
                ["font-size"] = new CssLengthValue(24),
                ["font-weight"] = new CssFontWeightValue(FontWeight.Bold),
                ["text-align"] = new CssTextAlignValue(TextAlign.Right),
                ["color"] = new CssColorValue(Colors.Green),
                ["background-color"] = new CssColorValue(Colors.Blue),
                ["padding"] = new CssThicknessValue(new Thickness(10)),
                ["margin"] = new CssThicknessValue(new Thickness(3)),
                ["opacity"] = new CssNumberValue(0.4),
                ["width"] = new CssLengthValue(700),
                ["height"] = new CssLengthValue(200)
            }),
            Rule("explicit-defaults", new Dictionary<string, CssValue>
            {
                ["font-size"] = new CssLengthValue(16),
                ["font-weight"] = new CssFontWeightValue(FontWeight.Normal),
                ["text-align"] = new CssTextAlignValue(TextAlign.Left),
                ["color"] = new CssColorValue(Colors.White)
            }),
            Rule("relative-font", new Dictionary<string, CssValue>
            {
                ["padding"] = new CssLengthValue(1, CssLengthUnit.Em),
                ["font-size"] = new CssLengthValue(1.5, CssLengthUnit.Em)
            })
        ]));

        using StyleResolutionView parent = new() { Class = "parent" };
        View nested = new();
        Text inherited = new();
        Text overridden = new() { Class = "explicit-defaults" };
        nested.Children.Add(inherited);
        parent.Children.Add(nested);
        parent.Children.Add(overridden);

        Assert.Equal("Arial", inherited.ComputedStyle.FontFamily);
        Assert.Equal(24.0, inherited.ComputedStyle.FontSize);
        Assert.Equal(FontWeight.Bold, inherited.ComputedStyle.FontWeight);
        Assert.Equal(TextAlign.Right, inherited.ComputedStyle.TextAlign);
        Assert.Equal(Colors.Green, inherited.ComputedStyle.TextColor);
        Assert.Equal<Color?>(null, inherited.ComputedStyle.BackgroundColor);
        Assert.Equal(Thickness.Zero, inherited.ComputedStyle.Padding);
        Assert.Equal(Thickness.Zero, inherited.ComputedStyle.Margin);
        Assert.Equal(1.0, inherited.ComputedStyle.Opacity);
        Assert.Equal<WidthValue?>(null, inherited.ComputedStyle.Width);
        Assert.Equal<double?>(null, inherited.ComputedStyle.Height);

        Assert.Equal(16.0, overridden.ComputedStyle.FontSize);
        Assert.Equal(FontWeight.Normal, overridden.ComputedStyle.FontWeight);
        Assert.Equal(TextAlign.Left, overridden.ComputedStyle.TextAlign);
        Assert.Equal(Colors.White, overridden.ComputedStyle.TextColor);

        Style inline = new() { FontSize = 18, Color = Colors.Blue };
        overridden.Style = inline;
        Assert.Equal(18.0, overridden.ComputedStyle.FontSize);
        Assert.Equal(Colors.Blue, overridden.ComputedStyle.TextColor);
        inline.Color = new ThemeValue<Color>("--missing-style-token");
        Assert.Equal(Colors.White, overridden.ComputedStyle.TextColor);
        inline.Color = new ThemeValue<Color>("--missing-style-token", Colors.Indigo);
        Assert.Equal(Colors.Indigo, overridden.ComputedStyle.TextColor);
        overridden.Style = null;
        Assert.Equal(16.0, overridden.ComputedStyle.FontSize);
        Assert.Equal(Colors.White, overridden.ComputedStyle.TextColor);

        parent.Style = new Style { FontSize = 32 };
        Assert.Equal(32.0, inherited.ComputedStyle.FontSize);
        Assert.Equal(16.0, overridden.ComputedStyle.FontSize);
        Assert.Same(inherited, nested.Children[0]);

        Text relative = new() { Class = "relative-font", Style = new Style { FontSize = 10 } };
        parent.Children.Add(relative);
        Assert.Equal(10.0, relative.ComputedStyle.FontSize);
        Assert.Equal(new Thickness(10), relative.ComputedStyle.Padding);
        relative.Style = null;
        Assert.Equal(48.0, relative.ComputedStyle.FontSize);
        Assert.Equal(new Thickness(48), relative.ComputedStyle.Padding);
    }

    internal static void VerifyRelativeLengths()
    {
        CssStyleSheet sheet = new("RelativeStyles", string.Empty,
        [
            Rule("relative", new Dictionary<string, CssValue>
            {
                ["font-size"] = new CssLengthValue(2, CssLengthUnit.Em)
            }),
            Rule("relative", new Dictionary<string, CssValue>
            {
                ["padding"] = new CssLengthValue(1, CssLengthUnit.Em),
                ["font-size"] = new CssLengthValue(1.5, CssLengthUnit.Em)
            }),
            Rule("percentage", new Dictionary<string, CssValue>
            {
                ["font-size"] = new CssLengthValue(75, CssLengthUnit.Percent)
            })
        ]);

        using View parent = new() { Style = new Style { FontSize = 20 } };
        Text child = new() { Class = "relative" };
        parent.Children.Add(child);
        ComputedStyle relative = sheet.ComputeStyle(child);
        Assert.Equal(30.0, relative.FontSize);
        Assert.Equal(new Thickness(30), relative.Padding);
        child.Class = "percentage";
        Assert.Equal(15.0, sheet.ComputeStyle(child).FontSize);
    }

    internal static void VerifyInvalidation()
    {
        Style parentInline = new() { FontFamily = "Arial", FontSize = 20 };
        using View parent = new() { Style = parentInline };
        Text child = new();
        parent.Children.Add(child);
        int parentInvalidations = 0;
        int childInvalidations = 0;
        parent.VisualInvalidated += (_, _) => parentInvalidations++;
        child.VisualInvalidated += (_, _) => childInvalidations++;
        ComputedStyle originalChildStyle = child.ComputedStyle;

        parent.RecomputeStyle();
        child.Style = new Style();
        parentInline.FontSize = 20;
        Assert.Equal(0, parentInvalidations);
        Assert.Equal(0, childInvalidations);
        Assert.Same(originalChildStyle, child.ComputedStyle);

        parentInline.BackgroundColor = Colors.Blue;
        Assert.Equal(1, parentInvalidations);
        Assert.Equal(0, childInvalidations);
        Assert.Same(originalChildStyle, child.ComputedStyle);

        parentInline.FontSize = 22;
        Assert.Equal(1, childInvalidations);
        Assert.Equal(22.0, child.ComputedStyle.FontSize);

        ComputedStyle beforeThemeChange = child.ComputedStyle;
        using TestTheme testTheme = new();
        testTheme.Apply(parent, Theme.Dark);
        Assert.Equal(beforeThemeChange, child.ComputedStyle);
        Assert.Equal(2, childInvalidations);
    }

    internal static void VerifyMetadataDefaults()
    {
        Button button = new();
        button.InvalidateStyle();
        Assert.True(button.ComputedStyle.Width?.IsFitContent == true);

        ScrollView scrollView = new();
        scrollView.InvalidateStyle();
        Assert.Equal<Overflow?>(Overflow.Auto, scrollView.ComputedStyle.OverflowY);

        Text text = new();
        text.InvalidateStyle();
        Assert.Equal<WidthValue?>(null, text.ComputedStyle.Width);
        Assert.Equal<Overflow?>(null, text.ComputedStyle.OverflowY);

        button.Width = 300;
        Assert.Equal(300.0, button.ComputedStyle.Width?.Pixels);

        scrollView.Style = new Style { OverflowY = Overflow.Hidden };
        Assert.Equal<Overflow?>(Overflow.Hidden, scrollView.ComputedStyle.OverflowY);
        Assert.Equal<Overflow?>(Overflow.Auto, scrollView.ComputedStyle.OverflowX);

        View parent = new()
        {
            StyleSheet = new StyleSheet
            {
                Rules = { StyleRule.Selector(static s => s.Type<Button>()).Style(new Style { Width = 150 }) }
            }
        };
        Button styled = new();
        parent.Children.Add(styled);
        Assert.Equal(150.0, styled.ComputedStyle.Width?.Pixels);
    }

    internal static void VerifyForwardedSizeProperties()
    {
        Text element = new();
        Assert.Null(element.Style);
        Assert.Equal<WidthValue?>(null, element.Width);
        Assert.Null(element.Style);

        element.Width = 100;
        Assert.NotNull(element.Style);
        Assert.Equal<WidthValue?>(100.0, element.Style!.Width);
        Assert.Equal(100.0, element.ComputedStyle.Width?.Pixels);

        element.Style.Width = 200;
        Assert.Equal<WidthValue?>(200.0, element.Width);
        Assert.Equal(200.0, element.ComputedStyle.Width?.Pixels);

        Assert.Same(Style.WidthProperty, UIElement.WidthProperty);
        element.SetValue(UIElement.WidthProperty, 300.0);
        Assert.Equal<WidthValue?>(300.0, element.Style.Width);

        List<BindableProperty> raised = [];
        element.PropertyChanged += (_, e) => raised.Add(e.Property);
        element.Style.Height = 40;
        Assert.Equal(40.0, element.Height);
        Assert.True(raised.Contains(UIElement.HeightProperty), "A change through Style must notify element listeners.");

        raised.Clear();
        element.Style = new Style { Width = 50 };
        Assert.Equal<WidthValue?>(50.0, element.Width);
        Assert.Null(element.Height);
        Assert.True(raised.Contains(UIElement.StyleProperty), "Replacing Style must notify the style change.");

        Style oldStyle = new() { Width = 60 };
        element.Style = oldStyle;
        element.Style = new Style();
        raised.Clear();
        oldStyle.Width = 70;
        Assert.Equal(0, raised.Count);
    }

    internal static void VerifyRootRuleMatching()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(RootRuleView), string.Empty,
        [
            new CssRule(":root", null, [], true, new Dictionary<string, CssValue>
            {
                ["--accent"] = new CssColorValue(Colors.Green),
                ["background-color"] = new CssVariableReference("--accent"),
                ["color"] = new CssColorValue(Colors.Blue)
            }),
            new CssRule(":root", null, [], true, new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.White)
            }, CssColorSchemeCondition.Dark),
            Rule("uses-accent", new Dictionary<string, CssValue>
            {
                ["border-radius"] = new CssLengthValue(4),
                ["background-color"] = new CssVariableReference("--accent")
            })
        ]));

        using RootRuleView root = new();
        Text child = new() { Class = "uses-accent" };
        Text plain = new();
        root.Children.Add(child);
        root.Children.Add(plain);
        root.InvalidateStyle();

        Assert.Equal<Color?>(Colors.Green, root.ComputedStyle.BackgroundColor);
        Assert.Equal<Color?>(Colors.Blue, root.ComputedStyle.TextColor);
        Assert.Equal<Color?>(Colors.Blue, plain.ComputedStyle.TextColor);
        Assert.Equal<Color?>(null, plain.ComputedStyle.BackgroundColor);
        Assert.Equal<Color?>(Colors.Green, child.ComputedStyle.BackgroundColor);

        using TestTheme testTheme = new();
        testTheme.Apply(root, Theme.Dark);
        Assert.Equal<Color?>(Colors.White, root.ComputedStyle.TextColor);
    }

    private static CssRule Rule(string className, IReadOnlyDictionary<string, CssValue> declarations)
    {
        return new CssRule("." + className, null, [className], false, declarations);
    }

    private sealed class StyleResolutionView : View
    {
    }

    private sealed class RootRuleView : View;
}
