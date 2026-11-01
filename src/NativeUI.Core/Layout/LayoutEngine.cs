using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Provides layout inputs for an element participating in <see cref="LayoutEngine"/>.
/// </summary>
public interface ILayoutNode
{
    /// <summary>
    /// Gets a value indicating whether the element participates in layout and rendering.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets the resolved <see cref="ComputedStyle"/> for the element.
    /// </summary>
    ComputedStyle ComputedStyle { get; }

    /// <summary>
    /// Gets the text content if this element is a text leaf, or <see langword="null"/> for container elements.
    /// </summary>
    string? TextContent { get; }

    /// <summary>
    /// Gets the child layout nodes of this element.
    /// </summary>
    IReadOnlyList<ILayoutNode> LayoutChildren { get; }
}

/// <summary>
/// Calculates CSS box-model geometry (<c>margin</c>, <c>padding</c>, <c>width</c>, <c>height</c>,
/// block/vertical flow, and text measurement) for a tree of <see cref="ILayoutNode"/> elements.
/// </summary>
public sealed class LayoutEngine
{
    private readonly ITextMeasurer _textMeasurer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutEngine"/> class.
    /// </summary>
    public LayoutEngine(ITextMeasurer textMeasurer)
    {
        ArgumentNullException.ThrowIfNull(textMeasurer);
        _textMeasurer = textMeasurer;
    }

    /// <summary>
    /// Computes the <see cref="LayoutBox"/> tree for <paramref name="root"/> within the given <paramref name="viewport"/> size.
    /// </summary>
    public LayoutBox ComputeLayout(ILayoutNode root, Size viewport)
    {
        ArgumentNullException.ThrowIfNull(root);
        return LayoutNodeRecursive(root, 0, 0, viewport.Width);
    }

    private double ResolveBorderWidth(ILayoutNode node, double availableWidth)
    {
        ComputedStyle style = node.ComputedStyle;
        double fillWidth = Math.Max(0, availableWidth - style.Margin.Horizontal);
        return style.Width switch
        {
            { Pixels: double pixels } => pixels,
            { IsFitContent: true } => Math.Min(MeasureMaxContentWidth(node), fillWidth),
            _ => fillWidth
        };
    }

    /// <summary>
    /// Returns the border-box width of <paramref name="node"/> when none of its text wraps.
    /// </summary>
    private double MeasureMaxContentWidth(ILayoutNode node)
    {
        ComputedStyle style = node.ComputedStyle;
        if (style.Width?.Pixels is double pixels)
        {
            return pixels;
        }

        if (node.TextContent is string text)
        {
            Size unconstrained = new(double.PositiveInfinity, double.PositiveInfinity);
            return _textMeasurer.MeasureText(text, unconstrained, style).Width + style.Padding.Horizontal;
        }

        return MeasureMaxContentOfChildren(node) + style.Padding.Horizontal;
    }

    private double MeasureMaxContentOfChildren(ILayoutNode node)
    {
        if (node is IGridLayoutNode grid)
        {
            return MeasureMaxContentOfGrid(grid);
        }

        double[] widths = VisibleChildren(node)
            .Select(child => MeasureMaxContentWidth(child) + child.ComputedStyle.Margin.Horizontal)
            .ToArray();
        if (widths.Length == 0)
        {
            return 0;
        }

        return node is IStackLayoutNode { Orientation: Orientation.Horizontal } stack
            ? widths.Sum() + (stack.Spacing * (widths.Length - 1))
            : widths.Max();
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
            sizes[i] = columns[i].IsPixels ? columns[i].Value : 0;
        }

        int rowCount = Math.Max(1, grid.Rows.Count);
        foreach ((ILayoutNode node, GridPlacement placement) in GetGridChildren(grid, rowCount, columns.Count))
        {
            if (placement.ColumnSpan == 1 && !columns[placement.Column].IsPixels)
            {
                double size = MeasureMaxContentWidth(node) + node.ComputedStyle.Margin.Horizontal;
                sizes[placement.Column] = Math.Max(sizes[placement.Column], size);
            }
        }

        return MeasureSpan(sizes, 0, sizes.Length, grid.ColumnSpacing);
    }

    private ContentLayout LayoutVerticalChildren(ILayoutNode node, double spacing, double x, double y, double width)
    {
        List<LayoutBox> boxes = [];
        double currentY = y;
        double maxWidth = 0;
        foreach (ILayoutNode child in VisibleChildren(node))
        {
            if (boxes.Count > 0)
            {
                currentY += spacing;
            }

            LayoutBox box = LayoutNodeRecursive(child, x, currentY, width);
            boxes.Add(box);
            currentY += box.BorderBox.Height + child.ComputedStyle.Margin.Vertical;
            maxWidth = Math.Max(maxWidth, box.BorderBox.Width + child.ComputedStyle.Margin.Horizontal);
        }

        return new ContentLayout(boxes, maxWidth, Math.Max(0, currentY - y));
    }

    private ContentLayout LayoutHorizontalStack(IStackLayoutNode stack, double x, double y, double width)
    {
        List<LayoutBox> boxes = [];
        double currentX = x;
        double maxHeight = 0;
        foreach (ILayoutNode child in VisibleChildren(stack))
        {
            if (boxes.Count > 0)
            {
                currentX += stack.Spacing;
            }

            Thickness margin = child.ComputedStyle.Margin;
            double remaining = Math.Max(0, x + width - currentX);
            LayoutBox box = LayoutNodeRecursive(child, currentX, y, Math.Min(MeasureMaxContentWidth(child) + margin.Horizontal, remaining));
            boxes.Add(box);
            currentX += box.BorderBox.Width + margin.Horizontal;
            maxHeight = Math.Max(maxHeight, box.BorderBox.Height + margin.Vertical);
        }

        return new ContentLayout(boxes, Math.Max(0, currentX - x), maxHeight);
    }

    /// <summary>
    /// Resolves columns, lays out each child in its column width to learn its height, resolves rows, then moves each
    /// child to its cell. Children keep their own height, so a child does not stretch to fill its row.
    /// </summary>
    private ContentLayout LayoutGrid(IGridLayoutNode grid, double x, double y, double width, double? definiteHeight)
    {
        List<GridLength> rows = grid.Rows.Count > 0 ? [.. grid.Rows] : [GridLength.Star()];
        List<GridLength> columns = grid.Columns.Count > 0 ? [.. grid.Columns] : [GridLength.Star()];
        List<(ILayoutNode Node, GridPlacement Placement)> children = GetGridChildren(grid, rows.Count, columns.Count);

        double[] columnWidths = ResolveTracks(
            columns,
            grid.ColumnSpacing,
            width,
            children.Where(static c => c.Placement.ColumnSpan == 1)
                .Select(c => (c.Placement.Column, MeasureMaxContentWidth(c.Node) + c.Node.ComputedStyle.Margin.Horizontal)));

        List<LayoutBox> placedAtOrigin = [];
        foreach ((ILayoutNode node, GridPlacement placement) in children)
        {
            double cellWidth = MeasureSpan(columnWidths, placement.Column, placement.ColumnSpan, grid.ColumnSpacing);
            placedAtOrigin.Add(LayoutNodeRecursive(node, 0, 0, cellWidth));
        }

        double[] rowHeights = ResolveRowHeights(grid, rows, children, placedAtOrigin, definiteHeight);
        double[] columnOffsets = GetTrackOffsets(columnWidths, grid.ColumnSpacing);
        double[] rowOffsets = GetTrackOffsets(rowHeights, grid.RowSpacing);

        List<LayoutBox> boxes = [];
        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            boxes.Add(placedAtOrigin[i].Translated(x + columnOffsets[placement.Column], y + rowOffsets[placement.Row]));
        }

        return new ContentLayout(
            boxes,
            MeasureSpan(columnWidths, 0, columnWidths.Length, grid.ColumnSpacing),
            MeasureSpan(rowHeights, 0, rowHeights.Length, grid.RowSpacing));
    }

    private double[] ResolveRowHeights(
        IGridLayoutNode grid,
        List<GridLength> rows,
        List<(ILayoutNode Node, GridPlacement Placement)> children,
        List<LayoutBox> boxes,
        double? definiteHeight)
    {
        double[] heights = new double[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            heights[i] = rows[i].IsPixels ? rows[i].Value : 0;
        }

        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            if (placement.RowSpan == 1 && !rows[placement.Row].IsPixels)
            {
                heights[placement.Row] = Math.Max(heights[placement.Row], GetOuterHeight(children[i].Node, boxes[i]));
            }
        }

        // A child spanning rows gives any height the rows lack to the last spanned row that can grow.
        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            if (placement.RowSpan <= 1)
            {
                continue;
            }

            double missing = GetOuterHeight(children[i].Node, boxes[i]) - MeasureSpan(heights, placement.Row, placement.RowSpan, grid.RowSpacing);
            int growRow = Enumerable.Range(placement.Row, placement.RowSpan).LastOrDefault(row => !rows[row].IsPixels, -1);
            if (missing > 0 && growRow >= 0)
            {
                heights[growRow] += missing;
            }
        }

        if (definiteHeight is double available)
        {
            DistributeToStarRows(rows, heights, available - MeasureSpan(heights, 0, heights.Length, grid.RowSpacing));
        }

        return heights;
    }

    /// <summary>
    /// Gives each star row its weighted share of the free height, without shrinking it below its content.
    /// </summary>
    private static void DistributeToStarRows(List<GridLength> rows, double[] heights, double freeHeight)
    {
        double totalHeight = freeHeight;
        double totalWeight = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsStar)
            {
                totalHeight += heights[i];
                totalWeight += rows[i].Value;
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
                heights[i] = Math.Max(heights[i], totalHeight * rows[i].Value / totalWeight);
            }
        }
    }

    private static double[] ResolveTracks(
        List<GridLength> tracks,
        double spacing,
        double available,
        IEnumerable<(int Track, double ContentSize)> items)
    {
        double[] sizes = new double[tracks.Count];
        for (int i = 0; i < tracks.Count; i++)
        {
            sizes[i] = tracks[i].IsPixels ? tracks[i].Value : 0;
        }

        foreach ((int track, double contentSize) in items)
        {
            if (tracks[track].IsAuto)
            {
                sizes[track] = Math.Max(sizes[track], contentSize);
            }
        }

        double free = Math.Max(0, available - MeasureSpan(sizes, 0, sizes.Length, spacing));
        double totalWeight = tracks.Where(static t => t.IsStar).Sum(static t => t.Value);
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].IsStar && totalWeight > 0)
            {
                sizes[i] = free * tracks[i].Value / totalWeight;
            }
        }

        return sizes;
    }

    private static List<(ILayoutNode Node, GridPlacement Placement)> GetGridChildren(IGridLayoutNode grid, int rowCount, int columnCount)
    {
        List<(ILayoutNode Node, GridPlacement Placement)> children = [];
        foreach (ILayoutNode child in VisibleChildren(grid))
        {
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

    private static double[] GetTrackOffsets(double[] sizes, double spacing)
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

    private static double GetOuterHeight(ILayoutNode node, LayoutBox box)
    {
        return box.BorderBox.Height + node.ComputedStyle.Margin.Vertical;
    }

    private static IEnumerable<ILayoutNode> VisibleChildren(ILayoutNode node)
    {
        return node.LayoutChildren.Where(static child => child.IsVisible);
    }

    private readonly record struct ContentLayout(List<LayoutBox> Boxes, double Width, double Height);

    private LayoutBox LayoutNodeRecursive(ILayoutNode node, double availableX, double availableY, double availableWidth)
    {
        ComputedStyle style = node.ComputedStyle;
        Thickness margin = style.Margin;
        Thickness padding = style.Padding;

        double borderX = availableX + margin.Left;
        double borderY = availableY + margin.Top;
        double borderWidth = ResolveBorderWidth(node, availableWidth);
        double contentX = borderX + padding.Left;
        double contentY = borderY + padding.Top;
        double contentMaxWidth = Math.Max(0, borderWidth - padding.Horizontal);

        if (node.TextContent is string text)
        {
            Size availableTextSize = new(contentMaxWidth, style.Height ?? double.PositiveInfinity);
            ITextLayout textLayout = _textMeasurer.CreateTextLayout(text, availableTextSize, style);
            Size measured = textLayout.Size;

            double? explicitWidth = style.Width?.Pixels;
            double textWidth = explicitWidth.HasValue ? contentMaxWidth : Math.Min(contentMaxWidth, measured.Width);
            double textHeight = measured.Height;
            double finalBorderWidth = explicitWidth ?? Math.Max(textWidth + padding.Horizontal, contentMaxWidth);
            double finalBorderHeight = style.Height ?? (textHeight + padding.Vertical);

            return new LayoutBox(
                node,
                style,
                new Rect(borderX, borderY, finalBorderWidth, finalBorderHeight),
                new Rect(contentX, contentY, Math.Max(0, finalBorderWidth - padding.Horizontal), textHeight),
                [],
                textLayout);
        }

        double? contentDefiniteHeight = style.Height is double borderHeight ? Math.Max(0, borderHeight - padding.Vertical) : null;
        ContentLayout content = node switch
        {
            IGridLayoutNode grid => LayoutGrid(grid, contentX, contentY, contentMaxWidth, contentDefiniteHeight),
            IStackLayoutNode { Orientation: Orientation.Horizontal } stack => LayoutHorizontalStack(stack, contentX, contentY, contentMaxWidth),
            IStackLayoutNode stack => LayoutVerticalChildren(stack, stack.Spacing, contentX, contentY, contentMaxWidth),
            _ => LayoutVerticalChildren(node, 0, contentX, contentY, contentMaxWidth)
        };

        List<LayoutBox> childBoxes = content.Boxes;
        double maxChildWidth = content.Width;
        double contentHeight = content.Height;
        double resolvedBorderHeight = style.Height ?? (contentHeight + padding.Vertical);

        if (style.ClipsOverflow && node is IScrollContainer scrollContainer)
        {
            Size viewport = new(borderWidth, resolvedBorderHeight);
            Size extent = new(
                Math.Max(borderWidth, maxChildWidth + padding.Horizontal),
                Math.Max(resolvedBorderHeight, contentHeight + padding.Vertical));
            Point offset = scrollContainer.UpdateScrollMetrics(extent, viewport);
            if (offset != default)
            {
                childBoxes = childBoxes.ConvertAll(child => child.Translated(-offset.X, -offset.Y));
            }
        }

        return new LayoutBox(
            node,
            style,
            new Rect(borderX, borderY, borderWidth, resolvedBorderHeight),
            new Rect(contentX, contentY, contentMaxWidth, Math.Max(0, resolvedBorderHeight - padding.Vertical)),
            childBoxes);
    }
}
