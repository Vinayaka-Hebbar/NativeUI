using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Implemented by a <see cref="INativeHandle"/> whose platform owns scrolling, such as a browser element.
/// </summary>
internal interface IScrollPeer
{
    /// <summary>
    /// Scrolls the platform element, which limits the offset to its scrollable range.
    /// </summary>
    void ScrollTo(double x, double y);

    /// <summary>
    /// Reads the platform element's scroll state, or returns <see langword="null"/> when no platform element exists.
    /// </summary>
    ScrollMetrics? GetScrollMetrics();
}
