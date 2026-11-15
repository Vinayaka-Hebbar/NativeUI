namespace NativeUI.Controls;

/// <summary>
/// Implemented by an <see cref="INativeHandle"/> whose platform element owns keyboard focus. The platform reports each
/// focus change back to the <see cref="KeyboardDevice"/>.
/// </summary>
internal interface IFocusPeer
{
    /// <summary>
    /// Moves keyboard focus to the platform element. Does nothing when no platform element exists.
    /// </summary>
    void Focus();

    /// <summary>
    /// Removes keyboard focus from the platform element. Does nothing when the element does not have focus.
    /// </summary>
    void Blur();
}
