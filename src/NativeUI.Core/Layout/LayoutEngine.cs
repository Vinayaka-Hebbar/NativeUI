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
    /// Gets the child layout nodes of this element.
    /// </summary>
    IReadOnlyList<ILayoutNode> LayoutChildren { get; }

    /// <summary>
    /// Gets or sets the retained desktop layout results of the element. Only <see cref="LayoutEngine"/> allocates it.
    /// </summary>
    internal LayoutState? LayoutState { get; set; }
}

/// <summary>
/// Calculates CSS box-model geometry (<c>margin</c>, <c>padding</c>, <c>width</c>, <c>height</c>,
/// block/vertical flow, and text measurement) for a tree of <see cref="ILayoutNode"/> elements.
/// </summary>
/// <remarks>
/// Layout runs in two passes. <c>Measure</c> resolves the size of each node and the flow offsets of its children. It does
/// not depend on where the node is placed. <c>Arrange</c> positions out-of-flow children and applies scrolling and sticky
/// offsets. The results are retained in the <see cref="LayoutState"/> of each node.
/// </remarks>
public sealed partial class LayoutEngine
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

        LayoutConstraints constraints = new(new Size(Math.Max(0, viewport.Width), Math.Max(0, viewport.Height)));
        Measure(root, constraints);

        LayoutState state = GetState(root);
        Thickness margin = root.ComputedStyle.Margin;
        state.VisualOffset = new Point(margin.Left, margin.Top);
        state.Bounds = new Rect(state.VisualOffset.X, state.VisualOffset.Y, state.RenderSize.Width, state.RenderSize.Height);
        if (!state.IsArrangeValid)
        {
            Rect viewportBounds = new(0, 0, constraints.Viewport.Width, constraints.Viewport.Height);
            Arrange(root, state.VisualOffset, viewportBounds, constraints.Viewport);
        }

        return CreateBox(root, Point.Zero);
    }

    private static LayoutState GetState(ILayoutNode node)
    {
        return node.LayoutState ??= new LayoutState();
    }

    /// <summary>
    /// Resolves the border-box size of <paramref name="node"/> and the flow offsets of its children, or returns the retained
    /// size when the node and <paramref name="constraints"/> are unchanged since the last pass. The result does not depend
    /// on where the node is placed.
    /// </summary>
    private Size Measure(ILayoutNode node, LayoutConstraints constraints)
    {
        LayoutState state = GetState(node);
        if (state.IsMeasureValid && state.PreviousConstraints == constraints)
        {
            return state.DesiredSize;
        }

        state.IsMeasureValid = false;
        state.InvalidateArrange();
        state.PreviousConstraints = constraints;

        ComputedStyle style = node.ComputedStyle;
        Thickness insets = style.ContentInsets;
        Size availableSize = constraints.AvailableSize;
        double borderWidth = ResolveBorderWidth(node, availableSize.Width, availableSize.Height);
        double contentWidth = Math.Max(0, borderWidth - insets.Horizontal);
        double? specifiedHeight = ResolveSpecifiedHeight(style, availableSize.Height)
            ?? (style.Height.IsAuto && constraints.StretchHeight is double stretchHeight ? ClampHeight(style, stretchHeight, availableSize.Height) : null);

        if (node is IIntrinsicSize intrinsic)
        {
            return MeasureIntrinsic(state, intrinsic, style, availableSize, borderWidth, contentWidth, specifiedHeight);
        }

        if (node is ITextSource { Text: not null } source)
        {
            return MeasureText(state, source, style, borderWidth, contentWidth, availableSize.Height, specifiedHeight);
        }

        double? contentDefiniteHeight = specifiedHeight is double height ? Math.Max(0, height - insets.Vertical) : null;
        double borderInset = style.BorderWidth * 2;
        double paddingBoxWidth = Math.Max(0, borderWidth - borderInset);
        Point contentOrigin = new(insets.Left, insets.Top);
        LayoutConstraints childConstraints = constraints with
        {
            AvailableSize = new Size(contentWidth, contentDefiniteHeight ?? double.PositiveInfinity),
            ContainingBlock = style.IsPositioned ? new Size(paddingBoxWidth, 0) : constraints.ContainingBlock,
            StretchHeight = null
        };

        Size content = node switch
        {
            IGridLayoutNode grid => MeasureGrid(grid, contentOrigin, contentWidth, contentDefiniteHeight, childConstraints),
            IResizableLayoutNode resizable => MeasureResizable(resizable, contentOrigin, contentWidth, contentDefiniteHeight, childConstraints),
            IStackLayoutNode { Orientation: Orientation.Horizontal } stack => MeasureHorizontalStack(stack, contentOrigin, contentWidth, childConstraints),
            IStackLayoutNode stack => MeasureBlock(stack, GetStackSpacing(stack), contentOrigin, contentWidth, childConstraints),
            _ => MeasureBlock(node, 0, contentOrigin, contentWidth, childConstraints)
        };

        double borderHeight = specifiedHeight ?? ClampHeight(style, content.Height + insets.Vertical, availableSize.Height);
        Size outOfFlowContainingBlock = style.IsPositioned
            ? new Size(paddingBoxWidth, Math.Max(0, borderHeight - borderInset))
            : constraints.ContainingBlock;
        MeasureOutOfFlowChildren(node, childConstraints with { ContainingBlock = outOfFlowContainingBlock });

        state.Extent = new Size(content.Width, content.Height);
        state.ReleaseTextLayout();
        return Complete(state, new Size(borderWidth, borderHeight));
    }

    /// <summary>
    /// Records the result of a measure pass in <paramref name="state"/>.
    /// </summary>
    private static Size Complete(LayoutState state, Size size)
    {
        state.DesiredSize = size;
        state.RenderSize = size;
        state.IsMeasureValid = true;
        return size;
    }

    /// <summary>
    /// Positions out-of-flow children, applies scrolling and sticky offsets, and arranges all children. The
    /// <paramref name="origin"/> is the absolute position of the border box of <paramref name="node"/>, and
    /// <paramref name="containingBlock"/> is the absolute rectangle that non-positioned descendants inherit.
    /// </summary>
    private void Arrange(ILayoutNode node, Point origin, Rect containingBlock, Size viewport)
    {
        LayoutState state = GetState(node);
        state.IsArrangeValid = true;
        if (!LaysOutChildren(node))
        {
            return;
        }

        ComputedStyle style = node.ComputedStyle;
        Size size = state.DesiredSize;
        double borderWidth = style.BorderWidth;
        Rect paddingBox = new(
            origin.X + borderWidth,
            origin.Y + borderWidth,
            Math.Max(0, size.Width - (borderWidth * 2)),
            Math.Max(0, size.Height - (borderWidth * 2)));
        Rect outOfFlowContainingBlock = style.IsPositioned ? paddingBox : containingBlock;

        List<ILayoutNode> flowChildren = [];
        List<ILayoutNode> outOfFlowChildren = [];
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ILayoutNode child = children[i];
            if (IsInFlow(child))
            {
                LayoutState childState = GetState(child);
                childState.Bounds = new Rect(childState.VisualOffset.X, childState.VisualOffset.Y, childState.RenderSize.Width, childState.RenderSize.Height);
                flowChildren.Add(child);
            }
            else if (IsPositionedOutOfFlow(child))
            {
                ArrangeOutOfFlowChild(child, GetContainingBlock(child, outOfFlowContainingBlock, viewport), origin);
                outOfFlowChildren.Add(child);
            }
        }

        Point scrollOffset = ArrangeScrollContainer(node, state, flowChildren, outOfFlowChildren);
        Rect inFlowContainingBlock = style.IsPositioned
            ? new Rect(paddingBox.X - scrollOffset.X, paddingBox.Y - scrollOffset.Y, paddingBox.Width, 0)
            : containingBlock;

        for (int i = 0; i < flowChildren.Count; i++)
        {
            ArrangeChild(flowChildren[i], origin, inFlowContainingBlock, viewport);
        }

        for (int i = 0; i < outOfFlowChildren.Count; i++)
        {
            ILayoutNode child = outOfFlowChildren[i];
            ArrangeChild(child, origin, GetContainingBlock(child, outOfFlowContainingBlock, viewport), viewport);
        }
    }

    private void ArrangeChild(ILayoutNode child, Point parentOrigin, Rect containingBlock, Size viewport)
    {
        Rect bounds = GetState(child).Bounds;
        Arrange(child, new Point(parentOrigin.X + bounds.X, parentOrigin.Y + bounds.Y), containingBlock, viewport);
    }

    /// <summary>
    /// Returns the border-box width of <paramref name="node"/> within <paramref name="availableWidth"/>, clamped to its
    /// <see cref="ComputedStyle.MinWidth"/> and <see cref="ComputedStyle.MaxWidth"/>.
    /// </summary>
    private double ResolveBorderWidth(ILayoutNode node, double availableWidth, double availableHeight)
    {
        ComputedStyle style = node.ComputedStyle;
        double fillWidth = Math.Max(0, availableWidth - style.Margin.Horizontal);
        double width;
        if (style.Width.IsAuto && node is IIntrinsicSize intrinsic)
        {
            Size natural = intrinsic.GetIntrinsicSize(new Size(fillWidth, double.PositiveInfinity));
            double naturalWidth = ResolveHeight(style.Height, availableHeight) is double h && natural.Height > 0 ? Math.Max(0, h - style.ContentInsets.Vertical) * natural.Width / natural.Height : natural.Width;
            width = Math.Min(fillWidth, naturalWidth + style.ContentInsets.Horizontal);
        }
        else
        {
            width = style.Width switch
            {
                { IsPixels: true } pixels => pixels.Value,
                { IsPercent: true } percent => Math.Max(0, availableWidth * percent.Value / 100.0),
                { IsFitContent: true } => Math.Min(MeasureMaxContentWidth(node), fillWidth),
                _ => (style.IsOutOfFlow && !(style.Left.HasValue && style.Right.HasValue))
                    ? Math.Min(MeasureMaxContentWidth(node), fillWidth)
                    : fillWidth
            };
        }

        return ClampWidth(style, width, availableWidth);
    }

    /// <summary>
    /// Clamps a border-box <paramref name="width"/> to the minimum and maximum width of <paramref name="style"/>. A
    /// percentage resolves against <paramref name="containingWidth"/> and is ignored when that width is not finite. The
    /// minimum wins over the maximum.
    /// </summary>
    private static double ClampWidth(ComputedStyle style, double width, double containingWidth)
    {
        if (style.MaxWidth is WidthValue maxWidth && ResolveWidthLimit(maxWidth, containingWidth) is double max)
        {
            width = Math.Min(width, max);
        }

        if (style.MinWidth is WidthValue minWidth && ResolveWidthLimit(minWidth, containingWidth) is double min)
        {
            width = Math.Max(width, min);
        }

        return width;
    }

    private static double? ResolveWidthLimit(WidthValue limit, double containingWidth)
    {
        return limit switch
        {
            { IsPixels: true } pixels => pixels.Value,
            { IsPercent: true } percent when double.IsFinite(containingWidth) => Math.Max(0, containingWidth * percent.Value / 100.0),
            _ => null
        };
    }

    /// <summary>
    /// Clamps a border-box <paramref name="height"/> to the minimum and maximum height of <paramref name="style"/>. A
    /// percentage resolves against <paramref name="containingHeight"/> and is ignored when that height is not finite. The
    /// minimum wins over the maximum.
    /// </summary>
    private static double ClampHeight(ComputedStyle style, double height, double containingHeight)
    {
        if (style.MaxHeight is HeightValue maxHeight && ResolveHeight(maxHeight, containingHeight) is double max)
        {
            height = Math.Min(height, max);
        }

        if (style.MinHeight is HeightValue minHeight && ResolveHeight(minHeight, containingHeight) is double min)
        {
            height = Math.Max(height, min);
        }

        return height;
    }

    /// <summary>
    /// Returns the border-box height that does not depend on content, or null when content determines it. As in CSS, an
    /// out-of-flow element with both vertical offsets and no height fills <paramref name="availableHeight"/> between them.
    /// </summary>
    private static double? ResolveSpecifiedHeight(ComputedStyle style, double availableHeight)
    {
        if (ResolveHeight(style.Height, availableHeight) is double height)
        {
            return ClampHeight(style, height, availableHeight);
        }

        return style.IsOutOfFlow && style.Top.HasValue && style.Bottom.HasValue ? ClampHeight(style, availableHeight, availableHeight) : null;
    }

    /// <summary>
    /// Returns the border-box height that <paramref name="value"/> specifies: its length in pixels, or its percentage of
    /// <paramref name="availableHeight"/> when that height is definite. Returns null when content determines the height,
    /// which includes a percentage of an indefinite height.
    /// </summary>
    private static double? ResolveHeight(HeightValue value, double availableHeight)
    {
        return value switch
        {
            { IsPixels: true } pixels => pixels.Value,
            { IsPercent: true } percent when double.IsFinite(availableHeight) => Math.Max(0, availableHeight * percent.Value / 100.0),
            _ => null
        };
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> takes part in the normal flow of its parent.
    /// </summary>
    private static bool IsInFlow(ILayoutNode node)
    {
        return node.IsVisible && !node.ComputedStyle.IsOutOfFlow;
    }

    /// <summary>
    /// Returns whether the parent of <paramref name="node"/> positions it against a containing block. Portals are excluded
    /// because the host lays each one out as its own root.
    /// </summary>
    private static bool IsPositionedOutOfFlow(ILayoutNode node)
    {
        return node.IsVisible && node is not IPortalLayoutNode && node.ComputedStyle.IsOutOfFlow;
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> lays out child nodes, which leaf text and intrinsically sized nodes do not.
    /// </summary>
    private static bool LaysOutChildren(ILayoutNode node)
    {
        return node is not IIntrinsicSize && node is not ITextSource { Text: not null };
    }

}
