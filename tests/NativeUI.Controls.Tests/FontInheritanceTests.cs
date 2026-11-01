using NativeUI.Css;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies parent font inheritance, overrides, and updates without inheriting box-model properties.
/// </summary>
internal static class FontInheritanceTests
{
    internal static void Verify()
    {
        using HelloView sample = new();
        Assert.Equal("Arial", sample.ComputedStyle.FontFamily);
        foreach (UIElement child in sample.Children)
        {
            Assert.Equal("Arial", child.ComputedStyle.FontFamily);
        }

        CssStyleRegistry.Register(new CssStyleSheet(nameof(FontInheritanceView), string.Empty,
        [
            new CssRule(CssSelector.Class("parent-font"),
                new Dictionary<string, CssValue> { ["font-family"] = new CssStringValue("Arial") }),
            new CssRule(CssSelector.Class("local-font"),
                new Dictionary<string, CssValue> { ["font-family"] = new CssStringValue("Courier New") })
        ]));

        using FontInheritanceView parent = new() { Class = "parent-font" };
        View nested = new();
        Text inherited = new() { Value = "Inherited font", Style = new Style { Color = Colors.Blue } };
        Text overridden = new() { Class = "local-font", Value = "Local font" };
        nested.Children.Add(inherited);
        parent.Children.Add(nested);
        parent.Children.Add(overridden);
        Assert.Equal("Arial", inherited.ComputedStyle.FontFamily);
        Assert.Equal("Courier New", overridden.ComputedStyle.FontFamily);

        parent.Style = new Style { FontFamily = "Times New Roman", Padding = new Thickness(20) };
        Assert.Equal("Times New Roman", inherited.ComputedStyle.FontFamily);
        Assert.Equal("Courier New", overridden.ComputedStyle.FontFamily);
        Assert.Equal(Thickness.Zero, inherited.ComputedStyle.Padding);
        inherited.Style.FontFamily = "Verdana";
        Assert.Equal("Verdana", inherited.ComputedStyle.FontFamily);
        inherited.Style.FontFamily = null;
        Assert.Equal("Times New Roman", inherited.ComputedStyle.FontFamily);

        parent.Style.FontFamily = "Arial";
        parent.RecomputeStyle();
        Assert.Equal("Arial", inherited.ComputedStyle.FontFamily);
        nested.Children.Remove(inherited);
        Assert.Equal<string?>(null, inherited.ComputedStyle.FontFamily);
        inherited.Dispose();
    }

    private sealed class FontInheritanceView : View;
}
