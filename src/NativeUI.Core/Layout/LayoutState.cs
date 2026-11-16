using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Holds the retained desktop layout results of one <see cref="ILayoutNode"/> between layout passes.
/// </summary>
/// <remarks>
/// The state is owned by its node and allocated by <see cref="LayoutEngine"/> on first use. Web nodes never allocate it.
/// </remarks>
internal sealed class LayoutState
{
    /// <summary>
    /// Gets or sets the inputs of the last measure pass.
    /// </summary>
    public LayoutConstraints PreviousConstraints { get; set; }

    /// <summary>
    /// Gets or sets the border-box width of the node when none of its text wraps, or <see langword="null"/> when it has to be
    /// measured again.
    /// </summary>
    public double? MaxContentWidth { get; set; }

    /// <summary>
    /// Gets or sets the border-box size the node resolved during its last measure pass.
    /// </summary>
    public Size DesiredSize { get; set; }

    /// <summary>
    /// Gets or sets the border-box size the node is arranged at. It equals <see cref="DesiredSize"/> unless the parent
    /// allocates a different size to the node.
    /// </summary>
    public Size RenderSize { get; set; }

    /// <summary>
    /// Gets or sets the margin in logical pixels that the node resolved during its last measure pass.
    /// </summary>
    public Thickness Margin { get; set; }

    /// <summary>
    /// Gets or sets the distance in logical pixels from each edge of the border box to the content box that the node
    /// resolved during its last measure pass.
    /// </summary>
    public Thickness ContentInsets { get; set; }

    /// <summary>
    /// Gets or sets the size that the in-flow children occupy within the content box, as of the last measure pass.
    /// </summary>
    public Size Extent { get; set; }

    /// <summary>
    /// Gets or sets the shaped text of a leaf text node, or <see langword="null"/> for any other node. The state owns the
    /// layout and releases it through <see cref="ReleaseTextLayout"/>.
    /// </summary>
    public ITextLayout? TextLayout { get; set; }

    /// <summary>
    /// Gets or sets the available size that <see cref="TextLayout"/> was shaped for.
    /// </summary>
    public Size TextConstraint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="TextLayout"/> still matches the text and style of the node.
    /// </summary>
    public bool IsTextLayoutValid { get; set; }

    /// <summary>
    /// Disposes <see cref="TextLayout"/> and clears it.
    /// </summary>
    public void ReleaseTextLayout()
    {
        (TextLayout as IDisposable)?.Dispose();
        TextLayout = null;
        TextConstraint = default;
        IsTextLayoutValid = false;
    }

    /// <summary>
    /// Gets or sets the position of the border box relative to the parent border box, before scrolling and sticky
    /// adjustments. Flow layout of the parent assigns it for in-flow children.
    /// </summary>
    public Point VisualOffset { get; set; }

    /// <summary>
    /// Gets or sets the border box of the node relative to the parent border box, as of the last completed arrange pass.
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="DesiredSize"/> is current for the previous measure inputs.
    /// </summary>
    public bool IsMeasureValid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Bounds"/> is current.
    /// </summary>
    public bool IsArrangeValid { get; set; }

    /// <summary>
    /// Marks the measurement invalid. An invalid measurement also invalidates the arrangement.
    /// </summary>
    public void InvalidateMeasure()
    {
        IsMeasureValid = false;
        IsTextLayoutValid = false;
        MaxContentWidth = null;
        InvalidateArrange();
    }

    /// <summary>
    /// Marks the arrangement invalid without discarding the measurement.
    /// </summary>
    public void InvalidateArrange()
    {
        IsArrangeValid = false;
    }
}
