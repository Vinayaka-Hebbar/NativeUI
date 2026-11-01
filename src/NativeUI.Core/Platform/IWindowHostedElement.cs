namespace NativeUI.Core;

/// <summary>
/// Indicates an element that can be hosted inside its own native platform window.
/// </summary>
public interface IWindowHostedElement
{
    /// <summary>
    /// Gets whether the element has its own native window.
    /// </summary>
    bool HasOwnWindow { get; }
}
