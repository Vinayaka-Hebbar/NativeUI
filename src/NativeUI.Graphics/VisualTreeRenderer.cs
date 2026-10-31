using System.Collections.Immutable;
using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Graphics;

/// <summary>
/// Traverses a <see cref="UIElement"/> hierarchy, computes its <see cref="LayoutBox"/> geometry via <see cref="LayoutEngine"/>,
/// and issues drawing commands (including text selection highlights and shaped text layouts) to a <see cref="DrawingContext"/>.
/// </summary>
public sealed class VisualTreeRenderer : IDisposable
{
    private static readonly Color s_lightSelectionColor = Color.FromRgbBytes(37, 99, 235, 76);
    private static readonly Color s_darkSelectionColor = Color.FromRgbBytes(96, 165, 250, 96);

    private readonly LayoutEngine _layoutEngine;
    private readonly RetainedTextMeasurer _textMeasurer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualTreeRenderer"/> class.
    /// </summary>
    /// <param name="textMeasurer">The <see cref="ITextMeasurer"/> used to shape and measure text runs during layout.</param>
    public VisualTreeRenderer(ITextMeasurer textMeasurer)
    {
        ArgumentNullException.ThrowIfNull(textMeasurer);
        _textMeasurer = new RetainedTextMeasurer(textMeasurer);
        _layoutEngine = new LayoutEngine(_textMeasurer);
    }

    /// <summary>
    /// Computes layout for <paramref name="root"/> within <paramref name="logicalViewport"/> and draws the visual tree
    /// into <paramref name="context"/> at the specified DPI <paramref name="scaleFactor"/>.
    /// </summary>
    public LayoutBox Render(
        DrawingContext context,
        UIElement root,
        Size logicalViewport,
        float scaleFactor = 1.0f,
        Color? background = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(root);

        _textMeasurer.BeginPass();
        LayoutBox rootBox;
        try
        {
            rootBox = _layoutEngine.ComputeLayout(root, logicalViewport);
        }
        finally
        {
            _textMeasurer.EndPass();
        }

        context.Save();
        context.Scale(scaleFactor, scaleFactor);
        context.Clear(background ?? root.ComputedStyle.BackgroundColor ?? Colors.Transparent);

        DrawBoxRecursive(context, rootBox, parentOpacity: 1.0);

        context.Restore();
        return rootBox;
    }

    /// <summary>
    /// Disposes the retained text layouts. Layout boxes returned by <see cref="Render"/> can no longer be drawn.
    /// </summary>
    public void Dispose()
    {
        _textMeasurer.Dispose();
    }

    private static void DrawBoxRecursive(DrawingContext context, LayoutBox box, double parentOpacity)
    {
        if (box.Element is UIElement { IsVisible: false })
        {
            return;
        }

        ComputedStyle style = box.Style;
        double effectiveOpacity = Math.Clamp(parentOpacity * style.Opacity, 0.0, 1.0);

        if (style.BackgroundColor is Color bgColor && bgColor.Alpha > 0)
        {
            if (style.BorderRadius > 0)
            {
                context.DrawRoundedRectangle(box.BorderBox, style.BorderRadius, bgColor, effectiveOpacity);
            }
            else
            {
                context.DrawRectangle(box.BorderBox, bgColor, effectiveOpacity);
            }
        }

        if (box.Element is Text textControl && !string.IsNullOrEmpty(textControl.Value))
        {
            if (box.TextLayout is ITextLayout textLayout)
            {
                DrawSelectableText(context, box, textControl, textLayout, style.TextColor ?? Colors.Black, effectiveOpacity);
            }
            else
            {
                Color textColor = style.TextColor ?? Colors.Black;
                context.DrawText(
                    textControl.Value,
                    box.ContentBox,
                    textColor,
                    style.FontSize,
                    style.FontWeight,
                    style.FontFamily,
                    style.TextAlign,
                    effectiveOpacity);
            }
        }

        foreach (LayoutBox child in box.Children)
        {
            DrawBoxRecursive(context, child, effectiveOpacity);
        }
    }

    /// <summary>
    /// Paints the selection background and shaped glyphs. The same layout is drawn in both colors under complementary
    /// clips, so selection never reshapes text or moves glyphs.
    /// </summary>
    private static void DrawSelectableText(
        DrawingContext context,
        LayoutBox box,
        Text textControl,
        ITextLayout textLayout,
        Color textColor,
        double opacity)
    {
        Point origin = new(box.ContentBox.X, box.ContentBox.Y);
        Rect[] region = GetSelectionRegion(box, textControl, textLayout);
        if (region.Length == 0)
        {
            context.DrawTextLayout(textLayout, origin, textColor, opacity);
            return;
        }

        SelectionStyle selectionStyle = textControl.SelectionStyle;
        Color selectionBackground = selectionStyle.BackgroundColor
            ?? (textControl.ActualTheme == Theme.Dark ? s_darkSelectionColor : s_lightSelectionColor);

        context.Save();
        context.ClipRegion(region, ClipMode.Intersect);
        context.DrawRectangle(GetBounds(region), selectionBackground, opacity);
        context.Restore();

        if (selectionStyle.TextColor is not Color selectedTextColor || selectedTextColor == textColor)
        {
            context.DrawTextLayout(textLayout, origin, textColor, opacity);
            return;
        }

        context.Save();
        context.ClipRegion(region, ClipMode.Exclude);
        context.DrawTextLayout(textLayout, origin, textColor, opacity);
        context.Restore();

        context.Save();
        context.ClipRegion(region, ClipMode.Intersect);
        context.DrawTextLayout(textLayout, origin, selectedTextColor, opacity);
        context.Restore();
    }

    private static Rect[] GetSelectionRegion(LayoutBox box, Text textControl, ITextLayout textLayout)
    {
        if (!textControl.IsTextSelectionEnabled || !textControl.HasSelection)
        {
            return [];
        }

        ImmutableArray<Rect> localRects = textLayout.HitTestTextRange(
            textControl.SelectionStart,
            textControl.SelectionLength);
        Rect[] region = new Rect[localRects.Length];
        for (int i = 0; i < region.Length; i++)
        {
            Rect local = localRects[i];
            region[i] = new Rect(box.ContentBox.X + local.X, box.ContentBox.Y + local.Y, local.Width, local.Height);
        }

        return region;
    }

    private static Rect GetBounds(Rect[] region)
    {
        double left = region.Min(static rect => rect.X);
        double top = region.Min(static rect => rect.Y);
        double right = region.Max(static rect => rect.Right);
        double bottom = region.Max(static rect => rect.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Reuses shaped layouts between layout passes while text, typography, and available size are unchanged,
    /// and disposes layouts that the latest pass no longer uses. Color changes never affect the cache key.
    /// </summary>
    private sealed class RetainedTextMeasurer(ITextMeasurer inner) : ITextMeasurer, IDisposable
    {
        private Dictionary<LayoutKey, ITextLayout> _retained = [];
        private Dictionary<LayoutKey, ITextLayout> _current = [];

        public void BeginPass()
        {
            _current = [];
        }

        public void EndPass()
        {
            foreach (KeyValuePair<LayoutKey, ITextLayout> entry in _retained)
            {
                if (!_current.ContainsKey(entry.Key))
                {
                    (entry.Value as IDisposable)?.Dispose();
                }
            }

            _retained = _current;
        }

        public Size MeasureText(string text, Size availableSize, ComputedStyle style)
        {
            return inner.MeasureText(text, availableSize, style);
        }

        public ITextLayout CreateTextLayout(string text, Size availableSize, ComputedStyle style)
        {
            LayoutKey key = new(text, availableSize, style.FontSize, style.FontWeight, style.FontFamily, style.TextAlign);
            if (_current.TryGetValue(key, out ITextLayout? current))
            {
                return current;
            }

            if (!_retained.TryGetValue(key, out ITextLayout? layout))
            {
                layout = inner.CreateTextLayout(text, availableSize, style);
            }

            _current[key] = layout;
            return layout;
        }

        public void Dispose()
        {
            foreach (ITextLayout layout in _retained.Values)
            {
                (layout as IDisposable)?.Dispose();
            }

            _retained = [];
            _current = [];
        }

        private readonly record struct LayoutKey(
            string Text,
            Size AvailableSize,
            double FontSize,
            FontWeight FontWeight,
            string? FontFamily,
            TextAlign TextAlign);
    }
}
