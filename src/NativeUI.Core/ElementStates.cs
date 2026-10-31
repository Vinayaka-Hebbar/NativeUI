namespace NativeUI.Core;

/// <summary>
/// Identifies the interaction states of an element. Style resolvers read these flags; platform hosts own the transitions.
/// </summary>
[Flags]
public enum ElementStates
{
    /// <summary>
    /// The element has no active state.
    /// </summary>
    None = 0,

    /// <summary>
    /// The pointer is over the element or one of its descendants.
    /// </summary>
    Hover = 1 << 0,

    /// <summary>
    /// The element is in a pressed/active interaction state.
    /// </summary>
    Pressed = 1 << 1,

    /// <summary>
    /// The element is disabled and does not respond to user interactions.
    /// </summary>
    Disabled = 1 << 2,

    /// <summary>
    /// The element has keyboard or input focus.
    /// </summary>
    Focus = 1 << 3
}
