using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class TextOverflowTests
{
    private const string Label = "A long label that has to wrap";

    public static void VerifyNoWrapKeepsOneLine()
    {
        ComputedStyle wrapping = new() { FontSize = 14 };
        ComputedStyle noWrap = wrapping with { WhiteSpace = WhiteSpace.NoWrap };
        Size narrow = new(60, double.PositiveInfinity);

        Assert.True(SkiaTextShaper.Shared.CreateTextLayout(Label, narrow, wrapping).Lines.Length > 1, "Normal text wraps.");
        Assert.Equal(1, SkiaTextShaper.Shared.CreateTextLayout(Label, narrow, noWrap).Lines.Length);
    }

    public static void VerifyEllipsisShortensToWidth()
    {
        ComputedStyle style = new() { FontSize = 14, WhiteSpace = WhiteSpace.NoWrap, TextOverflow = TextOverflow.Ellipsis };
        ITextLayout layout = SkiaTextShaper.Shared.CreateTextLayout(Label, new Size(80, double.PositiveInfinity), style);

        Assert.Equal(1, layout.Lines.Length);
        Assert.True(layout.Text.EndsWith('…'), "The shortened text ends with an ellipsis.");
        Assert.True(layout.Size.Width <= 80.5, "The shortened line fits the width.");

        ITextLayout fitting = SkiaTextShaper.Shared.CreateTextLayout(Label, new Size(1000, double.PositiveInfinity), style);
        Assert.Equal(Label, fitting.Text);
    }
}
