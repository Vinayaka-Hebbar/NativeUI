using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Returns the logical pixels between adjacent children of <paramref name="stack"/>. A positive
    /// <see cref="IStackLayoutNode.Spacing"/> overrides the computed <c>gap</c>.
    /// </summary>
    private static double GetStackSpacing(IStackLayoutNode stack)
    {
        double spacing = stack.Spacing > 0 ? stack.Spacing : stack.ComputedStyle.Gap;
        return double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
    }

    /// <summary>
    /// Measures the in-flow children of <paramref name="stack"/> from left to right, each at its maximum content width
    /// without a limit, so that its text never wraps, then shifts them to match <see cref="IStackLayoutNode.HorizontalAlignment"/>
    /// and <see cref="ComputedStyle.AlignItems"/>.
    /// </summary>
    private Size MeasureHorizontalStack(
        IStackLayoutNode stack,
        Point contentOrigin,
        double width,
        LayoutConstraints constraints)
    {
        int count = 0;
        double currentX = contentOrigin.X;
        double maxHeight = 0;
        double spacing = GetStackSpacing(stack);
        IReadOnlyList<ILayoutNode> children = stack.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            if (count > 0)
            {
                currentX += spacing;
            }

            Size size = Measure(child, constraints with
            {
                AvailableSize = new Size(double.PositiveInfinity, constraints.AvailableSize.Height),
                IsFixedWidth = false
            });
            LayoutState childState = GetState(child);
            Thickness margin = childState.Margin;
            Point borderOrigin = new(currentX + margin.Left, contentOrigin.Y + margin.Top);
            childState.VisualOffset = ApplyRelativeOffset(child.ComputedStyle, borderOrigin, new Size(width, constraints.AvailableSize.Height));
            count++;
            currentX += size.Width + margin.Horizontal;
            maxHeight = Math.Max(maxHeight, size.Height + margin.Vertical);
        }

        double usedWidth = Math.Max(0, currentX - contentOrigin.X);
        double shift = stack.HorizontalAlignment switch
        {
            Alignment.End => Math.Max(0, width - usedWidth),
            Alignment.Center => Math.Max(0, width - usedWidth) / 2.0,
            _ => 0
        };

        Alignment alignItems = stack.ComputedStyle.AlignItems;
        double contentHeight = constraints.GetContentHeight(maxHeight);
        if (shift > 0 || (alignItems != Alignment.Start && contentHeight > 0))
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (!IsInFlow(children[i]))
                {
                    continue;
                }

                LayoutState childState = GetState(children[i]);
                double freeHeight = Math.Max(0, contentHeight - childState.RenderSize.Height - childState.Margin.Vertical);
                double shiftY = alignItems switch
                {
                    Alignment.Center => freeHeight / 2.0,
                    Alignment.End => freeHeight,
                    _ => 0
                };

                childState.VisualOffset = new Point(childState.VisualOffset.X + shift, childState.VisualOffset.Y + shiftY);
            }
        }

        return new Size(usedWidth, maxHeight);
    }
}
