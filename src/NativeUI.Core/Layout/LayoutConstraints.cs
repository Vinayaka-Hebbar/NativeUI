using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// The inputs of a measure pass of <see cref="LayoutEngine"/>. A node whose inputs equal those of its last pass keeps its
/// retained size.
/// </summary>
/// <param name="AvailableSize">The size that the parent offers to the node.</param>
/// <param name="ContainingBlock">The size that out-of-flow descendants resolve percentages and offsets against.</param>
/// <param name="Viewport">The size of the viewport, which fixed descendants resolve against.</param>
/// <param name="StretchHeight">
/// The height that the parent allocates to a node whose own height is <see cref="HeightValue.Auto"/>, or
/// <see langword="null"/> when the node sizes to its content.
/// </param>
internal readonly record struct LayoutConstraints(
    Size AvailableSize,
    Size ContainingBlock,
    Size Viewport,
    double? StretchHeight = null)
{
    /// <summary>
    /// Initializes the constraints of a root node that fills <paramref name="viewport"/>.
    /// </summary>
    public LayoutConstraints(Size viewport)
        : this(viewport, viewport, viewport)
    {
    }
}
