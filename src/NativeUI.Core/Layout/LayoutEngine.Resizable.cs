using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    private const double DefaultResizableHandleSize = 6.0;

    /// <summary>
    /// Measures panels in proportion to their shares of the main length that the handles leave free. Each child is
    /// allocated the length of its share even when its content resolves to a different size.
    /// </summary>
    private Size MeasureResizable(
        IResizableLayoutNode resizable,
        Point contentOrigin,
        double width,
        double? definiteHeight,
        LayoutConstraints constraints)
    {
        IReadOnlyList<ILayoutNode> children = resizable.LayoutChildren;
        if (children.Count == 0)
        {
            return new Size(width, definiteHeight ?? 0);
        }

        bool isHorizontal = resizable.Orientation == Orientation.Horizontal;
        double totalMainLength = isHorizontal ? width : (definiteHeight ?? constraints.Viewport.Height);
        double crossLength = isHorizontal ? (definiteHeight ?? constraints.Viewport.Height) : width;

        double handlesTotalLength = 0;
        double totalPanelShares = 0;
        int uncollapsedPanelCount = 0;

        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            if (child is IResizablePanelLayoutNode panel)
            {
                if (!panel.IsCollapsed)
                {
                    totalPanelShares += Math.Max(0, panel.EffectiveSize);
                    uncollapsedPanelCount++;
                }
            }
            else
            {
                handlesTotalLength += GetHandleLength(child.ComputedStyle, isHorizontal);
            }
        }

        if (totalPanelShares <= 0)
        {
            totalPanelShares = uncollapsedPanelCount > 0 ? uncollapsedPanelCount : 1.0;
        }

        double availablePanelLength = Math.Max(0, totalMainLength - handlesTotalLength);
        double currentMainPosition = isHorizontal ? contentOrigin.X : contentOrigin.Y;

        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            ComputedStyle childStyle = child.ComputedStyle;
            double childMainLength;
            if (child is IResizablePanelLayoutNode panel)
            {
                childMainLength = panel.IsCollapsed ? 0 : Math.Max(0, panel.EffectiveSize) / totalPanelShares * availablePanelLength;
            }
            else
            {
                childMainLength = GetHandleLength(childStyle, isHorizontal);
            }

            double childWidth = isHorizontal ? childMainLength : width;
            double childHeight = isHorizontal ? (ResolveHeight(childStyle.Height, crossLength) ?? crossLength) : childMainLength;
            if (!isHorizontal && childStyle.Width is { IsPixels: true } explicitWidth)
            {
                childWidth = explicitWidth.Value;
            }

            Point origin = isHorizontal ? new Point(currentMainPosition, contentOrigin.Y) : new Point(contentOrigin.X, currentMainPosition);
            Size measured = Measure(child, constraints with { AvailableSize = new Size(childWidth, childHeight) });
            LayoutState childState = GetState(child);

            Point borderOrigin;
            if (measured.Width != childWidth || measured.Height != childHeight)
            {
                childState.RenderSize = new Size(childWidth, childHeight);
                borderOrigin = origin;
            }
            else
            {
                childState.RenderSize = childState.DesiredSize;
                borderOrigin = new Point(origin.X + childStyle.Margin.Left, origin.Y + childStyle.Margin.Top);
            }

            childState.VisualOffset = ApplyRelativeOffset(childStyle, borderOrigin, new Size(width, definiteHeight ?? double.PositiveInfinity));
            currentMainPosition += childMainLength;
        }

        return isHorizontal
            ? new Size(totalMainLength, crossLength)
            : new Size(width, totalMainLength);
    }

    private static double GetHandleLength(ComputedStyle style, bool isHorizontal)
    {
        return isHorizontal
            ? (style.Width is { IsPixels: true } width ? width.Value : DefaultResizableHandleSize)
            : (style.Height is { IsPixels: true } height ? height.Value : DefaultResizableHandleSize);
    }
}
