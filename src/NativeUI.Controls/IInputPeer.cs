namespace NativeUI.Controls;

/// <summary>
/// Implemented by the <see cref="INativeHandle"/> of an <see cref="Input"/>, whose platform control owns editing and focus.
/// </summary>
internal interface IInputPeer
{
    /// <summary>
    /// Moves keyboard focus to the platform control. Does nothing when no platform control exists.
    /// </summary>
    void Focus();
}
