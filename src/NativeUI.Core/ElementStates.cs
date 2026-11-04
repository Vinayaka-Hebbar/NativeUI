namespace NativeUI.Core;

/// <summary>
/// Identifies the states of an element that pseudo-classes select. Style resolvers read these flags. Platform hosts own
/// the interaction transitions, controls own their value states, and views own the structural states of their children.
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
    Focus = 1 << 3,

    /// <summary>
    /// The element is checked, selected by the <c>:checked</c> pseudo-class.
    /// </summary>
    Checked = 1 << 4,

    /// <summary>
    /// The element can be checked and is not checked, selected by the <c>:unchecked</c> pseudo-class.
    /// </summary>
    Unchecked = 1 << 5,

    /// <summary>
    /// The element is neither checked nor unchecked, selected by the <c>:indeterminate</c> pseudo-class.
    /// </summary>
    Indeterminate = 1 << 6,

    /// <summary>
    /// The element is a toggle that is pressed, selected by the <c>:pressed</c> pseudo-class. Unlike
    /// <see cref="Pressed"/>, this state persists after the pointer is released.
    /// </summary>
    On = 1 << 7,

    /// <summary>
    /// The element is a toggle that is not pressed, selected by the <c>:unpressed</c> pseudo-class.
    /// </summary>
    Off = 1 << 8,

    /// <summary>
    /// The element has focus that the platform indicates visibly, selected by the <c>:focus-visible</c> pseudo-class.
    /// </summary>
    FocusVisible = 1 << 9,

    /// <summary>
    /// The element or one of its descendants has focus, selected by the <c>:focus-within</c> pseudo-class.
    /// </summary>
    FocusWithin = 1 << 10,

    /// <summary>
    /// The element is the first child of its parent, selected by the <c>:first-child</c> pseudo-class.
    /// </summary>
    FirstChild = 1 << 11,

    /// <summary>
    /// The element is the last child of its parent, selected by the <c>:last-child</c> pseudo-class.
    /// </summary>
    LastChild = 1 << 12
}
