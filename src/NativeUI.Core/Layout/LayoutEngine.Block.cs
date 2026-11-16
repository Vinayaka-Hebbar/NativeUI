using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Measures the in-flow children of <paramref name="node"/> from top to bottom, each at the content width.
    /// </summary>
    private Size MeasureBlock(
        ILayoutNode node,
        double spacing,
        Point contentOrigin,
        double width,
        LayoutConstraints constraints)
    {
        double currentY = contentOrigin.Y;
        double maxWidth = 0;
        int count = 0;
        double effectiveSpacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            if (count > 0)
            {
                currentY += effectiveSpacing;
            }

            Size size = Measure(child, constraints with { AvailableSize = new Size(width, double.PositiveInfinity) });
            Thickness margin = child.ComputedStyle.Margin;
            Point borderOrigin = new(contentOrigin.X + margin.Left, currentY + margin.Top);
            GetState(child).VisualOffset = ApplyRelativeOffset(child.ComputedStyle, borderOrigin);
            currentY += size.Height + margin.Vertical;
            maxWidth = Math.Max(maxWidth, size.Width + margin.Horizontal);
            count++;
        }

        return new Size(maxWidth, Math.Max(0, currentY - contentOrigin.Y));
    }
}
