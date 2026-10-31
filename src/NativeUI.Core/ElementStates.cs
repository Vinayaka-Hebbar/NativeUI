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
    Hover = 1
}
