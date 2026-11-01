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
        if (!root.IsVisible)
        {
            return new LayoutBox(root, root.ComputedStyle, Rect.Empty, Rect.Empty, []);
        }

        double width = Math.Max(0, viewport.Width);
        double height = Math.Max(0, viewport.Height);
        Size clampedViewport = new(width, height);
        Rect viewportRect = new(0, 0, width, height);
        return LayoutNodeRecursive(root, Point.Zero, clampedViewport, viewportRect, clampedViewport);
    }

    private double ResolveBorderWidth(ILayoutNode node, double availableWidth, double? knownMaxContentWidth = null)
    {
        ComputedStyle style = node.ComputedStyle;
        double fillWidth = Math.Max(0, availableWidth - style.Margin.Horizontal);
        return style.Width switch
        {
            { Pixels: double pixels } => pixels,
            { IsFitContent: true } => Math.Min(knownMaxContentWidth ?? MeasureMaxContentWidth(node), fillWidth),
            _ => (style.IsOutOfFlow && !(style.Left.HasValue && style.Right.HasValue))
                ? Math.Min(knownMaxContentWidth ?? MeasureMaxContentWidth(node), fillWidth)
                : fillWidth
        };
    }

    /// <summary>
    /// Returns the border-box width of <paramref name="node"/> when none of its text wraps.
    /// </summary>
    private double MeasureMaxContentWidth(ILayoutNode node)
    {
        if (!node.IsVisible)
        {
            return 0;
        }

        ComputedStyle style = node.ComputedStyle;
        if (style.Width?.Pixels is double pixels)
        {
            return pixels;
        }

        if (node.TextContent is string text)
        {
            Size unconstrained = new(double.PositiveInfinity, double.PositiveInfinity);
            return _textMeasurer.MeasureText(text, unconstrained, style).Width + style.ContentInsets.Horizontal;
        }

        return MeasureMaxContentOfChildren(node) + style.ContentInsets.Horizontal;
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
            if (!child.IsVisible)
            {
                continue;
            }

            ComputedStyle style = child.ComputedStyle;
            if (style.Position is not (Position.Static or Position.Relative))
            {
                continue;
            }

            double childWidth = MeasureMaxContentWidth(child) + style.Margin.Horizontal;
            width = horizontal ? width + childWidth : Math.Max(width, childWidth);
            count++;
        }

        if (horizontal && count > 1 && node is IStackLayoutNode stack)
        {
            double spacing = double.IsFinite(stack.Spacing) ? Math.Max(0, stack.Spacing) : 0;
            width += spacing * (count - 1);
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

    private ContentLayout LayoutVerticalChildren(
        ILayoutNode node,
        double spacing,
        double x,
        double y,
        double width,
        Rect containingBlock,
        Size viewport)
    {
        List<LayoutBox> boxes = [];
        double currentY = y;
        double maxWidth = 0;
        double effectiveSpacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }

            ComputedStyle style = child.ComputedStyle;
            if (style.Position is not (Position.Static or Position.Relative))
            {
                continue;
            }

            if (boxes.Count > 0)
            {
                currentY += effectiveSpacing;
            }

            Point origin = new(x, currentY);
            Size available = new(width, double.PositiveInfinity);
            LayoutBox box = LayoutNodeRecursive(child, origin, available, containingBlock, viewport);
            boxes.Add(ApplyRelativeOffset(box));
            currentY += box.BorderBox.Height + style.Margin.Vertical;
            maxWidth = Math.Max(maxWidth, box.BorderBox.Width + style.Margin.Horizontal);
        }

        return new ContentLayout(boxes, maxWidth, Math.Max(0, currentY - y));
    }

    private ContentLayout LayoutHorizontalStack(
        IStackLayoutNode stack,
        double x,
        double y,
        double width,
        Rect containingBlock,
        Size viewport)
    {
        List<LayoutBox> boxes = [];
        double currentX = x;
        double maxHeight = 0;
        double spacing = double.IsFinite(stack.Spacing) ? Math.Max(0, stack.Spacing) : 0;
        IReadOnlyList<ILayoutNode> children = stack.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }

            ComputedStyle style = child.ComputedStyle;
            if (style.Position is not (Position.Static or Position.Relative))
            {
                continue;
            }

            if (boxes.Count > 0)
            {
                currentX += spacing;
            }

            Thickness margin = style.Margin;
            double remaining = Math.Max(0, x + width - currentX);
            double desiredWidth = MeasureMaxContentWidth(child);
            double availableWidth = Math.Min(desiredWidth + margin.Horizontal, remaining);

            Point origin = new(currentX, y);
            Size available = new(availableWidth, double.PositiveInfinity);
            LayoutBox box = LayoutNodeRecursive(child, origin, available, containingBlock, viewport, desiredWidth);
            boxes.Add(ApplyRelativeOffset(box));
            currentX += box.BorderBox.Width + margin.Horizontal;
            maxHeight = Math.Max(maxHeight, box.BorderBox.Height + margin.Vertical);
        }

        double usedWidth = Math.Max(0, currentX - x);
        double shift = stack.HorizontalAlignment switch
        {
            Alignment.End => Math.Max(0, width - usedWidth),
            Alignment.Center => Math.Max(0, width - usedWidth) / 2.0,
            _ => 0
        };

        if (shift > 0)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i] = boxes[i].Translated(shift, 0);
            }
        }

        return new ContentLayout(boxes, usedWidth, maxHeight);
    }

    /// <summary>
    /// Resolves columns, lays out each child in its column width to learn its height, resolves rows, then moves each
    /// child to its cell. Children keep their own height, so a child does not stretch to fill its row.
    /// </summary>
    private ContentLayout LayoutGrid(
        IGridLayoutNode grid,
        double x,
        double y,
        double width,
        double? definiteHeight,
        Rect containingBlock,
        Size viewport)
    {
        List<GridLength> rows = grid.Rows.Count > 0 ? [.. grid.Rows] : [GridLength.Star()];
        List<GridLength> columns = grid.Columns.Count > 0 ? [.. grid.Columns] : [GridLength.Star()];
        List<(ILayoutNode Node, GridPlacement Placement)> children = GetGridChildren(grid, rows.Count, columns.Count);

        List<(int Track, double ContentSize)> singleColContent = [];
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Placement.ColumnSpan == 1)
            {
                singleColContent.Add((children[i].Placement.Column, MeasureMaxContentWidth(children[i].Node) + children[i].Node.ComputedStyle.Margin.Horizontal));
            }
        }

        double colSpacing = double.IsFinite(grid.ColumnSpacing) ? Math.Max(0, grid.ColumnSpacing) : 0;
        double rowSpacing = double.IsFinite(grid.RowSpacing) ? Math.Max(0, grid.RowSpacing) : 0;
        double[] columnWidths = ResolveTracks(columns, colSpacing, width, singleColContent);

        List<LayoutBox> placedAtOrigin = [];
        for (int i = 0; i < children.Count; i++)
        {
            (ILayoutNode node, GridPlacement placement) = children[i];
            double cellWidth = MeasureSpan(columnWidths, placement.Column, placement.ColumnSpan, colSpacing);
            Point origin = Point.Zero;
            Size available = new(cellWidth, double.PositiveInfinity);
            placedAtOrigin.Add(LayoutNodeRecursive(node, origin, available, containingBlock, viewport));
        }

        double[] rowHeights = ResolveRowHeights(grid, rows, children, placedAtOrigin, definiteHeight);
        double[] columnOffsets = GetTrackOffsets(columnWidths, colSpacing);
        double[] rowOffsets = GetTrackOffsets(rowHeights, rowSpacing);

        List<LayoutBox> boxes = [];
        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            double cellX = x + columnOffsets[placement.Column];
            double cellY = y + rowOffsets[placement.Row];
            boxes.Add(ApplyRelativeOffset(placedAtOrigin[i].Translated(cellX, cellY)));
        }

        return new ContentLayout(
            boxes,
            MeasureSpan(columnWidths, 0, columnWidths.Length, colSpacing),
            MeasureSpan(rowHeights, 0, rowHeights.Length, rowSpacing));
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
            heights[i] = rows[i].IsPixels ? Math.Max(0, rows[i].Value) : 0;
        }

        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            if (placement.RowSpan == 1 && !rows[placement.Row].IsPixels)
            {
                heights[placement.Row] = Math.Max(heights[placement.Row], GetOuterHeight(children[i].Node, boxes[i]));
            }
        }

        double rowSpacing = double.IsFinite(grid.RowSpacing) ? Math.Max(0, grid.RowSpacing) : 0;
        // A child spanning rows gives any height the rows lack to the last spanned row that can grow.
        for (int i = 0; i < children.Count; i++)
        {
            GridPlacement placement = children[i].Placement;
            if (placement.RowSpan <= 1)
            {
                continue;
            }

            double missing = GetOuterHeight(children[i].Node, boxes[i]) - MeasureSpan(heights, placement.Row, placement.RowSpan, rowSpacing);
            int growRow = Enumerable.Range(placement.Row, placement.RowSpan).LastOrDefault(row => !rows[row].IsPixels, -1);
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
                heights[i] = Math.Max(heights[i], totalHeight * rows[i].Value / totalWeight);
            }
        }
    }

    private static double[] ResolveTracks(
        List<GridLength> tracks,
        double spacing,
        double available,
        List<(int Track, double ContentSize)> items)
    {
        double[] sizes = new double[tracks.Count];
        double[] contentSizes = new double[tracks.Count];

        for (int i = 0; i < tracks.Count; i++)
        {
            sizes[i] = tracks[i].IsPixels ? Math.Max(0, tracks[i].Value) : 0;
        }

        for (int i = 0; i < items.Count; i++)
        {
            (int track, double contentSize) = items[i];
            contentSizes[track] = Math.Max(contentSizes[track], contentSize);
            if (tracks[track].IsAuto)
            {
                sizes[track] = Math.Max(sizes[track], contentSize);
            }
        }

        double fixedAndAuto = MeasureSpan(sizes, 0, sizes.Length, spacing);
        double free = Math.Max(0, available - fixedAndAuto);
        double totalWeight = 0;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].IsStar)
            {
                totalWeight += Math.Max(0, tracks[i].Value);
            }
        }

        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].IsStar)
            {
                if (double.IsPositiveInfinity(available))
                {
                    sizes[i] = contentSizes[i];
                }
                else if (totalWeight > 0)
                {
                    sizes[i] = free * tracks[i].Value / totalWeight;
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
            if (!child.IsVisible)
            {
                continue;
            }

            if (child.ComputedStyle.Position is not (Position.Static or Position.Relative))
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

    private static LayoutBox ApplyRelativeOffset(LayoutBox box)
    {
        ComputedStyle style = box.Style;
        if (style.Position != Position.Relative)
        {
            return box;
        }

        double dx = style.Left.HasValue ? style.Left.Value : (style.Right.HasValue ? -style.Right.Value : 0);
        double dy = style.Top.HasValue ? style.Top.Value : (style.Bottom.HasValue ? -style.Bottom.Value : 0);
        return (dx != 0 || dy != 0) ? box.Translated(dx, dy) : box;
    }

    private readonly record struct ContentLayout(List<LayoutBox> Boxes, double Width, double Height);

    private LayoutBox LayoutNodeRecursive(
        ILayoutNode node,
        Point origin,
        Size availableSize,
        Rect containingBlock,
        Size viewport,
        double? knownMaxContentWidth = null)
    {
        if (!node.IsVisible)
        {
            return new LayoutBox(node, node.ComputedStyle, Rect.Empty, Rect.Empty, []);
        }

        ComputedStyle style = node.ComputedStyle;
        Thickness margin = style.Margin;
        Thickness padding = style.ContentInsets;

        double borderX = origin.X + margin.Left;
        double borderY = origin.Y + margin.Top;
        double borderWidth = ResolveBorderWidth(node, availableSize.Width, knownMaxContentWidth);
        double contentX = borderX + padding.Left;
        double contentY = borderY + padding.Top;
        double contentMaxWidth = Math.Max(0, borderWidth - padding.Horizontal);

        if (node.TextContent is string text)
        {
            double finalBorderWidth = borderWidth;
            Size availableTextSize = new(contentMaxWidth, style.Height ?? double.PositiveInfinity);
            ITextLayout textLayout = _textMeasurer.CreateTextLayout(text, availableTextSize, style);
            Size measured = textLayout.Size;

            double finalBorderHeight = style.Height ?? (measured.Height + padding.Vertical);

            return new LayoutBox(
                node,
                style,
                new Rect(borderX, borderY, finalBorderWidth, finalBorderHeight),
                new Rect(contentX, contentY, Math.Max(0, finalBorderWidth - padding.Horizontal), measured.Height),
                [],
                textLayout);
        }

        double? contentDefiniteHeight = style.Height is double borderHeight ? Math.Max(0, borderHeight - padding.Vertical) : null;

        double padX = borderX + style.BorderWidth;
        double padY = borderY + style.BorderWidth;
        double padWidth = Math.Max(0, borderWidth - (style.BorderWidth * 2));
        Rect inFlowContainingBlock = style.IsPositioned
            ? new Rect(padX, padY, padWidth, 0)
            : containingBlock;

        ContentLayout content = node switch
        {
            IGridLayoutNode grid => LayoutGrid(grid, contentX, contentY, contentMaxWidth, contentDefiniteHeight, inFlowContainingBlock, viewport),
            IStackLayoutNode { Orientation: Orientation.Horizontal } stack => LayoutHorizontalStack(stack, contentX, contentY, contentMaxWidth, inFlowContainingBlock, viewport),
            IStackLayoutNode stack => LayoutVerticalChildren(stack, stack.Spacing, contentX, contentY, contentMaxWidth, inFlowContainingBlock, viewport),
            _ => LayoutVerticalChildren(node, 0, contentX, contentY, contentMaxWidth, inFlowContainingBlock, viewport)
        };

        List<LayoutBox> childBoxes = content.Boxes;
        double maxChildWidth = content.Width;
        double contentHeight = content.Height;
        double resolvedBorderHeight = style.Height ?? (contentHeight + padding.Vertical);

        double padHeight = Math.Max(0, resolvedBorderHeight - (style.BorderWidth * 2));
        Rect outOfFlowContainingBlock = style.IsPositioned
            ? new Rect(padX, padY, padWidth, padHeight)
            : containingBlock;

        IReadOnlyList<ILayoutNode> allChildren = node.LayoutChildren;
        for (int i = 0; i < allChildren.Count; i++)
        {
            ILayoutNode child = allChildren[i];
            if (!child.IsVisible || child is IPortalLayoutNode)
            {
                continue;
            }

            ComputedStyle childStyle = child.ComputedStyle;
            if (childStyle.Position is not (Position.Absolute or Position.Fixed))
            {
                continue;
            }

            Rect targetCb = childStyle.Position == Position.Fixed
                ? new Rect(0, 0, viewport.Width, viewport.Height)
                : outOfFlowContainingBlock;

            Thickness childMargin = childStyle.Margin;
            double? explicitW = childStyle.Width?.Pixels;
            double childW;
            double knownContentW = 0;
            bool hasKnownContentW = false;

            if (explicitW.HasValue)
            {
                childW = explicitW.Value;
            }
            else if (childStyle.Left.HasValue && childStyle.Right.HasValue)
            {
                childW = Math.Max(0, targetCb.Width - childStyle.Left.Value - childStyle.Right.Value - childMargin.Horizontal);
            }
            else
            {
                knownContentW = MeasureMaxContentWidth(child);
                hasKnownContentW = true;
                double avail = targetCb.Width - (childStyle.Left ?? 0) - (childStyle.Right ?? 0) - childMargin.Horizontal;
                childW = Math.Min(knownContentW, Math.Max(0, avail));
            }

            double originX;
            if (childStyle.Left.HasValue)
            {
                originX = targetCb.X + childStyle.Left.Value;
            }
            else if (childStyle.Right.HasValue)
            {
                originX = targetCb.Right - childStyle.Right.Value - childMargin.Horizontal - childW;
            }
            else
            {
                originX = targetCb.X;
            }

            double originY;
            if (childStyle.Top.HasValue)
            {
                originY = targetCb.Y + childStyle.Top.Value;
            }
            else
            {
                originY = targetCb.Y;
            }

            double availHeight = childStyle.Height ?? (childStyle.Top.HasValue && childStyle.Bottom.HasValue
                ? Math.Max(0, targetCb.Height - childStyle.Top.Value - childStyle.Bottom.Value - childMargin.Vertical)
                : double.PositiveInfinity);

            Size childAvail = new(childW + childMargin.Horizontal, availHeight);
            LayoutBox childBox = LayoutNodeRecursive(
                child,
                new Point(originX, originY),
                childAvail,
                targetCb,
                viewport,
                hasKnownContentW ? knownContentW : null);

            if (!childStyle.Top.HasValue && childStyle.Bottom.HasValue)
            {
                double targetBorderY = targetCb.Bottom - childStyle.Bottom.Value - childMargin.Bottom - childBox.BorderBox.Height;
                double dy = targetBorderY - childBox.BorderBox.Y;
                if (dy != 0)
                {
                    childBox = childBox.Translated(0, dy);
                }
            }

            childBoxes.Add(childBox);
        }

        if (style.ClipsOverflow && node is IScrollContainer scrollContainer)
        {
            double maxRight = borderWidth;
            double maxBottom = resolvedBorderHeight;
            for (int i = 0; i < childBoxes.Count; i++)
            {
                LayoutBox b = childBoxes[i];
                if (b.Style.Position != Position.Fixed)
                {
                    maxRight = Math.Max(maxRight, b.BorderBox.Right + b.Style.Margin.Right - borderX);
                    maxBottom = Math.Max(maxBottom, b.BorderBox.Bottom + b.Style.Margin.Bottom - borderY);
                }
            }

            Size scrollViewport = new(borderWidth, resolvedBorderHeight);
            Size extent = new(
                Math.Max(borderWidth, Math.Max(maxChildWidth + padding.Horizontal, maxRight)),
                Math.Max(resolvedBorderHeight, Math.Max(contentHeight + padding.Vertical, maxBottom)));
            Point offset = scrollContainer.UpdateScrollMetrics(extent, scrollViewport);
            if (offset != default)
            {
                for (int i = 0; i < childBoxes.Count; i++)
                {
                    if (childBoxes[i].Style.Position != Position.Fixed)
                    {
                        childBoxes[i] = childBoxes[i].Translated(-offset.X, -offset.Y);
                    }
                }
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
