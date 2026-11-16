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
    /// limited to the remaining width, then shifts them to match <see cref="IStackLayoutNode.HorizontalAlignment"/>.
    /// </summary>
    private Size MeasureHorizontalStack(
        IStackLayoutNode stack,
        Point contentOrigin,
        double width,
        LayoutConstraints constraints)
    {
        List<LayoutState> flowStates = [];
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

            if (flowStates.Count > 0)
            {
                currentX += spacing;
            }

            Thickness margin = child.ComputedStyle.Margin;
            double remaining = Math.Max(0, contentOrigin.X + width - currentX);
            double desiredWidth = MeasureMaxContentWidth(child);
            double availableWidth = Math.Min(desiredWidth + margin.Horizontal, remaining);

            Size size = Measure(child, constraints with { AvailableSize = new Size(availableWidth, constraints.AvailableSize.Height) });
            LayoutState childState = GetState(child);
            Point borderOrigin = new(currentX + margin.Left, contentOrigin.Y + margin.Top);
            childState.VisualOffset = ApplyRelativeOffset(child.ComputedStyle, borderOrigin);
            flowStates.Add(childState);
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

        if (shift > 0)
        {
            for (int i = 0; i < flowStates.Count; i++)
            {
                Point offset = flowStates[i].VisualOffset;
                flowStates[i].VisualOffset = new Point(offset.X + shift, offset.Y);
            }
        }

        return new Size(usedWidth, maxHeight);
    }
}
