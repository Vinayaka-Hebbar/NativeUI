namespace NativeUI.Layout;

/// <summary>
/// Describes the scroll state of a container, using the same box as browser <c>scrollHeight</c> and <c>clientHeight</c>.
/// </summary>
/// <param name="Offset">The distance scrolled from the start of the content.</param>
/// <param name="Extent">The size of the content, including padding.</param>
/// <param name="Viewport">The visible size, including padding.</param>
public readonly record struct ScrollMetrics(Point Offset, Size Extent, Size Viewport);

/// <summary>
/// Implemented by layout nodes whose children can scroll when their style clips overflow.
/// </summary>
public interface IScrollContainer : ILayoutNode
{
    /// <summary>
    /// Records the measured sizes and returns the offset to apply, limited to the scrollable range.
    /// Called during layout, so implementations must not invalidate layout or raise events.
    /// </summary>
    Point UpdateScrollMetrics(Size extent, Size viewport);
}
