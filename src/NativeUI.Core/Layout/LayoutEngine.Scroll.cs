using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Reports the extent of a clipping scroll container to <see cref="IScrollContainer.UpdateScrollMetrics"/>, moves its
    /// non-fixed children by the returned scroll offset, and keeps sticky children within the scrollport. Returns the
    /// scroll offset, or the default point when <paramref name="node"/> does not scroll.
    /// </summary>
    private static Point ArrangeScrollContainer(
        ILayoutNode node,
        LayoutState state,
        List<ILayoutNode> flowChildren,
        List<ILayoutNode> outOfFlowChildren)
    {
        ComputedStyle style = node.ComputedStyle;
        if (!style.ClipsOverflow || node is not IScrollContainer scrollContainer)
        {
            return default;
        }

        List<ILayoutNode> children = [.. flowChildren, .. outOfFlowChildren];
        Size size = state.DesiredSize;
        Thickness insets = style.ContentInsets;
        double maxRight = size.Width;
        double maxBottom = size.Height;
        for (int i = 0; i < children.Count; i++)
        {
            ComputedStyle childStyle = children[i].ComputedStyle;
            if (childStyle.Position != Position.Fixed)
            {
                Rect bounds = GetState(children[i]).Bounds;
                maxRight = Math.Max(maxRight, bounds.Right + childStyle.Margin.Right);
                maxBottom = Math.Max(maxBottom, bounds.Bottom + childStyle.Margin.Bottom);
            }
        }

        Size extent = new(
            Math.Max(size.Width, Math.Max(state.Extent.Width + insets.Horizontal, maxRight)),
            Math.Max(size.Height, Math.Max(state.Extent.Height + insets.Vertical, maxBottom)));
        Point offset = scrollContainer.UpdateScrollMetrics(extent, size);
        if (offset != default)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ComputedStyle.Position != Position.Fixed)
                {
                    LayoutState childState = GetState(children[i]);
                    childState.Bounds = childState.Bounds with
                    {
                        X = childState.Bounds.X - offset.X,
                        Y = childState.Bounds.Y - offset.Y
                    };
                }
            }
        }

        double scrollportTop = style.BorderWidth;
        double scrollportBottom = size.Height - style.BorderWidth;
        double contentTop = scrollportTop - offset.Y;
        double contentBottom = extent.Height - offset.Y;
        for (int i = 0; i < children.Count; i++)
        {
            ComputedStyle childStyle = children[i].ComputedStyle;
            if (childStyle.Position == Position.Sticky)
            {
                LayoutState childState = GetState(children[i]);
                double dy = GetStickyOffset(childStyle, childState.Bounds, scrollportTop, scrollportBottom, contentTop, contentBottom);
                childState.Bounds = childState.Bounds with { Y = childState.Bounds.Y + dy };
            }
        }

        return offset;
    }
}
