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

            ComputedStyle childStyle = child.ComputedStyle;
            Size targetBlock = childStyle.Position == Position.Fixed ? constraints.Viewport : constraints.ContainingBlock;
            Thickness childMargin = childStyle.Margin;
            bool isPercentWidth = childStyle.Width is { IsPercent: true };
            double? explicitWidth = childStyle.Width switch
            {
                { IsPixels: true } pixels => pixels.Value,
                { IsPercent: true } percent => Math.Max(0, targetBlock.Width * percent.Value / 100.0),
                _ => null
            };

            double childWidth;
            if (explicitWidth.HasValue)
            {
                childWidth = explicitWidth.Value;
            }
            else if (childStyle.Left.HasValue && childStyle.Right.HasValue)
            {
                childWidth = Math.Max(0, targetBlock.Width - childStyle.Left.Value - childStyle.Right.Value - childMargin.Horizontal);
            }
            else
            {
                double available = targetBlock.Width - (childStyle.Left ?? 0) - (childStyle.Right ?? 0) - childMargin.Horizontal;
                childWidth = Math.Min(MeasureMaxContentWidth(child), Math.Max(0, available));
            }

            childWidth = ClampWidth(childStyle, childWidth, targetBlock.Width);

            double availableHeight = childStyle.Height ?? (childStyle.Top.HasValue && childStyle.Bottom.HasValue
                ? Math.Max(0, targetBlock.Height - childStyle.Top.Value - childStyle.Bottom.Value - childMargin.Vertical)
                : double.PositiveInfinity);

            // A percentage resolves against the available width, so it needs the containing block and not its own result.
            Size childAvailable = new(isPercentWidth ? targetBlock.Width : childWidth + childMargin.Horizontal, availableHeight);
            Measure(child, constraints with
            {
                AvailableSize = childAvailable,
                ContainingBlock = targetBlock
            });
        }
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
        Thickness margin = style.Margin;
        LayoutState state = GetState(child);

        double borderX;
        if (style.Left.HasValue)
        {
            borderX = containingBlock.X + style.Left.Value + margin.Left;
        }
        else if (style.Right.HasValue)
        {
            borderX = containingBlock.Right - style.Right.Value - margin.Right - state.RenderSize.Width;
        }
        else
        {
            borderX = containingBlock.X + margin.Left;
        }

        double borderY;
        if (style.Top.HasValue)
        {
            borderY = containingBlock.Y + style.Top.Value + margin.Top;
        }
        else if (style.Bottom.HasValue)
        {
            borderY = containingBlock.Bottom - style.Bottom.Value - margin.Bottom - state.RenderSize.Height;
        }
        else
        {
            borderY = containingBlock.Y + margin.Top;
        }

        state.VisualOffset = new Point(borderX - parentOrigin.X, borderY - parentOrigin.Y);
        state.Bounds = new Rect(state.VisualOffset.X, state.VisualOffset.Y, state.RenderSize.Width, state.RenderSize.Height);
    }

    /// <summary>
    /// Returns <paramref name="offset"/> moved by the <c>top</c>, <c>right</c>, <c>bottom</c> and <c>left</c> insets of a
    /// relatively positioned element, or <paramref name="offset"/> unchanged for any other element.
    /// </summary>
    private static Point ApplyRelativeOffset(ComputedStyle style, Point offset)
    {
        if (style.Position != Position.Relative)
        {
            return offset;
        }

        double dx = style.Left ?? -style.Right ?? 0;
        double dy = style.Top ?? -style.Bottom ?? 0;
        return new Point(offset.X + dx, offset.Y + dy);
    }

    /// <summary>
    /// Returns the vertical distance that moves a sticky box to stay within the scrollport, inset by its <c>top</c> and
    /// <c>bottom</c>, without leaving the scroll content.
    /// </summary>
    private static double GetStickyOffset(ComputedStyle style, Rect bounds, double scrollportTop, double scrollportBottom, double contentTop, double contentBottom)
    {
        if (style.Top is double top && bounds.Y < scrollportTop + top)
        {
            return Math.Min(scrollportTop + top - bounds.Y, contentBottom - bounds.Bottom);
        }

        if (style.Bottom is double bottom && bounds.Bottom > scrollportBottom - bottom)
        {
            return Math.Max(scrollportBottom - bottom - bounds.Bottom, contentTop - bounds.Y);
        }

        return 0;
    }
}
