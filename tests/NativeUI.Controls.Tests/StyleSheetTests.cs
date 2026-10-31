using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies C# stylesheets with strongly typed selectors, without any compiled CSS.
/// </summary>
internal static class StyleSheetTests
{
    internal static void VerifyRuleMatchingAndPrecedence()
    {
        StyleSheet styleSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Type<Text>().Class("title").Class("large"))
                    .Style(new Style { Color = Colors.Green }),
                StyleRule.Selector(s => s.Class("title"))
                    .Style(new Style { Color = Colors.Blue, FontSize = 26, Opacity = 0.5 }),
                StyleRule.Selector(s => s.Class("order"))
                    .Style(new Style { Color = Colors.Blue }),
                StyleRule.Selector(s => s.Class("order"))
                    .Style(new Style { Color = Colors.White }),
                StyleRule.Selector(s => s.Type<View>())
                    .Style(new Style { FontFamily = "Arial", Padding = new Thickness(4) })
            }
        };

        using View root = new() { StyleResolver = styleSheet };
        Text title = new() { Class = "title" };
        Text large = new() { Class = "title large" };
        Text order = new() { Class = "order" };
        Text inline = new() { Class = "title", Style = new Style { Color = Colors.Black } };
        DerivedText derived = new() { Class = "title large" };
        root.Children.Add(title);
        root.Children.Add(large);
        root.Children.Add(order);
        root.Children.Add(inline);
        root.Children.Add(derived);

        Assert.Equal(Colors.Blue, title.ComputedStyle.TextColor);
        Assert.Equal(26.0, title.ComputedStyle.FontSize);
        Assert.Equal(Colors.Green, large.ComputedStyle.TextColor);
        Assert.Equal(0.5, large.ComputedStyle.Opacity);
        Assert.Equal(Colors.White, order.ComputedStyle.TextColor);
        Assert.Equal(Colors.Black, inline.ComputedStyle.TextColor);
        Assert.Equal(26.0, inline.ComputedStyle.FontSize);
        Assert.Equal(Colors.Green, derived.ComputedStyle.TextColor);

        Assert.Equal("Arial", title.ComputedStyle.FontFamily);
        Assert.Equal(Thickness.Zero, title.ComputedStyle.Padding);
        Assert.Equal(new Thickness(4), root.ComputedStyle.Padding);
    }

    internal static void VerifyHoverAndDescendants()
    {
        StyleSheet styleSheet = new()
        {
            Rules =
            {
                StyleRule.Selector(s => s.Class("title"))
                    .Style(new Style { Color = Colors.Black, FontSize = 16 }),
                StyleRule.Selector(s => s.Type<Text>().Class("title").Hover())
                    .Style(new Style { FontSize = 28 }),
                StyleRule.Selector(s => s.Class("card").Hover().Descendant(d => d.Class("title")))
                    .Style(new Style { Color = Colors.Blue })
            }
        };

        using View root = new() { Class = "card" };
        Text title = new() { Class = "title", Value = "Title", Style = new Style { Height = 40 } };
        Text other = new() { Value = "Other", Style = new Style { Height = 40 } };
        root.Children.Add(title);
        root.Children.Add(other);
        using Window window = new()
        {
            Width = 300,
            Height = 200,
            StyleResolver = styleSheet,
            Content = root
        };

        Assert.Equal(Colors.Black, title.ComputedStyle.TextColor);
        Assert.Equal(16.0, title.ComputedStyle.FontSize);

        window.HandlePointerMoved(new Point(5, 20), isPressed: false);
        Assert.Equal(Colors.Blue, title.ComputedStyle.TextColor);
        Assert.Equal(28.0, title.ComputedStyle.FontSize);

        window.HandlePointerMoved(new Point(5, 60), isPressed: false);
        Assert.Equal(Colors.Blue, title.ComputedStyle.TextColor);
        Assert.Equal(16.0, title.ComputedStyle.FontSize);

        window.HandlePointerExited();
        Assert.Equal(Colors.Black, title.ComputedStyle.TextColor);
    }

    internal static void VerifySelectorSpecificity()
    {
        Assert.Equal(new SelectorSpecificity(0, 0), Build(s => s).Specificity);
        Assert.Equal(new SelectorSpecificity(0, 1), Build(s => s.Type<Text>()).Specificity);
        Assert.Equal(new SelectorSpecificity(2, 1), Build(s => s.Type<Text>().Class("a").Hover()).Specificity);
        Assert.Equal(new SelectorSpecificity(3, 1), Build(s => s.Class("a").Hover().Descendant(d => d.Type<Text>().Class("b"))).Specificity);

        // Lexicographic comparison keeps eleven type selectors below one class, which a weighted sum would not.
        Assert.True(new SelectorSpecificity(1, 0).CompareTo(new SelectorSpecificity(0, 11)) > 0);
        Assert.True(new SelectorSpecificity(1, 2).CompareTo(new SelectorSpecificity(1, 1)) > 0);
        Assert.Equal(0, new SelectorSpecificity(1, 1).CompareTo(new SelectorSpecificity(1, 1)));

        Assert.Equal(new SelectorSpecificity(1, 0), new CssRule(":root", null, [], true, new Dictionary<string, CssValue>()).Specificity);
        Assert.Equal(new SelectorSpecificity(1, 1), new CssRule(
            ".a::selection",
            null,
            ["a"],
            false,
            new Dictionary<string, CssValue>(),
            pseudoElement: CssPseudoElement.Selection).Specificity);

        StyleSelector chained = Build(s => s.Class("a").Descendant(d => d.Class("b").Descendant(e => e.Class("c"))));
        Assert.Equal("c", chained.Subject.Classes[0]);
        Assert.Equal("a", chained.Ancestors[0].Classes[0]);
        Assert.Equal("b", chained.Ancestors[1].Classes[0]);

        static StyleSelector Build(Func<SelectorBuilder, SelectorBuilder> selector)
        {
            return StyleRule.Selector(selector).Style(new Style()).Target;
        }
    }

    internal static void VerifySelectorValidation()
    {
        CompoundSelector part = new(null, []);

        AssertThrows<ArgumentException>(() => new CompoundSelector(null, default));
        AssertThrows<ArgumentException>(() => new CompoundSelector(null, [" "]));
        AssertThrows<ArgumentNullException>(() => new StyleSelector(null!, []));
        AssertThrows<ArgumentException>(() => new StyleSelector(part, default));
        AssertThrows<ArgumentException>(() => new StyleSelector(part, [null!]));

        Assert.Equal(0, new CssRule(".a", null, default, false, new Dictionary<string, CssValue>()).Target.Ancestors.Length);
        Assert.True(new StyleSelector(part, []).Matches(new Text()));
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        bool thrown = false;
        try
        {
            action();
        }
        catch (TException)
        {
            thrown = true;
        }

        Assert.True(thrown, $"Expected {typeof(TException).Name}.");
    }

    private sealed class DerivedText : Text;
}
