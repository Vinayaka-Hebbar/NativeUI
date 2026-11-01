using System.Diagnostics;
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
    private const double ScrollIndicatorThickness = 6;
    private const double ThinScrollIndicatorThickness = 4;
    private const double ScrollIndicatorInset = 2;
    private const double ScrollIndicatorMinimumLength = 24;
    private const double ScrollIndicatorOpacity = 0.35;

    private static readonly Color s_darkSelectionColor = Color.FromRgbBytes(96, 165, 250, 96);

    private readonly LayoutEngine _layoutEngine;
    private readonly RetainedTextMeasurer _textMeasurer;
    private readonly ITextMeasurer _overlayTextMeasurer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualTreeRenderer"/> class.
    /// </summary>
    /// <param name="textMeasurer">The <see cref="ITextMeasurer"/> used to shape and measure text runs during layout.</param>
    public VisualTreeRenderer(ITextMeasurer textMeasurer)
    {
        ArgumentNullException.ThrowIfNull(textMeasurer);
        _textMeasurer = new RetainedTextMeasurer(textMeasurer);
        _overlayTextMeasurer = textMeasurer;
        _layoutEngine = new LayoutEngine(_textMeasurer);
    }

    /// <summary>
    /// Gets the overlays drawn after the visual tree, in order.
    /// </summary>
    public IList<IRenderOverlay> Overlays { get; } = [];

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(root);

        long layoutStart = Stopwatch.GetTimestamp();
        _textMeasurer.BeginPass();
        LayoutBox rootBox;
        try
        {
            rootBox = _layoutEngine.ComputeLayout(root, logicalViewport);
            _textMeasurer.CommitPass();
        }
        catch
        {
            _textMeasurer.RollbackPass();
            throw;
        }

        TimeSpan layoutDuration = Stopwatch.GetElapsedTime(layoutStart);

        context.Save();
        try
        {
            context.Scale(scaleFactor, scaleFactor);
            context.Clear(background ?? root.ComputedStyle.BackgroundColor ?? Colors.Transparent);

            DrawBoxRecursive(context, rootBox);

            RenderPass pass = new(rootBox, logicalViewport, layoutDuration, _overlayTextMeasurer);
            foreach (IRenderOverlay overlay in Overlays)
            {
                overlay.Draw(context, pass);
            }
        }
        finally
        {
            context.Restore();
        }

        return rootBox;
    }

    /// <summary>
    /// Disposes the retained text layouts. Layout boxes returned by <see cref="Render"/> can no longer be drawn.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _textMeasurer.Dispose();
    }

    private void DrawBoxRecursive(DrawingContext context, LayoutBox box)
    {
        if (box.Element is UIElement { IsVisible: false })
        {
            return;
        }

        ComputedStyle style = box.Style;
        bool pushedLayer = style.Opacity < 1.0 && box.Children.Count > 0;
        if (pushedLayer)
        {
            context.SaveLayer(box.BorderBox, style.Opacity);
        }

        try
        {
            double drawOpacity = pushedLayer ? 1.0 : style.Opacity;

            if (style.BackgroundColor is Color bgColor && bgColor.Alpha > 0)
            {
                Paint bgPaint = new(bgColor, drawOpacity);
                if (style.BorderRadius > 0)
                {
                    context.DrawRoundedRectangle(box.BorderBox, style.BorderRadius, bgPaint);
                }
                else
                {
                    context.DrawRectangle(box.BorderBox, bgPaint);
                }
            }

            DrawBorder(context, box, drawOpacity);

            if (box.Element is Text textControl && !string.IsNullOrEmpty(textControl.Value))
            {
                ITextLayout textLayout = box.TextLayout ?? _textMeasurer.CreateTextLayout(
                    textControl.Value,
                    box.ContentBox.Size,
                    style);

                DrawSelectableText(
                    context,
                    box,
                    textControl,
                    textLayout,
                    new Paint(style.TextColor ?? Colors.Black, drawOpacity));
            }
            else if (box.Element is Button button && !string.IsNullOrEmpty(button.Text) && button.Children.Count == 0)
            {
                ITextLayout textLayout = box.TextLayout ?? _textMeasurer.CreateTextLayout(
                    button.Text,
                    box.ContentBox.Size,
                    style);

                double offsetX = box.ContentBox.X + (box.ContentBox.Width - textLayout.Size.Width) / 2.0;
                double offsetY = box.ContentBox.Y + (box.ContentBox.Height - textLayout.Size.Height) / 2.0;
                Point origin = new(Math.Max(box.ContentBox.X, offsetX), Math.Max(box.ContentBox.Y, offsetY));
                Paint textPaint = new(style.TextColor ?? Colors.Black, drawOpacity);
                context.DrawTextLayout(textLayout, origin, textPaint);
            }
            else if (box.Element is CanvasView canvasView && box.ContentBox.Width > 0 && box.ContentBox.Height > 0)
            {
                Rect localBounds = new(0, 0, box.ContentBox.Width, box.ContentBox.Height);
                context.Save();
                try
                {
                    context.Translate(box.ContentBox.X, box.ContentBox.Y);
                    context.ClipRect(localBounds);
                    canvasView.RenderCanvas(context, localBounds);
                }
                finally
                {
                    context.Restore();
                }
            }

            if (box.Children.Count > 0)
            {
                bool clipChildren = ShouldClipChildren(box);
                if (clipChildren)
                {
                    context.Save();
                    try
                    {
                        if (style.BorderRadius > 0)
                        {
                            context.ClipRoundedRectangle(box.BorderBox, style.BorderRadius);
                        }
                        else
                        {
                            context.ClipRect(box.BorderBox);
                        }

                        foreach (LayoutBox child in box.Children)
                        {
                            DrawBoxRecursive(context, child);
                        }
                    }
                    finally
                    {
                        context.Restore();
                    }
                }
                else
                {
                    foreach (LayoutBox child in box.Children)
                    {
                        DrawBoxRecursive(context, child);
                    }
                }
            }

            DrawScrollIndicators(context, box, drawOpacity);
        }
        finally
        {
            if (pushedLayer)
            {
                context.Restore();
            }
        }
    }

    /// <summary>
    /// Strokes the border inside the border box, so its outer edge follows <see cref="ComputedStyle.BorderRadius"/>.
    /// </summary>
    private static void DrawBorder(DrawingContext context, LayoutBox box, double opacity)
    {
        ComputedStyle style = box.Style;
        double width = style.BorderWidth;
        if (width <= 0)
        {
            return;
        }

        double inset = width / 2.0;
        Rect outline = new(
            box.BorderBox.X + inset,
            box.BorderBox.Y + inset,
            Math.Max(0, box.BorderBox.Width - width),
            Math.Max(0, box.BorderBox.Height - width));
        Paint paint = new(style.BorderColor ?? style.TextColor ?? Colors.Black, opacity);
        context.StrokeRoundedRectangle(outline, Math.Max(0, style.BorderRadius - inset), width, paint);
    }

    private static bool ShouldClipChildren(LayoutBox box)
    {
        return box.Style.ClipsOverflow || box.Element is View { ClipToBounds: true };
    }

    /// <summary>
    /// Draws overlay scrollbars, which take no layout space, along the right and bottom edges of a scrolled view.
    /// </summary>
    private static void DrawScrollIndicators(DrawingContext context, LayoutBox box, double opacity)
    {
        ComputedStyle style = box.Style;
        if (box.Element is not View view || !style.ClipsOverflow || style.ScrollbarWidth == ScrollbarWidth.None)
        {
            return;
        }

        double thickness = style.ScrollbarWidth == ScrollbarWidth.Thin ? ThinScrollIndicatorThickness : ScrollIndicatorThickness;
        Rect bounds = box.BorderBox;

        if (style.IsUserScrollableY && view.ExtentHeight > view.ViewportHeight)
        {
            double trackX = bounds.X + bounds.Width - thickness - (2 * ScrollIndicatorInset);
            double length = GetScrollIndicatorLength(view.ViewportHeight, view.ExtentHeight);
            double travel = view.VerticalOffset / (view.ExtentHeight - view.ViewportHeight) * (view.ViewportHeight - length);
            DrawScrollIndicator(
                context,
                style,
                new Rect(trackX, bounds.Y, thickness + (2 * ScrollIndicatorInset), bounds.Height),
                new Rect(trackX + ScrollIndicatorInset, bounds.Y + travel, thickness, length),
                opacity);
        }

        if (style.IsUserScrollableX && view.ExtentWidth > view.ViewportWidth)
        {
            double trackY = bounds.Y + bounds.Height - thickness - (2 * ScrollIndicatorInset);
            double length = GetScrollIndicatorLength(view.ViewportWidth, view.ExtentWidth);
            double travel = view.HorizontalOffset / (view.ExtentWidth - view.ViewportWidth) * (view.ViewportWidth - length);
            DrawScrollIndicator(
                context,
                style,
                new Rect(bounds.X, trackY, bounds.Width, thickness + (2 * ScrollIndicatorInset)),
                new Rect(bounds.X + travel, trackY + ScrollIndicatorInset, length, thickness),
                opacity);
        }
    }

    /// <summary>
    /// Draws one scrollbar. As in CSS, the track color applies only together with a thumb color.
    /// </summary>
    private static void DrawScrollIndicator(DrawingContext context, ComputedStyle style, Rect track, Rect thumb, double opacity)
    {
        Paint thumbPaint;
        if (style.ScrollbarThumbColor is Color thumbColor)
        {
            thumbPaint = new Paint(thumbColor, opacity);
            if (style.ScrollbarTrackColor is Color trackColor && trackColor.Alpha > 0)
            {
                context.DrawRectangle(track, new Paint(trackColor, opacity));
            }
        }
        else
        {
            Color defaultColor = Application.Current.CurrentTheme == Theme.Dark ? Colors.White : Colors.Black;
            thumbPaint = new Paint(defaultColor, opacity * ScrollIndicatorOpacity);
        }

        context.DrawRoundedRectangle(thumb, Math.Min(thumb.Width, thumb.Height) / 2, thumbPaint);
    }

    private static double GetScrollIndicatorLength(double viewport, double extent)
    {
        return Math.Min(viewport, Math.Max(ScrollIndicatorMinimumLength, viewport * viewport / extent));
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
        Paint textPaint)
    {
        Point origin = new(box.ContentBox.X, box.ContentBox.Y);
        Rect[] region = GetSelectionRegion(box, textControl, textLayout);
        if (region.Length == 0)
        {
            context.DrawTextLayout(textLayout, origin, textPaint);
            return;
        }

        SelectionStyle selectionStyle = textControl.SelectionStyle;
        Color selectionBackground = selectionStyle.BackgroundColor
            ?? (Application.Current.CurrentTheme == Theme.Dark ? s_darkSelectionColor : s_lightSelectionColor);
        Paint selBgPaint = new(selectionBackground, textPaint.Opacity);

        context.Save();
        try
        {
            context.ClipRegion(region, ClipMode.Intersect);
            context.DrawRectangle(GetBounds(region), selBgPaint);
        }
        finally
        {
            context.Restore();
        }

        if (selectionStyle.TextColor is not Color selectedTextColor || selectedTextColor == textPaint.Color)
        {
            context.DrawTextLayout(textLayout, origin, textPaint);
            return;
        }

        context.Save();
        try
        {
            context.ClipRegion(region, ClipMode.Exclude);
            context.DrawTextLayout(textLayout, origin, textPaint);
        }
        finally
        {
            context.Restore();
        }

        context.Save();
        try
        {
            context.ClipRegion(region, ClipMode.Intersect);
            context.DrawTextLayout(textLayout, origin, new Paint(selectedTextColor, textPaint.Opacity));
        }
        finally
        {
            context.Restore();
        }
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
        List<Rect> region = new(localRects.Length);
        for (int i = 0; i < localRects.Length; i++)
        {
            Rect local = localRects[i];
            Rect globalRect = new(box.ContentBox.X + local.X, box.ContentBox.Y + local.Y, local.Width, local.Height);
            Rect clipped = globalRect.Intersect(box.ContentBox);
            if (clipped.Width > 0 && clipped.Height > 0)
            {
                region.Add(clipped);
            }
        }

        return region.ToArray();
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
        private readonly List<ITextLayout> _newlyCreated = [];
        private bool _disposed;

        public void BeginPass()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _current = [];
            _newlyCreated.Clear();
        }

        public void CommitPass()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (KeyValuePair<LayoutKey, ITextLayout> entry in _retained)
            {
                if (!_current.ContainsKey(entry.Key))
                {
                    (entry.Value as IDisposable)?.Dispose();
                }
            }

            _retained = _current;
            _current = [];
            _newlyCreated.Clear();
        }

        public void RollbackPass()
        {
            if (_disposed)
            {
                return;
            }

            foreach (ITextLayout layout in _newlyCreated)
            {
                (layout as IDisposable)?.Dispose();
            }

            _newlyCreated.Clear();
            _current.Clear();
        }

        public Size MeasureText(string text, Size availableSize, ComputedStyle style)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return inner.MeasureText(text, availableSize, style);
        }

        public ITextLayout CreateTextLayout(string text, Size availableSize, ComputedStyle style)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            LayoutKey key = new(text, availableSize, style.FontSize, style.FontWeight, style.FontFamily, style.TextAlign);
            if (_current.TryGetValue(key, out ITextLayout? current))
            {
                return current;
            }

            if (!_retained.TryGetValue(key, out ITextLayout? layout))
            {
                layout = inner.CreateTextLayout(text, availableSize, style);
                _newlyCreated.Add(layout);
            }

            _current[key] = layout;
            return layout;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            HashSet<ITextLayout> toDispose = new(ReferenceEqualityComparer.Instance);
            foreach (ITextLayout layout in _retained.Values)
            {
                toDispose.Add(layout);
            }

            foreach (ITextLayout layout in _current.Values)
            {
                toDispose.Add(layout);
            }

            foreach (ITextLayout layout in _newlyCreated)
            {
                toDispose.Add(layout);
            }

            foreach (ITextLayout layout in toDispose)
            {
                (layout as IDisposable)?.Dispose();
            }

            _retained.Clear();
            _current.Clear();
            _newlyCreated.Clear();
        }

        /// <summary>
        /// Cache key identifying shaped text layout inputs.
        /// Includes all typographic parameters supported by <see cref="ComputedStyle"/> and <see cref="ITextMeasurer"/>.
        /// </summary>
        private readonly record struct LayoutKey(
            string Text,
            Size AvailableSize,
            double FontSize,
            FontWeight FontWeight,
            string? FontFamily,
            TextAlign TextAlign);
    }
}
