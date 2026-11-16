using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// The inputs of a measure pass of <see cref="LayoutEngine"/>. A node whose inputs equal those of its last pass keeps its
/// retained size.
/// </summary>
/// <param name="AvailableSize">The size that the parent offers to the node.</param>
/// <param name="ContainingBlock">The size that out-of-flow descendants resolve percentages and offsets against.</param>
/// <param name="Viewport">The size of the viewport, which fixed descendants resolve against.</param>
/// <param name="IsFixedWidth">
/// Whether the parent dictates the width of a node whose own width is <see cref="WidthValue.Auto"/>, which then fills the
/// width of <paramref name="AvailableSize"/> less its margin. Otherwise the node sizes to its content within that width.
/// </param>
/// <param name="IsFixedHeight">
/// Whether the parent dictates the height of a node whose own height is <see cref="HeightValue.Auto"/>, which then equals
/// the height of <paramref name="AvailableSize"/> and is definite for the percentage heights of its descendants.
/// </param>
internal readonly record struct LayoutConstraints(
    Size AvailableSize,
    Size ContainingBlock,
    Size Viewport,
    bool IsFixedWidth,
    bool IsFixedHeight)
{
    /// <summary>
    /// Initializes the constraints of a root node that fills <paramref name="viewport"/>.
    /// </summary>
    public LayoutConstraints(Size viewport)
        : this(viewport, viewport, viewport, true, false)
    {
    }

    /// <summary>
    /// Returns the width of the content box of a node whose children occupy <paramref name="extentWidth"/>. It is the width
    /// of <see cref="AvailableSize"/> when that width is definite and larger, and the extent otherwise.
    /// </summary>
    /// <param name="extentWidth">The width that the in-flow children occupy.</param>
    public double GetContentWidth(double extentWidth)
    {
        return double.IsFinite(AvailableSize.Width) ? Math.Max(extentWidth, AvailableSize.Width) : extentWidth;
    }

    /// <summary>
    /// Returns the height of the content box of a node whose children occupy <paramref name="extentHeight"/>. It is the
    /// height of <see cref="AvailableSize"/> when that height is definite and larger, and the extent otherwise.
    /// </summary>
    /// <param name="extentHeight">The height that the in-flow children occupy.</param>
    public double GetContentHeight(double extentHeight)
    {
        return double.IsFinite(AvailableSize.Height) ? Math.Max(extentHeight, AvailableSize.Height) : extentHeight;
    }
}
