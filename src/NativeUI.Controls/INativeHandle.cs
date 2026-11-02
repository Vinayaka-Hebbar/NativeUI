namespace NativeUI.Controls;

using NativeUI.Layout;

/// <summary>
/// Represents the platform-owned handle that presents a <see cref="UIElement"/> on a native or browser surface.
/// </summary>
public interface INativeHandle : IDisposable
{
    /// <summary>
    /// Called when this handle is attached to a <see cref="UIElement"/>.
    /// </summary>
    /// <param name="element">The UI element attaching this handle.</param>
    void OnAttachView(UIElement element)
    {
    }

    /// <summary>
    /// Called when this handle is detached from a <see cref="UIElement"/>.
    /// </summary>
    /// <param name="element">The UI element detaching this handle.</param>
    void OnDetachView(UIElement element)
    {
    }

    /// <summary>
    /// Creates a size observer for the specified <paramref name="element"/> on demand.
    /// </summary>
    /// <param name="element">The UI element to observe.</param>
    /// <returns>A new <see cref="ResizeObserver"/> instance, or <see langword="null"/> if size observation is not supported.</returns>
    ResizeObserver? CreateSizeObserver(UIElement element) => null;

    /// <summary>
    /// Gets the rendered bounds of this handle in logical coordinates, if available.
    /// </summary>
    /// <returns>The rendered bounds, or <see langword="null"/> when not available.</returns>
    Rect? GetBounds() => null;
}
