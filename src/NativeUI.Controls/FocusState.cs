namespace NativeUI.Controls;

/// <summary>
/// Specifies how an element received keyboard focus, which decides whether its focus is visible.
/// </summary>
public enum FocusState
{
    /// <summary>
    /// The element does not have focus.
    /// </summary>
    Unfocused,

    /// <summary>
    /// A pointer press moved focus to the element. The focus is not visible, except on an element that edits text.
    /// </summary>
    Pointer,

    /// <summary>
    /// A key press, such as Tab, moved focus to the element. The focus is visible.
    /// </summary>
    Keyboard,

    /// <summary>
    /// Code moved focus to the element. The focus is visible when the most recent input was a key press.
    /// </summary>
    Programmatic
}
