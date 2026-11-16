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

            // A containing block of zero height has no definite height to resolve against.
            double blockHeight = targetBlock.Height > 0 ? targetBlock.Height : double.PositiveInfinity;
            double? left = ResolveInset(childStyle.Left, targetBlock.Width);
            double? right = ResolveInset(childStyle.Right, targetBlock.Width);
            double? top = ResolveInset(childStyle.Top, blockHeight);
            double? bottom = ResolveInset(childStyle.Bottom, blockHeight);

            double childWidth;
            if (explicitWidth.HasValue)
            {
                childWidth = explicitWidth.Value;
            }
            else if (left is double leftInset && right is double rightInset)
            {
                childWidth = Math.Max(0, targetBlock.Width - leftInset - rightInset - childMargin.Horizontal);
            }
            else
            {
                double available = targetBlock.Width - (left ?? 0) - (right ?? 0) - childMargin.Horizontal;
                childWidth = Math.Min(MeasureMaxContentWidth(child), Math.Max(0, available));
            }

            childWidth = ClampWidth(childStyle, childWidth, targetBlock.Width);

            double availableHeight;
            if (childStyle.Height.IsPercent)
            {
                availableHeight = blockHeight;
            }
            else if (childStyle.Height.IsPixels)
            {
                availableHeight = childStyle.Height.Value;
            }
            else
            {
                availableHeight = top is double topInset && bottom is double bottomInset
                    ? Math.Max(0, targetBlock.Height - topInset - bottomInset - childMargin.Vertical)
                    : double.PositiveInfinity;
            }

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

        double blockHeight = containingBlock.Height > 0 ? containingBlock.Height : double.PositiveInfinity;
        double borderX;
        if (ResolveInset(style.Left, containingBlock.Width) is double left)
        {
            borderX = containingBlock.X + left + margin.Left;
        }
        else if (ResolveInset(style.Right, containingBlock.Width) is double right)
        {
            borderX = containingBlock.Right - right - margin.Right - state.RenderSize.Width;
        }
        else
        {
            borderX = containingBlock.X + margin.Left;
        }

        double borderY;
        if (ResolveInset(style.Top, blockHeight) is double top)
        {
            borderY = containingBlock.Y + top + margin.Top;
        }
        else if (ResolveInset(style.Bottom, blockHeight) is double bottom)
        {
            borderY = containingBlock.Bottom - bottom - margin.Bottom - state.RenderSize.Height;
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
