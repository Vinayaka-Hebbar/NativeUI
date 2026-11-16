using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Reports the extent of a clipping scroll container to <see cref="IScrollContainer.UpdateScrollMetrics"/>, moves its
    /// non-fixed children by the returned scroll offset, and keeps sticky children within the scrollport. Returns the
    /// scroll offset, or the default point when <paramref name="node"/> does not scroll.
    /// </summary>
    private static Point ArrangeScrollContainer(ILayoutNode node, LayoutState state)
    {
        ComputedStyle style = node.ComputedStyle;
        if (!style.ClipsOverflow || node is not IScrollContainer scrollContainer)
        {
            return default;
        }

        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        Size size = state.DesiredSize;
        Thickness insets = state.ContentInsets;
        double maxRight = size.Width;
        double maxBottom = size.Height;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            ComputedStyle childStyle = child.ComputedStyle;
            if (IsArranged(child) && childStyle.Position != Position.Fixed)
            {
                Rect bounds = GetState(child).Bounds;
                maxRight = Math.Max(maxRight, bounds.Right + GetState(child).Margin.Right);
                maxBottom = Math.Max(maxBottom, bounds.Bottom + GetState(child).Margin.Bottom);
            }
        }

        Size extent = new(
            Math.Max(size.Width, Math.Max(state.Extent.Width + insets.Horizontal, maxRight)),
            Math.Max(size.Height, Math.Max(state.Extent.Height + insets.Vertical, maxBottom)));
        Point offset = scrollContainer.UpdateScrollMetrics(extent, size);
        double scrollportTop = style.BorderWidth;
        double scrollportBottom = size.Height - style.BorderWidth;
        double contentTop = scrollportTop - offset.Y;
        double contentBottom = extent.Height - offset.Y;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            ComputedStyle childStyle = child.ComputedStyle;
            if (!IsArranged(child))
            {
                continue;
            }

            LayoutState childState = GetState(child);
            if (childStyle.Position != Position.Fixed && offset != default)
            {
                childState.Bounds = childState.Bounds with
                {
                    X = childState.Bounds.X - offset.X,
                    Y = childState.Bounds.Y - offset.Y
                };
            }

            if (childStyle.Position == Position.Sticky)
            {
                double dy = GetStickyOffset(childStyle, childState.Bounds, scrollportTop, scrollportBottom, contentTop, contentBottom);
                childState.Bounds = childState.Bounds with { Y = childState.Bounds.Y + dy };
            }
        }

        return offset;
    }
}
