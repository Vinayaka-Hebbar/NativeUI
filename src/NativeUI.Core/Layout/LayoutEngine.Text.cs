using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Measures a node whose size derives from the natural size of its content, such as an image.
    /// </summary>
    private static Size MeasureIntrinsic(
        LayoutState state,
        IIntrinsicSize intrinsic,
        ComputedStyle style,
        Size availableSize,
        double borderWidth,
        double contentWidth,
        double? specifiedHeight)
    {
        Thickness insets = style.ContentInsets;
        Size natural = intrinsic.GetIntrinsicSize(new Size(availableSize.Width, specifiedHeight ?? double.PositiveInfinity));
        double height = specifiedHeight
            ?? ClampHeight(style, (natural.Width > 0 ? contentWidth * natural.Height / natural.Width : natural.Height) + insets.Vertical);

        state.Extent = Size.Zero;
        state.ReleaseTextLayout();
        return Complete(state, new Size(borderWidth, height));
    }

    /// <summary>
    /// Measures a leaf node that shapes its own text within the content width. The shaped layout stays with the node until
    /// its text or style is invalidated or the available size changes.
    /// </summary>
    private Size MeasureText(
        LayoutState state,
        ITextSource source,
        ComputedStyle style,
        double borderWidth,
        double contentWidth,
        double? specifiedHeight)
    {
        Size availableTextSize = new(contentWidth, specifiedHeight ?? double.PositiveInfinity);
        ITextLayout? layout = state.TextLayout;
        if (layout is null || !state.IsTextLayoutValid || state.TextConstraint != availableTextSize)
        {
            ITextLayout created = _textMeasurer.CreateTextLayout(source, availableTextSize, style);
            state.ReleaseTextLayout();
            state.TextLayout = created;
            state.TextConstraint = availableTextSize;
            state.IsTextLayoutValid = true;
            layout = created;
        }

        double height = specifiedHeight ?? ClampHeight(style, layout.Size.Height + style.ContentInsets.Vertical);

        state.Extent = Size.Zero;
        return Complete(state, new Size(borderWidth, height));
    }
}
