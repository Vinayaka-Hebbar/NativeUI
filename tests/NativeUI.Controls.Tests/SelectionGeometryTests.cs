using System.Collections.Immutable;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;
using SkiaSharp;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies selection geometry against font metrics independently of the text layout's line spacing.
/// </summary>
internal static class SelectionGeometryTests
{
    internal static void Verify()
    {
        const string title = "Cross-Platform UI with Compiled XAML";
        ComputedStyle style = new() { FontSize = 26, FontWeight = FontWeight.Bold };
        SKTypeface typeface = SkiaResourceCache.Shared.GetTypeface(style.FontFamily, style.FontWeight);
        using SKFont font = new(typeface, (float)style.FontSize);
        font.GetFontMetrics(out SKFontMetrics metrics);

        foreach (double width in new double[] { 1000, 160 })
        {
            using SkiaTextLayout layout = SkiaTextShaper.Shared.ShapeText(title, new Size(width, 400), style);
            ImmutableArray<Rect> selection = layout.HitTestTextRange(0, title.Length);
            Assert.Equal(layout.Lines.Length, selection.Length);
            for (int index = 0; index < layout.Lines.Length; index++)
            {
                TextLine line = layout.Lines[index];
                Rect rect = selection[index];
                double fontTop = line.BaselineY + metrics.Ascent;
                double fontBottom = line.BaselineY + metrics.Descent;
                Assert.True(Math.Abs(rect.Y - fontTop) < 0.001, "Selection must start at the font ascent.");
                Assert.True(Math.Abs(rect.Bottom - fontBottom) < 0.001, "Selection must end at the font descent.");
                Assert.True(Math.Abs((fontTop - line.Bounds.Y) - (line.Bounds.Bottom - fontBottom)) < 0.001,
                    "Extra line spacing must be balanced around the font bounds.");

                foreach (TextClusterMetrics cluster in line.Clusters)
                {
                    TextHitTestResult hit = layout.HitTestPoint(new Point(cluster.Midpoint, line.BaselineY));
                    Assert.Equal(cluster.Range, hit.Range);
                }
            }
        }
    }
}
