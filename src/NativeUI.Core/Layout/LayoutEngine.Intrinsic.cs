using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Returns the border-box width of <paramref name="node"/> when none of its text wraps.
    /// </summary>
    private double MeasureMaxContentWidth(ILayoutNode node)
    {
        if (!node.IsVisible)
        {
            return 0;
        }

        LayoutState state = GetState(node);
        if (state.MaxContentWidth is double retained)
        {
            return retained;
        }

        ComputedStyle style = node.ComputedStyle;
        double width;
        if (style.Width is { IsPixels: true } fixedWidth)
        {
            width = fixedWidth.Value;
        }
        else if (node is IIntrinsicSize intrinsic)
        {
            width = intrinsic.GetIntrinsicSize().Width + style.ContentInsets.Horizontal;
        }
        else if (node is ITextSource { Text: not null } source)
        {
            Size unconstrained = new(double.PositiveInfinity, double.PositiveInfinity);
            Size measured = _textMeasurer.MeasureText(source, unconstrained, style);
            width = measured.Width + style.ContentInsets.Horizontal;
        }
        else
        {
            width = MeasureMaxContentOfChildren(node) + style.ContentInsets.Horizontal;
        }

        // Only pixel limits apply. The containing width is unknown during measurement.
        double maxContentWidth = ClampWidth(style, width, double.PositiveInfinity);
        state.MaxContentWidth = maxContentWidth;
        return maxContentWidth;
    }

    private double MeasureMaxContentOfChildren(ILayoutNode node)
    {
        if (node is IGridLayoutNode grid)
        {
            return MeasureMaxContentOfGrid(grid);
        }

        bool horizontal = node is IStackLayoutNode { Orientation: Orientation.Horizontal };
        double width = 0;
        int count = 0;
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            double childWidth = MeasureMaxContentWidth(child) + child.ComputedStyle.Margin.Horizontal;
            width = horizontal ? width + childWidth : Math.Max(width, childWidth);
            count++;
        }

        if (horizontal && count > 1 && node is IStackLayoutNode stack)
        {
            width += GetStackSpacing(stack) * (count - 1);
        }

        return width;
    }

    /// <summary>
    /// Sizes each non-fixed column to its single-column children, the maximum content width of a grid.
    /// </summary>
    private double MeasureMaxContentOfGrid(IGridLayoutNode grid)
    {
        List<GridLength> columns = grid.Columns.Count > 0 ? [.. grid.Columns] : [GridLength.Star()];
        double[] sizes = new double[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            sizes[i] = columns[i].IsPixels ? Math.Max(0, columns[i].Value) : 0;
        }

        int rowCount = Math.Max(1, grid.Rows.Count);
        List<(ILayoutNode Node, GridPlacement Placement)> children = GetGridChildren(grid, rowCount, columns.Count);
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            if (placement.ColumnSpan == 1 && !columns[placement.Column].IsPixels)
            {
                double size = MeasureMaxContentWidth(node) + node.ComputedStyle.Margin.Horizontal;
                sizes[placement.Column] = Math.Max(sizes[placement.Column], size);
            }
        }

        double spacing = double.IsFinite(grid.ColumnSpacing) ? Math.Max(0, grid.ColumnSpacing) : 0;
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            if (placement.ColumnSpan > 1)
            {
                double childWidth = MeasureMaxContentWidth(node) + node.ComputedStyle.Margin.Horizontal;
                double currentSpan = MeasureSpan(sizes, placement.Column, placement.ColumnSpan, spacing);
                double missing = childWidth - currentSpan;
                if (missing > 0)
                {
                    int growable = 0;
                    for (int c = placement.Column; c < placement.Column + placement.ColumnSpan; c++)
                    {
                        if (!columns[c].IsPixels)
                        {
                            growable++;
                        }
                    }

                    if (growable > 0)
                    {
                        double add = missing / growable;
                        for (int c = placement.Column; c < placement.Column + placement.ColumnSpan; c++)
                        {
                            if (!columns[c].IsPixels)
                            {
                                sizes[c] += add;
                            }
                        }
                    }
                }
            }
        }

        return MeasureSpan(sizes, 0, sizes.Length, spacing);
    }
}
