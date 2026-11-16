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
        Assert.True(inherited.ComputedStyle.Width.IsAuto);
        Assert.True(inherited.ComputedStyle.Height.IsAuto);

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
        Assert.True(button.ComputedStyle.Width.IsFitContent);

        ScrollView scrollView = new();
        scrollView.InvalidateStyle();
        Assert.Equal<Overflow?>(Overflow.Auto, scrollView.ComputedStyle.OverflowY);

        Text text = new();
        text.InvalidateStyle();
        Assert.True(text.ComputedStyle.Width.IsAuto);
        Assert.Equal<Overflow?>(null, text.ComputedStyle.OverflowY);

        button.Width = 300;
        Assert.Equal(300.0, button.ComputedStyle.Width.Value);

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
        Assert.Equal(150.0, styled.ComputedStyle.Width.Value);
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
        Assert.Equal(100.0, element.ComputedStyle.Width.Value);

        element.Style.Width = 200;
        Assert.Equal<WidthValue?>(200.0, element.Width);
        Assert.Equal(200.0, element.ComputedStyle.Width.Value);

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

    internal static void VerifyVariablesResolveThroughTheCascade()
    {
        Theme previousTheme = Application.Current.RequestedTheme;
        try
        {
            Application.Current.RequestedTheme = Theme.Light;
            CssStyleSheet sheet = new(nameof(RootRuleView), string.Empty,
            [
                new CssRule(CssSelector.Root(), new Dictionary<string, CssValue>
                {
                    ["--primary"] = new CssColorValue(Colors.Blue),
                    ["--cycle-a"] = new CssVariableReference("--cycle-b"),
                    ["--cycle-b"] = new CssVariableReference("--cycle-a")
                }),
                Rule("parent", new Dictionary<string, CssValue> { ["--primary"] = new CssColorValue(Colors.Indigo) }),
                Rule("parent", new Dictionary<string, CssValue> { ["--primary"] = new CssColorValue(Colors.Red) }),
                new CssRule(CssSelector.Class("parent"), new Dictionary<string, CssValue> { ["--primary"] = new CssColorValue(Colors.White) }, media: CssMedia.Dark),
                Rule("local", new Dictionary<string, CssValue> { ["--primary"] = new CssColorValue(Colors.Green) }),
                Rule("child", new Dictionary<string, CssValue> { ["color"] = new CssVariableReference("--primary") })
            ]);
            CssStyleRegistry.Register(sheet);
            using RootRuleView root = new();
            View parent = new() { Class = "parent" };
            Text child = new() { Class = "child" };
            root.Children.Add(parent);
            parent.Children.Add(child);
            CssVariables lookup = sheet.CreateScopedVariables(child, CssColorSchemeCondition.Light);
            Assert.Equal(1, lookup.Declarations.Count);
            Assert.False(lookup.Declarations.ContainsKey("--cycle-a"), "Local environments must not copy inherited declarations.");
            Assert.Equal<Color?>(Colors.Red, lookup.ResolveColor(new ThemeValue<Color>("--primary")));
            Assert.Equal<Color?>(Colors.Red, child.ComputedStyle.TextColor);

            parent.Class = string.Empty;
            Assert.Equal<Color?>(Colors.Red, lookup.ResolveColor(new ThemeValue<Color>("--primary")), "An existing computed environment remains a snapshot.");
            lookup = sheet.CreateScopedVariables(child, CssColorSchemeCondition.Light);
            Assert.Equal<Color?>(Colors.Blue, lookup.ResolveColor(new ThemeValue<Color>("--primary")));
            Assert.Equal<Color?>(Colors.Blue, child.ComputedStyle.TextColor);
            child.Class = "child local";
            lookup = sheet.CreateScopedVariables(child, CssColorSchemeCondition.Light);
            Assert.Equal<Color?>(Colors.Green, lookup.ResolveColor(new ThemeValue<Color>("--primary")));
            Assert.Equal<Color?>(Colors.Green, child.ComputedStyle.TextColor);
            child.Class = "child";
            parent.Class = "parent";
            Application.Current.RequestedTheme = Theme.Dark;
            root.InvalidateStyle();
            Assert.Equal<Color?>(Colors.White, child.ComputedStyle.TextColor);
            Assert.Equal<Color?>(Colors.White, sheet.CreateScopedVariables(child, CssColorSchemeCondition.Dark).ResolveColor(new ThemeValue<Color>("--primary")));

            parent.Children.Remove(child);
            root.Children.Add(child);
            lookup = sheet.CreateScopedVariables(child, CssColorSchemeCondition.Light);
            Assert.Equal<Color?>(Colors.Blue, lookup.ResolveColor(new ThemeValue<Color>("--primary")));
            Assert.Equal<Color?>(Colors.Blue, child.ComputedStyle.TextColor);
            Assert.Equal<Color?>(Colors.Indigo, lookup.ResolveColor(new ThemeValue<Color>("--missing", Colors.Indigo)));
            Assert.Equal(new CssColorValue(Colors.Green), lookup.Resolve(new CssVariableReference("--cycle-a", new CssColorValue(Colors.Green))));
            Assert.Equal(3, lookup.Declarations.Count);
        }
        finally
        {
            Application.Current.RequestedTheme = previousTheme;
        }
    }

    internal static void VerifyInheritedVariablesKeepTheirComputedContext()
    {
        CssStyleSheet sheet = new(nameof(RootRuleView), string.Empty,
        [
            new CssRule(CssSelector.Root(), new Dictionary<string, CssValue>
            {
                ["--root-alias"] = new CssVariableReference("--accent"),
                ["--accent"] = new CssColorValue(Colors.Green)
            }),
            Rule("parent", new Dictionary<string, CssValue>
            {
                ["--primary"] = new CssVariableReference("--accent"),
                ["--accent"] = new CssColorValue(Colors.Red)
            }),
            Rule("child", new Dictionary<string, CssValue>
            {
                ["--accent"] = new CssColorValue(Colors.Blue),
                ["color"] = new CssVariableReference("--primary")
            }),
            Rule("alternate", new Dictionary<string, CssValue> { ["--accent"] = new CssColorValue(Colors.Indigo) })
        ]);
        CssStyleRegistry.Register(sheet);
        using RootRuleView root = new();
        View parent = new() { Class = "parent" };
        Text child = new() { Class = "child" };
        root.Children.Add(parent);
        parent.Children.Add(child);
        Assert.Equal<Color?>(Colors.Red, child.ComputedStyle.TextColor);
        CssVariables environment = sheet.CreateScopedVariables(child, CssColorSchemeCondition.Light);
        Assert.Equal(new CssColorValue(Colors.Red), environment.Resolve(new CssVariableReference("--primary")));
        Assert.Equal(new CssColorValue(Colors.Green), environment.Resolve(new CssVariableReference("--root-alias")));
        Assert.Equal(1, environment.Declarations.Count);
        parent.Class = "parent alternate";
        Assert.Equal<Color?>(Colors.Indigo, child.ComputedStyle.TextColor);
        Assert.Equal(new CssColorValue(Colors.Red), environment.Resolve(new CssVariableReference("--primary")), "Previously computed environments retain their original declarations.");
    }

    internal static void VerifyPublicVariableEnvironmentsAndCaseSensitivity()
    {
        CssVariables parent = new();
        parent.Set("accent", new CssColorValue(Colors.Red));
        parent.Set("--primary", new CssVariableReference("--accent"));
        CssVariables child = new(parent);
        child.Set("--accent", new CssColorValue(Colors.Blue));
        child.Set("--Primary", new CssColorValue(Colors.White));
        Assert.True(child.TryGetValue("--primary", out CssValue? inherited));
        Assert.Equal(new CssColorValue(Colors.Red), inherited);
        Assert.Equal<Color?>(Colors.Red, child.ResolveColor(new ThemeValue<Color>("--primary")));
        Assert.Equal<Color?>(Colors.White, child.ResolveColor(new ThemeValue<Color>("--Primary")));
        Assert.Equal<Color?>(Colors.Blue, child.ResolveColor(new ThemeValue<Color>("accent")));
        Assert.False(child.Declarations.ContainsKey("--primary"));
        int localNotifications = 0;
        int parentNotifications = 0;
        child.VariableChanged += (_, _) => localNotifications++;
        parent.VariableChanged += (_, _) => parentNotifications++;
        parent.Set("--accent", new CssColorValue(Colors.Green));
        Assert.Equal(1, parentNotifications);
        Assert.Equal(0, localNotifications, "VariableChanged reports only Set calls on that environment.");
        Assert.Equal<Color?>(Colors.Green, child.ResolveColor(new ThemeValue<Color>("--primary")));
        child.Set("--accent", new CssColorValue(Colors.Indigo));
        child.Set("--accent", new CssColorValue(Colors.Indigo));
        Assert.Equal(1, localNotifications);
    }

    internal static void VerifyRootRuleMatching()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(RootRuleView), string.Empty,
        [
            new CssRule(CssSelector.Root(), new Dictionary<string, CssValue>
            {
                ["--accent"] = new CssColorValue(Colors.Green),
                ["background-color"] = new CssVariableReference("--accent"),
                ["color"] = new CssColorValue(Colors.Blue)
            }),
            new CssRule(CssSelector.Root(), new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.White)
            }, media: CssMedia.Dark),
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

    internal static void VerifyPlatformSpecificStyling()
    {
        string originalPlatform = Application.Current.Platform;
        try
        {
            CssStyleRegistry.Register(new CssStyleSheet("PlatformStylingView", string.Empty,
            [
                new CssRule(
                    CssSelector.Descendant(
                        CssSelector.Attribute("data-platform", "android"),
                        CssSelector.Class("toolbar")),
                    new Dictionary<string, CssValue>
                    {
                        ["height"] = new CssLengthValue(56)
                    }),
                new CssRule(
                    CssSelector.Descendant(
                        CssSelector.Attribute("data-platform", "macos"),
                        CssSelector.Class("toolbar")),
                    new Dictionary<string, CssValue>
                    {
                        ["height"] = new CssLengthValue(56)
                    }),
                new CssRule(
                    CssSelector.Descendant(
                        CssSelector.Attribute("data-platform", "web"),
                        CssSelector.Class("toolbar")),
                    new Dictionary<string, CssValue>
                    {
                        ["height"] = new CssLengthValue(44)
                    })
            ]));

            using PlatformStylingView view = new();
            View toolbar = new() { Class = "toolbar" };
            view.Children.Add(toolbar);

            Application.Current.Platform = "macos";
            view.InvalidateStyle();
            Assert.Equal(56.0, toolbar.ComputedStyle.Height);

            Application.Current.Platform = "web";
            view.InvalidateStyle();
            Assert.Equal(44.0, toolbar.ComputedStyle.Height);

            Application.Current.Platform = "android";
            view.InvalidateStyle();
            Assert.Equal(56.0, toolbar.ComputedStyle.Height);

            Application.Current.Platform = "windows";
            view.InvalidateStyle();
            Assert.True(toolbar.ComputedStyle.Height.IsAuto);
        }
        finally
        {
            Application.Current.Platform = originalPlatform;
        }
    }

    private static CssRule Rule(string className, IReadOnlyDictionary<string, CssValue> declarations)
    {
        return new CssRule(CssSelector.Class(className), declarations);
    }

    private sealed class StyleResolutionView : View
    {
    }

    private sealed class RootRuleView : View;

    private sealed class PlatformStylingView : View;
}
