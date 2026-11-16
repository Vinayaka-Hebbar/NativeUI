namespace NativeUI.Layout;

/// <summary>
/// The inputs of a measure pass of <see cref="LayoutEngine"/>. A node whose inputs equal those of its last pass keeps its
/// retained size.
/// </summary>
/// <param name="AvailableSize">The size that the parent offers to the node.</param>
/// <param name="ContainingBlock">The size that out-of-flow descendants resolve percentages and offsets against.</param>
/// <param name="Viewport">The size of the viewport, which fixed descendants resolve against.</param>
internal readonly record struct LayoutConstraints(Size AvailableSize, Size ContainingBlock, Size Viewport)
{
    /// <summary>
    /// Initializes the constraints of a root node that fills <paramref name="viewport"/>.
    /// </summary>
    public LayoutConstraints(Size viewport)
        : this(viewport, viewport, viewport)
    {
    }
}
