using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Measures the absolutely and fixed positioned children of <paramref name="node"/> against their containing blocks.
    /// </summary>
    private void MeasureOutOfFlowChildren(ILayoutNode node, LayoutConstraints constraints)
    {
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsPositionedOutOfFlow(child))
            {
                continue;
            }

            if (_anchorLayout is null || !HasAnchorPositioning(child.ComputedStyle))
            {
                MeasureOutOfFlowChild(child, constraints);
            }
        }
    }

    private void MeasureOutOfFlowChild(ILayoutNode child, LayoutConstraints constraints)
    {
        ComputedStyle style = child.ComputedStyle;
        Size block = style.Position == Position.Fixed ? constraints.Viewport : constraints.ContainingBlock;
        double blockHeight = block.Height > 0 ? block.Height : double.PositiveInfinity;
        Thickness margin = style.Margin.Resolve(block.Width);
        double? left = ResolveInset(style.Left, block.Width);
        double? right = ResolveInset(style.Right, block.Width);
        double? top = ResolveInset(style.Top, blockHeight);
        double? bottom = ResolveInset(style.Bottom, blockHeight);
        double width = ResolvePositionedWidth(ResolveWidthLimit(style.Width, block.Width),
            (left, right), block.Width, margin.Horizontal, MeasureMaxContentWidth(child));
        double? height = ResolvePositionedHeight(ResolveHeight(style.Height, blockHeight), (top, bottom), block.Height, margin.Vertical);

        (Size minimum, Size maximum) = ResolveSizeLimits(style, new Size(block.Width, blockHeight));
        BoxConstraints box = new(width, height, minimum, maximum, margin);
        Measure(child, constraints with
        {
            AvailableSize = new Size(block.Width, blockHeight),
            ContainingBlock = block
        }, box);
    }

    private static double ResolvePositionedWidth(
        double? specifiedWidth,
        (double? Start, double? End) insets,
        double availableWidth,
        double margin,
        double contentWidth)
    {
        if (specifiedWidth is double width)
        {
            return width;
        }

        double available = Math.Max(0, availableWidth - (insets.Start ?? 0) - (insets.End ?? 0) - margin);
        return insets is (not null, not null) ? available : Math.Min(contentWidth, available);
    }

    private static double? ResolvePositionedHeight(
        double? specifiedHeight,
        (double? Start, double? End) insets,
        double availableHeight,
        double margin)
    {
        return specifiedHeight ?? (insets is (double start, double end)
            ? Math.Max(0, availableHeight - start - end - margin) : null);
    }

    /// <summary>
    /// Returns the absolute containing block of an out-of-flow <paramref name="child"/>, which is the viewport for a
    /// fixed child and <paramref name="containingBlock"/> otherwise.
    /// </summary>
    private static Rect GetContainingBlock(ILayoutNode child, Rect containingBlock, Size viewport)
    {
        return child.ComputedStyle.Position == Position.Fixed
            ? new Rect(0, 0, viewport.Width, viewport.Height)
            : containingBlock;
    }

    /// <summary>
    /// Places an out-of-flow <paramref name="child"/> in <paramref name="containingBlock"/> according to its offsets, as a
    /// position relative to <paramref name="parentOrigin"/>.
    /// </summary>
    private static void ArrangeOutOfFlowChild(ILayoutNode child, Rect containingBlock, Point parentOrigin)
    {
        ComputedStyle style = child.ComputedStyle;
        double blockHeight = containingBlock.Height > 0 ? containingBlock.Height : double.PositiveInfinity;
        ArrangeOutOfFlowChild(child, containingBlock, parentOrigin,
            (ResolveInset(style.Top, blockHeight), ResolveInset(style.Right, containingBlock.Width),
                ResolveInset(style.Bottom, blockHeight), ResolveInset(style.Left, containingBlock.Width)));
    }

    private static void ArrangeOutOfFlowChild(
        ILayoutNode child,
        Rect containingBlock,
        Point parentOrigin,
        (double? Top, double? Right, double? Bottom, double? Left) insets)
    {
        LayoutState state = GetState(child);
        Thickness margin = state.Margin;
        double x = ResolvePositionedOffset((insets.Left, insets.Right), containingBlock.Width,
            state.RenderSize.Width, (margin.Left, margin.Right));
        double y = ResolvePositionedOffset((insets.Top, insets.Bottom), containingBlock.Height,
            state.RenderSize.Height, (margin.Top, margin.Bottom));
        state.VisualOffset = new Point(containingBlock.X + x - parentOrigin.X, containingBlock.Y + y - parentOrigin.Y);
        state.Bounds = new Rect(state.VisualOffset.X, state.VisualOffset.Y, state.RenderSize.Width, state.RenderSize.Height);
    }

    private static double ResolvePositionedOffset(
        (double? Start, double? End) insets,
        double available,
        double size,
        (double Start, double End) margin)
    {
        if (insets.Start is double start)
        {
            return start + margin.Start;
        }

        return insets.End is double end ? available - end - margin.End - size : margin.Start;
    }

    /// <summary>
    /// Returns <paramref name="offset"/> moved by the <c>top</c>, <c>right</c>, <c>bottom</c> and <c>left</c> insets of a
    /// relatively positioned element, or <paramref name="offset"/> unchanged for any other element. A percentage resolves
    /// against <paramref name="containingSize"/>, the content box of the parent.
    /// </summary>
    private static Point ApplyRelativeOffset(ComputedStyle style, Point offset, Size containingSize)
    {
        if (style.Position != Position.Relative)
        {
            return offset;
        }

        double dx = ResolveInset(style.Left, containingSize.Width) ?? -ResolveInset(style.Right, containingSize.Width) ?? 0;
        double dy = ResolveInset(style.Top, containingSize.Height) ?? -ResolveInset(style.Bottom, containingSize.Height) ?? 0;
        return new Point(offset.X + dx, offset.Y + dy);
    }

    /// <summary>
    /// Returns the distance that <paramref name="inset"/> places an element from an edge: its length in pixels, or its
    /// percentage of <paramref name="containingLength"/> when that length is definite. Returns null for
    /// <see cref="InsetValue.Auto"/> and for a percentage of an indefinite length.
    /// </summary>
    private static double? ResolveInset(InsetValue inset, double containingLength)
    {
        return inset switch
        {
            { IsPixels: true } pixels => pixels.Value,
            { IsPercent: true } percent when double.IsFinite(containingLength) => containingLength * percent.Value / 100.0,
            _ => null
        };
    }

    /// <summary>
    /// Returns the vertical distance that moves a sticky box to stay within the scrollport, inset by its <c>top</c> and
    /// <c>bottom</c>, without leaving the scroll content.
    /// </summary>
    private static double GetStickyOffset(ComputedStyle style, Rect bounds, double scrollportTop, double scrollportBottom, double contentTop, double contentBottom)
    {
        double scrollportHeight = scrollportBottom - scrollportTop;
        if (ResolveInset(style.Top, scrollportHeight) is double top && bounds.Y < scrollportTop + top)
        {
            return Math.Min(scrollportTop + top - bounds.Y, contentBottom - bounds.Bottom);
        }

        if (ResolveInset(style.Bottom, scrollportHeight) is double bottom && bounds.Bottom > scrollportBottom - bottom)
        {
            return Math.Max(scrollportBottom - bottom - bounds.Bottom, contentTop - bounds.Y);
        }

        return 0;
    }
}
