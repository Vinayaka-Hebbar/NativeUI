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
    private readonly DrawingResourceCache _resources = new();
    private readonly HashSet<Image> _images = [];

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
    /// Gets the layout boxes of the open portals drawn in the last pass, in drawing order. A portal that
    /// <see cref="IWindowHostedElement.HasOwnWindow"/> is not among them.
    /// </summary>
    public IReadOnlyList<LayoutBox> PortalBoxes { get; private set; } = [];

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

        HashSet<Image> currentImages = [];
        AttachImages(root, context.ImageDecoder, currentImages);
        foreach (Image previous in _images.Where(image => !currentImages.Contains(image))) previous.Detach();
        _images.Clear();
        _images.UnionWith(currentImages);
        context.UseResources(_resources);
        _resources.BeginPass();
        long layoutStart = Stopwatch.GetTimestamp();
        _textMeasurer.BeginPass();
        LayoutBox rootBox;
        try
        {
            rootBox = _layoutEngine.ComputeLayout(root, logicalViewport);
            PortalBoxes = LayoutPortals(root, rootBox, logicalViewport);
            _textMeasurer.CommitPass();
        }
        catch
        {
            _textMeasurer.RollbackPass();
            throw;
        }

        NotifySizeChanged(rootBox);
        foreach (LayoutBox portalBox in PortalBoxes)
        {
            NotifySizeChanged(portalBox);
        }

        TimeSpan layoutDuration = Stopwatch.GetElapsedTime(layoutStart);

        context.Save();
        _currentRoot = root;
        _pixelScale = scaleFactor > 0 ? scaleFactor : 1;
        try
        {
            context.Scale(scaleFactor, scaleFactor);
            context.Clear(background ?? root.ComputedStyle.BackgroundColor ?? Colors.Transparent);

            DrawBoxRecursive(context, rootBox);
            foreach (LayoutBox portalBox in PortalBoxes)
            {
                DrawBoxRecursive(context, portalBox);
            }

            RenderPass pass = new(rootBox, logicalViewport, layoutDuration, _overlayTextMeasurer);
            foreach (IRenderOverlay overlay in Overlays)
            {
                overlay.Draw(context, pass);
            }
        }
        finally
        {
            context.Restore();
            _resources.EndPass();
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
        foreach (Image image in _images) image.Detach();
        _images.Clear();
        _resources.Dispose();
    }

    /// <summary>
    /// Lays out each open portal at the origin to learn its size, then moves it to its position against its anchor.
    /// </summary>
    private List<LayoutBox> LayoutPortals(UIElement root, LayoutBox rootBox, Size viewport)
    {
        List<Portal> portals = [];
        CollectOpenPortals(root, portals);

        List<LayoutBox> boxes = [];
        Rect area = new(0, 0, viewport.Width, viewport.Height);
        foreach (Portal portal in portals)
        {
            LayoutBox box = _layoutEngine.ComputeLayout(portal, viewport);
            Rect? anchor = portal.Anchor is UIElement element ? FindAnchorBounds(element, rootBox, boxes) : null;
            Point position = PortalPlacement.Compute(portal, anchor, box.BorderBox.Size, area, area);
            boxes.Add(box.Translated(position.X - box.BorderBox.X, position.Y - box.BorderBox.Y));
        }

        return boxes;
    }

    private static void CollectOpenPortals(UIElement element, List<Portal> portals)
    {
        if (element is not View { IsVisible: true } view)
        {
            return;
        }

        foreach (UIElement child in view.Children)
        {
            if (child is Portal portal)
            {
                if (portal.IsOpen && portal is not IWindowHostedElement { HasOwnWindow: true })
                {
                    portals.Add(portal);
                    CollectOpenPortals(portal, portals);
                }
            }
            else
            {
                CollectOpenPortals(child, portals);
            }
        }
    }

    private static Rect? FindAnchorBounds(UIElement anchor, LayoutBox rootBox, List<LayoutBox> portalBoxes)
    {
        LayoutBox? found = FindBox(rootBox, anchor);
        foreach (LayoutBox portalBox in portalBoxes)
        {
            found ??= FindBox(portalBox, anchor);
        }

        return found?.BorderBox;
    }

    private static LayoutBox? FindBox(LayoutBox box, UIElement target)
    {
        if (ReferenceEquals(box.Element, target))
        {
            return box;
        }

        foreach (LayoutBox child in box.Children)
        {
            LayoutBox? found = FindBox(child, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private UIElement? _currentRoot;
    // TODO: Use the actual pixel scale of the drawing context instead of a fixed value. This is needed for snapping to device pixels.
    private double _pixelScale = 1;

    private void DrawBoxRecursive(DrawingContext context, LayoutBox box)
    {
        if (box.Element is UIElement { IsVisible: false })
        {
            return;
        }

        if (box.Element is IWindowHostedElement { HasOwnWindow: true } && !ReferenceEquals(box.Element, _currentRoot))
        {
            return;
        }

        (box.Element as UIElement)?.MarkRendered();

        if (box.Style.Transform is not { IsIdentity: false } transform)
        {
            DrawBoxContents(context, box);
            return;
        }

        Rect bounds = box.BorderBox;
        Point translation = transform.GetTranslation(bounds.Size);
        double centerX = bounds.X + (bounds.Width / 2.0);
        double centerY = bounds.Y + (bounds.Height / 2.0);
        context.Save();
        try
        {
            context.Translate(centerX + translation.X, centerY + translation.Y);
            context.Scale((float)transform.ScaleX, (float)transform.ScaleY);
            context.Translate(-centerX, -centerY);
            DrawBoxContents(context, box);
        }
        finally
        {
            context.Restore();
        }
    }

    private void DrawBoxContents(DrawingContext context, LayoutBox box)
    {
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
                Rect background = SnapToPixels(box.BorderBox);
                if (style.BorderRadius > 0)
                {
                    context.DrawRoundedRectangle(background, style.BorderRadius, bgPaint);
                }
                else
                {
                    context.DrawRectangle(background, bgPaint);
                }
            }

            // An element that clips its children draws its border after them. The clip edge and the border's inner edge
            // share an anti-aliased pixel, which the border then covers.
            bool drawsBorderLast = box.Children.Count > 0 && ShouldClipChildren(box);
            if (!drawsBorderLast)
            {
                DrawBorder(context, box, drawOpacity);
            }

            if (box.Element is Image image)
            {
                DrawImageControl(context, image, box.ContentBox, drawOpacity);
            }
            else if (box.Element is Controls.Shapes.Shape path)
            {
                DrawPathControl(context, path, box.ContentBox, box.Style, drawOpacity);
            }
            else if (box.Element is Svg svg)
            {
                DrawSvgControl(context, svg, box.ContentBox, svg.Stretch, drawOpacity);
            }
            else if (box.Element is Text textControl && !string.IsNullOrEmpty(textControl.Value))
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
                        ClipToPaddingBox(context, box);

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

            if (drawsBorderLast)
            {
                DrawBorder(context, box, drawOpacity);
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
    /// Clips to the padding box, the area inside the border, as CSS clips overflow. The corner radius of the clip is
    /// <see cref="ComputedStyle.BorderRadius"/> reduced by the border width, which follows the inner edge of the border.
    /// The clip edges lie on device pixels, as do the backgrounds and border edges they meet.
    /// </summary>
    private void ClipToPaddingBox(DrawingContext context, LayoutBox box)
    {
        ComputedStyle style = box.Style;
        Rect paddingBox = GetPaddingBox(box);
        double radius = Math.Max(0, style.BorderRadius - Math.Max(0, style.BorderWidth));
        if (radius > 0)
        {
            context.ClipRoundedRectangle(paddingBox, radius);
        }
        else
        {
            context.ClipRect(paddingBox);
        }
    }

    /// <summary>
    /// Strokes the border between the border box and the padding box, so its outer edge follows
    /// <see cref="ComputedStyle.BorderRadius"/>. Both edges lie on device pixels.
    /// </summary>
    private void DrawBorder(DrawingContext context, LayoutBox box, double opacity)
    {
        ComputedStyle style = box.Style;
        if (style.BorderWidth <= 0)
        {
            return;
        }

        Rect outer = SnapToPixels(box.BorderBox);
        Rect inner = GetPaddingBox(box);
        double width = Math.Max(1 / _pixelScale, inner.Y - outer.Y);
        double inset = width / 2.0;
        Rect outline = new(
            outer.X + inset,
            outer.Y + inset,
            Math.Max(0, outer.Width - width),
            Math.Max(0, outer.Height - width));
        Paint paint = new(style.BorderColor ?? style.TextColor ?? Colors.Black, opacity);
        context.StrokeRoundedRectangle(outline, Math.Max(0, style.BorderRadius - inset), width, paint);
    }

    /// <summary>
    /// Returns the padding box of <paramref name="box"/> with its edges on device pixels.
    /// </summary>
    private Rect GetPaddingBox(LayoutBox box)
    {
        double width = Math.Max(0, box.Style.BorderWidth);
        return SnapToPixels(new Rect(
            box.BorderBox.X + width,
            box.BorderBox.Y + width,
            Math.Max(0, box.BorderBox.Width - (width * 2)),
            Math.Max(0, box.BorderBox.Height - (width * 2))));
    }

    /// <summary>
    /// Rounds each edge of <paramref name="rect"/> to the nearest device pixel. Two boxes that share an edge in layout then
    /// share it on screen, without an anti-aliased pixel that shows what lies beneath both.
    /// </summary>
    private Rect SnapToPixels(Rect rect)
    {
        double left = Math.Round(rect.X * _pixelScale) / _pixelScale;
        double top = Math.Round(rect.Y * _pixelScale) / _pixelScale;
        double right = Math.Round(rect.Right * _pixelScale) / _pixelScale;
        double bottom = Math.Round(rect.Bottom * _pixelScale) / _pixelScale;
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static void AttachImages(UIElement element, IImageDecoder? decoder, HashSet<Image> images)
    {
        if (!element.IsVisible)
        {
            return;
        }

        if (element is Image image && decoder is not null)
        {
            images.Add(image);
            if (ReferenceEquals(image.Dispatcher, ImmediateDispatcher.Instance)) image.Dispatcher = Application.Current.Dispatcher;
            image.Attach(decoder);
        }
        if (element is View view)
        {
            foreach (UIElement child in view.Children) AttachImages(child, decoder, images);
        }
    }

    private static void DrawImageControl(DrawingContext context, Image image, Rect bounds, double opacity)
    {
        context.Save();
        try
        {
            context.ClipRect(bounds);
            if (image.VectorImage is Svg svg) DrawSvgControl(context, svg, bounds, image.Stretch, opacity);
            else if (image.DecodedImage is IDecodedImage decoded)
            {
                Rect target = StretchLayout.Fit(decoded.Size, bounds, image.Stretch);
                if (target.Width > 0 && target.Height > 0) context.DrawImage(decoded, target, opacity);
            }
        }
        finally { context.Restore(); }
    }

    private static void DrawPathControl(DrawingContext context, Controls.Shapes.Shape path, Rect bounds, ComputedStyle style, double opacity)
    {
        PathGeometry geometry = path.GetGeometry(bounds.Size);
        Rect source = path.GetGeometryBounds(bounds.Size);
        Rect target = StretchLayout.Fit(source.Size, bounds, path.Stretch);
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0) return;
        context.Save();
        try
        {
            context.ClipRect(bounds);
            context.Translate(target.X, target.Y);
            context.Scale((float)(target.Width / source.Width), (float)(target.Height / source.Height));
            context.Translate(-source.X, -source.Y);
            context.DrawGeometry(geometry, ResolveCurrentColor(path.Fill, style), ResolveCurrentColor(path.Stroke, style), path.StrokeThickness, opacity);
        }
        finally { context.Restore(); }
    }

    /// <summary>
    /// Resolves <see cref="SvgCurrentColorBrush"/> to the element's computed text color, as <c>currentColor</c> does in the browser.
    /// </summary>
    private static Brush? ResolveCurrentColor(Brush? brush, ComputedStyle style)
    {
        return brush is SvgCurrentColorBrush ? new SolidColorBrush(style.TextColor ?? Colors.Black) : brush;
    }

    private static void DrawSvgControl(DrawingContext context, Svg svg, Rect bounds, Stretch stretch, double opacity)
    {
        Rect source = svg.GetViewBox(bounds.Size);
        Rect target = StretchLayout.Fit(source.Size, bounds, stretch);
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0) return;
        context.Save();
        try
        {
            context.ClipRect(bounds);
            context.Translate(target.X, target.Y);
            context.Scale((float)(target.Width / source.Width), (float)(target.Height / source.Height));
            context.Translate(-source.X, -source.Y);
            if (svg.Definition is SvgNode definition) context.DrawSvg(definition, source, opacity);
        }
        finally { context.Restore(); }
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
        foreach (ScrollbarGeometry scrollbar in GetScrollbars(box))
        {
            DrawScrollIndicator(context, box.Style, scrollbar.Track, scrollbar.Thumb, opacity);
        }
    }

    /// <summary>
    /// Returns the overlay scrollbars of <paramref name="box"/>: one along the right edge while its content scrolls
    /// vertically, and one along the bottom edge while it scrolls horizontally.
    /// </summary>
    /// <param name="box">The laid-out box of a view.</param>
    public static IReadOnlyList<ScrollbarGeometry> GetScrollbars(LayoutBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        ComputedStyle style = box.Style;
        if (box.Element is not View view || !style.ClipsOverflow || style.ScrollbarWidth == ScrollbarWidth.None)
        {
            return [];
        }

        List<ScrollbarGeometry> scrollbars = [];
        double thickness = style.ScrollbarWidth == ScrollbarWidth.Thin ? ThinScrollIndicatorThickness : ScrollIndicatorThickness;
        Rect bounds = box.BorderBox;

        if (style.IsUserScrollableY && view.ExtentHeight > view.ViewportHeight)
        {
            double trackX = bounds.X + bounds.Width - thickness - (2 * ScrollIndicatorInset);
            double length = GetScrollIndicatorLength(view.ViewportHeight, view.ExtentHeight);
            double travel = view.VerticalOffset / (view.ExtentHeight - view.ViewportHeight) * (view.ViewportHeight - length);
            scrollbars.Add(new ScrollbarGeometry(
                view,
                Orientation.Vertical,
                new Rect(trackX, bounds.Y, thickness + (2 * ScrollIndicatorInset), bounds.Height),
                new Rect(trackX + ScrollIndicatorInset, bounds.Y + travel, thickness, length)));
        }

        if (style.IsUserScrollableX && view.ExtentWidth > view.ViewportWidth)
        {
            double trackY = bounds.Y + bounds.Height - thickness - (2 * ScrollIndicatorInset);
            double length = GetScrollIndicatorLength(view.ViewportWidth, view.ExtentWidth);
            double travel = view.HorizontalOffset / (view.ExtentWidth - view.ViewportWidth) * (view.ViewportWidth - length);
            scrollbars.Add(new ScrollbarGeometry(
                view,
                Orientation.Horizontal,
                new Rect(bounds.X, trackY, bounds.Width, thickness + (2 * ScrollIndicatorInset)),
                new Rect(bounds.X + travel, trackY + ScrollIndicatorInset, length, thickness)));
        }

        return scrollbars;
    }

    /// <summary>
    /// Returns the topmost scrollbar whose track contains <paramref name="point"/>, or <see langword="null"/> when there is
    /// none. A view's scrollbars lie above its children, and later siblings lie above earlier ones, as they are drawn.
    /// </summary>
    /// <param name="box">The laid-out box to search, with its descendants.</param>
    /// <param name="point">The point in the coordinates of the layout.</param>
    public static ScrollbarGeometry? HitTestScrollbar(LayoutBox box, Point point)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (box.Element is UIElement { IsVisible: false })
        {
            return null;
        }

        IReadOnlyList<ScrollbarGeometry> scrollbars = GetScrollbars(box);
        for (int i = scrollbars.Count - 1; i >= 0; i--)
        {
            if (scrollbars[i].Track.Contains(point))
            {
                return scrollbars[i];
            }
        }

        if (ShouldClipChildren(box) && !box.BorderBox.Contains(point))
        {
            return null;
        }

        for (int i = box.Children.Count - 1; i >= 0; i--)
        {
            if (HitTestScrollbar(box.Children[i], point) is ScrollbarGeometry hit)
            {
                return hit;
            }
        }

        return null;
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
    /// Traverses the computed layout box tree and raises <see cref="UIElement.SizeChanged"/> on elements whose rendered dimensions changed.
    /// </summary>
    /// <param name="box">The root layout box to traverse.</param>
    public static void NotifyLayoutSizeChanged(LayoutBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        NotifySizeChanged(box);
    }

    private static void NotifySizeChanged(LayoutBox box)
    {
        if (box.Element is UIElement element)
        {
            element.UpdateRenderedBounds(box.BorderBox);
        }

        foreach (LayoutBox child in box.Children)
        {
            NotifySizeChanged(child);
        }
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
