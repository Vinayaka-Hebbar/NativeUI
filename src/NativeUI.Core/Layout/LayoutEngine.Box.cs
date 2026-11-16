using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    /// <summary>
    /// Builds the <see cref="LayoutBox"/> of <paramref name="node"/> and its descendants from their retained layout state.
    /// The <paramref name="parentOrigin"/> is the absolute position of the border box of the parent.
    /// </summary>
    private static LayoutBox CreateBox(ILayoutNode node, Point parentOrigin)
    {
        ComputedStyle style = node.ComputedStyle;
        if (!node.IsVisible)
        {
            return new LayoutBox(node, style, Rect.Empty, Rect.Empty, []);
        }

        LayoutState state = GetState(node);
        Rect borderBox = new(parentOrigin.X + state.Bounds.X, parentOrigin.Y + state.Bounds.Y, state.Bounds.Width, state.Bounds.Height);
        Thickness insets = state.ContentInsets;
        ITextLayout? textLayout = state.TextLayout;
        Rect contentBox = new(
            borderBox.X + insets.Left,
            borderBox.Y + insets.Top,
            Math.Max(0, borderBox.Width - insets.Horizontal),
            ResolveContentHeight(state, borderBox, insets));

        if (!LaysOutChildren(node))
        {
            return new LayoutBox(node, style, borderBox, contentBox, [], textLayout, state.Margin);
        }

        List<LayoutBox> boxes = [];
        Point origin = new(borderBox.X, borderBox.Y);
        IReadOnlyList<ILayoutNode> children = node.LayoutChildren;
        for (int i = 0; i < children.Count; i++)
        {
            if (IsInFlow(children[i]))
            {
                boxes.Add(CreateBox(children[i], origin));
            }
        }

        for (int i = 0; i < children.Count; i++)
        {
            if (IsPositionedOutOfFlow(children[i]))
            {
                boxes.Add(CreateBox(children[i], origin));
            }
        }

        return new LayoutBox(node, style, borderBox, contentBox, OrderForPainting(boxes), margin: state.Margin);
    }

    /// <summary>
    /// Returns the content height of a node. The height of the retained text layout applies while the arranged size equals
    /// <see cref="LayoutState.DesiredSize"/>. Otherwise the height is what remains of the border box after the insets.
    /// </summary>
    private static double ResolveContentHeight(LayoutState state, Rect borderBox, Thickness insets)
    {
        if (state.TextLayout is ITextLayout textLayout && state.RenderSize == state.DesiredSize)
        {
            return textLayout.Size.Height;
        }

        return Math.Max(0, borderBox.Height - insets.Vertical);
    }

    /// <summary>
    /// Returns the boxes in CSS paint order: positioned boxes with a negative <c>z-index</c>, then boxes in normal flow,
    /// then the other positioned boxes by ascending <c>z-index</c>. Boxes with equal values keep tree order.
    /// </summary>
    private static List<LayoutBox> OrderForPainting(List<LayoutBox> boxes)
    {
        if (!boxes.Exists(static box => box.Style.IsPositioned))
        {
            return boxes;
        }

        List<LayoutBox> below = [];
        List<LayoutBox> inFlow = [];
        List<LayoutBox> above = [];
        foreach (LayoutBox box in boxes)
        {
            if (!box.Style.IsPositioned)
            {
                inFlow.Add(box);
            }
            else if (box.Style.ZIndex < 0)
            {
                below.Add(box);
            }
            else
            {
                above.Add(box);
            }
        }

        List<LayoutBox> ordered = new(boxes.Count);
        ordered.AddRange(below.OrderBy(static box => box.Style.ZIndex));
        ordered.AddRange(inFlow);
        ordered.AddRange(above.OrderBy(static box => box.Style.ZIndex));
        return ordered;
    }
}
