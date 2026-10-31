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
public sealed class VisualTreeRenderer
{
    private static readonly Color s_lightSelectionColor = Color.FromRgbBytes(37, 99, 235, 76);
    private static readonly Color s_darkSelectionColor = Color.FromRgbBytes(96, 165, 250, 96);

    private readonly LayoutEngine _layoutEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualTreeRenderer"/> class.
    /// </summary>
    /// <param name="textMeasurer">The <see cref="ITextMeasurer"/> used to shape and measure text runs during layout.</param>
    public VisualTreeRenderer(ITextMeasurer textMeasurer)
    {
        ArgumentNullException.ThrowIfNull(textMeasurer);
        _layoutEngine = new LayoutEngine(textMeasurer);
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

        LayoutBox rootBox = _layoutEngine.ComputeLayout(root, logicalViewport);

        context.Save();
        context.Scale(scaleFactor, scaleFactor);
        context.Clear(background ?? root.ComputedStyle.BackgroundColor ?? Colors.Transparent);

        DrawBoxRecursive(context, rootBox, parentOpacity: 1.0);

        context.Restore();
        return rootBox;
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
                if (textControl.IsTextSelectionEnabled && textControl.HasSelection)
                {
                    Color selectionColor = textControl.ActualTheme == Theme.Dark
                        ? s_darkSelectionColor
                        : s_lightSelectionColor;

                    ImmutableArray<Rect> selectionRects = textLayout.HitTestTextRange(
                        textControl.SelectionStart,
                        textControl.SelectionLength);

                    foreach (Rect localRect in selectionRects)
                    {
                        Rect viewportRect = new(
                            box.ContentBox.X + localRect.X,
                            box.ContentBox.Y + localRect.Y,
                            localRect.Width,
                            localRect.Height);
                        context.DrawRectangle(viewportRect, selectionColor, effectiveOpacity);
                    }
                }

                Color textColor = style.TextColor ?? Colors.Black;
                context.DrawTextLayout(
                    textLayout,
                    new Point(box.ContentBox.X, box.ContentBox.Y),
                    textColor,
                    effectiveOpacity);
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
}
