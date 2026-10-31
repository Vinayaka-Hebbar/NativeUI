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

    private LayoutBox LayoutNodeRecursive(ILayoutNode node, double availableX, double availableY, double availableWidth)
    {
        ComputedStyle style = node.ComputedStyle;
        Thickness margin = style.Margin;
        Thickness padding = style.Padding;

        double borderX = availableX + margin.Left;
        double borderY = availableY + margin.Top;
        double borderWidth = style.Width ?? Math.Max(0, availableWidth - margin.Horizontal);
        double contentX = borderX + padding.Left;
        double contentY = borderY + padding.Top;
        double contentMaxWidth = Math.Max(0, borderWidth - padding.Horizontal);

        List<LayoutBox> childBoxes = [];
        double currentY = contentY;
        double maxChildWidth = 0;

        if (node.TextContent is string text)
        {
            Size availableTextSize = new(contentMaxWidth, style.Height ?? double.PositiveInfinity);
            ITextLayout textLayout = _textMeasurer.CreateTextLayout(text, availableTextSize, style);
            Size measured = textLayout.Size;

            double textWidth = style.Width.HasValue ? contentMaxWidth : Math.Min(contentMaxWidth, measured.Width);
            double textHeight = measured.Height;
            double finalBorderWidth = style.Width ?? Math.Max(textWidth + padding.Horizontal, contentMaxWidth);
            double finalBorderHeight = style.Height ?? (textHeight + padding.Vertical);

            return new LayoutBox(
                node,
                style,
                new Rect(borderX, borderY, finalBorderWidth, finalBorderHeight),
                new Rect(contentX, contentY, Math.Max(0, finalBorderWidth - padding.Horizontal), textHeight),
                childBoxes,
                textLayout);
        }

        foreach (ILayoutNode child in node.LayoutChildren)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            LayoutBox childBox = LayoutNodeRecursive(child, contentX, currentY, contentMaxWidth);
            childBoxes.Add(childBox);

            double childTotalHeight = childBox.BorderBox.Height + child.ComputedStyle.Margin.Vertical;
            currentY += childTotalHeight;
            maxChildWidth = Math.Max(maxChildWidth, childBox.BorderBox.Width + child.ComputedStyle.Margin.Horizontal);
        }

        double contentHeight = Math.Max(0, currentY - contentY);
        double resolvedBorderHeight = style.Height ?? (contentHeight + padding.Vertical);

        return new LayoutBox(
            node,
            style,
            new Rect(borderX, borderY, borderWidth, resolvedBorderHeight),
            new Rect(contentX, contentY, contentMaxWidth, contentHeight),
            childBoxes);
    }
}
