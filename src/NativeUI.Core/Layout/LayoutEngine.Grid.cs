using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Resolves columns, measures each child in its column width to learn its height, resolves rows, then offsets each
    /// child to its cell. A child whose height is a percentage, or auto and shorter than its cell, is measured again against
    /// the height of its cell, as the height of a grid area is not definite until the rows are resolved. Auto-height
    /// children stretch to fill the cell, except those with an intrinsic size.
    /// </summary>
    private Size MeasureGrid(
        IGridLayoutNode grid,
        Point contentOrigin,
        double width,
        double? definiteHeight,
        LayoutConstraints constraints)
    {
        List<GridLength> rows = grid.Rows.Count > 0 ? [.. grid.Rows] : [GridLength.Star()];
        List<GridLength> columns = grid.Columns.Count > 0 ? [.. grid.Columns] : [GridLength.Star()];
        List<(ILayoutNode Node, GridPlacement Placement)> children = GetGridChildren(grid, rows.Count, columns.Count);

        List<(int Column, double MaxContentWidth)> columnContents = [];
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            if (placement.ColumnSpan == 1)
            {
                columnContents.Add((placement.Column, MeasureMaxContentWidth(node) + node.ComputedStyle.Margin.Horizontal));
            }
        }

        double colSpacing = double.IsFinite(grid.ColumnSpacing) ? Math.Max(0, grid.ColumnSpacing) : 0;
        double rowSpacing = double.IsFinite(grid.RowSpacing) ? Math.Max(0, grid.RowSpacing) : 0;
        double[] columnWidths = ResolveColumnWidths(columns, colSpacing, width, columnContents);

        Size[] sizes = new Size[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            double cellWidth = MeasureSpan(columnWidths, placement.Column, placement.ColumnSpan, colSpacing);
            sizes[i] = Measure(node, constraints with { AvailableSize = new Size(cellWidth, double.PositiveInfinity) });
        }

        double[] rowHeights = ResolveRowHeights(grid, rows, children, sizes, definiteHeight);
        double[] columnOffsets = GetOffsets(columnWidths, colSpacing);
        double[] rowOffsets = GetOffsets(rowHeights, rowSpacing);

        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            ComputedStyle childStyle = node.ComputedStyle;
            double areaHeight = MeasureSpan(rowHeights, placement.Row, placement.RowSpan, rowSpacing);
            double stretchHeight = Math.Max(0, areaHeight - childStyle.Margin.Vertical);
            bool stretches = childStyle.Height.IsAuto && node is not IIntrinsicSize && stretchHeight > sizes[i].Height;
            if (!childStyle.Height.IsPercent && !stretches)
            {
                continue;
            }

            double cellWidth = MeasureSpan(columnWidths, placement.Column, placement.ColumnSpan, colSpacing);
            sizes[i] = Measure(node, constraints with
            {
                AvailableSize = new Size(cellWidth, areaHeight),
                StretchHeight = stretches ? stretchHeight : null
            });
        }

        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            Thickness margin = node.ComputedStyle.Margin;
            Point borderOrigin = new(
                contentOrigin.X + columnOffsets[placement.Column] + margin.Left,
                contentOrigin.Y + rowOffsets[placement.Row] + margin.Top);
            GetState(node).VisualOffset = ApplyRelativeOffset(node.ComputedStyle, borderOrigin, new Size(width, definiteHeight ?? double.PositiveInfinity));
        }

        return new Size(
            MeasureSpan(columnWidths, 0, columnWidths.Length, colSpacing),
            MeasureSpan(rowHeights, 0, rowHeights.Length, rowSpacing));
    }

    /// <summary>
    /// Resolves the height of each row. Pixel rows keep their length and auto rows fit their tallest item. When the grid
    /// has a definite height, star rows share the height that the other rows leave, and their items do not enlarge them.
    /// Otherwise star rows fit their tallest item.
    /// </summary>
    private static double[] ResolveRowHeights(
        IGridLayoutNode grid,
        List<GridLength> rows,
        List<(ILayoutNode Node, GridPlacement Placement)> children,
        Size[] sizes,
        double? definiteHeight)
    {
        bool isSizedToContent = definiteHeight is null;
        double[] heights = new double[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            heights[i] = rows[i].IsPixels ? Math.Max(0, rows[i].Value) : 0;
        }

        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            GridLength row = rows[placement.Row];
            if (placement.RowSpan == 1 && !row.IsPixels && (isSizedToContent || !row.IsStar))
            {
                heights[placement.Row] = Math.Max(heights[placement.Row], GetOuterHeight(node, sizes[i]));
            }
        }

        double rowSpacing = double.IsFinite(grid.RowSpacing) ? Math.Max(0, grid.RowSpacing) : 0;
        // A child spanning rows gives any height the rows lack to the last spanned row that can grow.
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            if (placement.RowSpan <= 1)
            {
                continue;
            }

            double missing = GetOuterHeight(node, sizes[i]) - MeasureSpan(heights, placement.Row, placement.RowSpan, rowSpacing);
            int growRow = Enumerable.Range(placement.Row, placement.RowSpan)
                .LastOrDefault(row => !rows[row].IsPixels && (isSizedToContent || !rows[row].IsStar), -1);
            if (missing > 0 && growRow >= 0)
            {
                heights[growRow] += missing;
            }
        }

        if (definiteHeight is double available)
        {
            DistributeToStarRows(rows, heights, available - MeasureSpan(heights, 0, heights.Length, rowSpacing));
        }

        return heights;
    }

    /// <summary>
    /// Gives each star row its weighted share of <paramref name="freeHeight"/>, which is the height that the other rows
    /// leave.
    /// </summary>
    private static void DistributeToStarRows(List<GridLength> rows, double[] heights, double freeHeight)
    {
        double totalWeight = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsStar)
            {
                totalWeight += Math.Max(0, rows[i].Value);
            }
        }

        if (totalWeight <= 0)
        {
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsStar)
            {
                heights[i] = Math.Max(0, freeHeight) * Math.Max(0, rows[i].Value) / totalWeight;
            }
        }
    }

    private static double[] ResolveColumnWidths(
        List<GridLength> columns,
        double spacing,
        double available,
        List<(int Column, double MaxContentWidth)> items)
    {
        double[] sizes = new double[columns.Count];
        double[] maxContentWidths = new double[columns.Count];

        for (int i = 0; i < columns.Count; i++)
        {
            sizes[i] = columns[i].IsPixels ? Math.Max(0, columns[i].Value) : 0;
        }

        for (int i = 0; i < items.Count; i++)
        {
            (int column, double maxContentWidth) = items[i];
            maxContentWidths[column] = Math.Max(maxContentWidths[column], maxContentWidth);
            if (columns[column].IsAuto)
            {
                sizes[column] = Math.Max(sizes[column], maxContentWidth);
            }
        }

        double fixedAndAuto = MeasureSpan(sizes, 0, sizes.Length, spacing);
        double free = Math.Max(0, available - fixedAndAuto);
        double totalWeight = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].IsStar)
            {
                totalWeight += Math.Max(0, columns[i].Value);
            }
        }

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].IsStar)
            {
                if (double.IsPositiveInfinity(available))
                {
                    sizes[i] = maxContentWidths[i];
                }
                else if (totalWeight > 0)
                {
                    sizes[i] = free * columns[i].Value / totalWeight;
                }
            }
        }

        return sizes;
    }

    private static List<(ILayoutNode Node, GridPlacement Placement)> GetGridChildren(IGridLayoutNode grid, int rowCount, int columnCount)
    {
        List<(ILayoutNode Node, GridPlacement Placement)> children = [];
        IReadOnlyList<ILayoutNode> allChildren = grid.LayoutChildren;
        for (int i = 0; i < allChildren.Count; i++)
        {
            ILayoutNode child = allChildren[i];
            if (!IsInFlow(child))
            {
                continue;
            }

            children.Add((child, ClampPlacement(grid.GetPlacement(child), rowCount, columnCount)));
        }

        return children;
    }

    private static GridPlacement ClampPlacement(GridPlacement placement, int rowCount, int columnCount)
    {
        int row = Math.Clamp(placement.Row, 0, rowCount - 1);
        int column = Math.Clamp(placement.Column, 0, columnCount - 1);
        return new GridPlacement(
            row,
            column,
            Math.Clamp(placement.RowSpan, 1, rowCount - row),
            Math.Clamp(placement.ColumnSpan, 1, columnCount - column));
    }

    private static double[] GetOffsets(double[] sizes, double spacing)
    {
        double[] offsets = new double[sizes.Length];
        double offset = 0;
        for (int i = 0; i < sizes.Length; i++)
        {
            offsets[i] = offset;
            offset += sizes[i] + spacing;
        }

        return offsets;
    }

    private static double MeasureSpan(double[] sizes, int start, int count, double spacing)
    {
        double total = spacing * Math.Max(0, count - 1);
        for (int i = start; i < start + count; i++)
        {
            total += sizes[i];
        }

        return total;
    }

    private static double GetOuterHeight(ILayoutNode node, Size size)
    {
        return size.Height + node.ComputedStyle.Margin.Vertical;
    }
}
