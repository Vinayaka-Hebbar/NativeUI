namespace NativeUI.Core;

/// <summary>
/// Identifies a logical key, which is the meaning of a key press in the current keyboard layout. Names follow the values
/// of the DOM <c>KeyboardEvent.key</c> property. A key that produces text and has no name here is <see cref="Character"/>,
/// and <see cref="KeyEventArgs.Text"/> holds its text. Physical key positions are not represented.
/// </summary>
public enum Key
{
    /// <summary>
    /// The key is unknown.
    /// </summary>
    None,

    /// <summary>
    /// A key that produces the text in <see cref="KeyEventArgs.Text"/>.
    /// </summary>
    Character,

    /// <summary>
    /// The Tab key.
    /// </summary>
    Tab,

    /// <summary>
    /// The Enter or Return key, including the keypad Enter key.
    /// </summary>
    Enter,

    /// <summary>
    /// The Space bar.
    /// </summary>
    Space,

    /// <summary>
    /// The Escape key.
    /// </summary>
    Escape,

    /// <summary>
    /// The Backspace key, labelled Delete on Mac keyboards.
    /// </summary>
    Backspace,

    /// <summary>
    /// The forward Delete key.
    /// </summary>
    Delete,

    /// <summary>
    /// The Insert key.
    /// </summary>
    Insert,

    /// <summary>
    /// The Left Arrow key.
    /// </summary>
    ArrowLeft,

    /// <summary>
    /// The Up Arrow key.
    /// </summary>
    ArrowUp,

    /// <summary>
    /// The Right Arrow key.
    /// </summary>
    ArrowRight,

    /// <summary>
    /// The Down Arrow key.
    /// </summary>
    ArrowDown,

    /// <summary>
    /// The Home key.
    /// </summary>
    Home,

    /// <summary>
    /// The End key.
    /// </summary>
    End,

    /// <summary>
    /// The Page Up key.
    /// </summary>
    PageUp,

    /// <summary>
    /// The Page Down key.
    /// </summary>
    PageDown,

    /// <summary>
    /// The Context Menu key.
    /// </summary>
    ContextMenu,

    /// <summary>
    /// The F1 function key.
    /// </summary>
    F1,

    /// <summary>
    /// The F2 function key.
    /// </summary>
    F2,

    /// <summary>
    /// The F3 function key.
    /// </summary>
    F3,

    /// <summary>
    /// The F4 function key.
    /// </summary>
    F4,

    /// <summary>
    /// The F5 function key.
    /// </summary>
    F5,

    /// <summary>
    /// The F6 function key.
    /// </summary>
    F6,

    /// <summary>
    /// The F7 function key.
    /// </summary>
    F7,

    /// <summary>
    /// The F8 function key.
    /// </summary>
    F8,

    /// <summary>
    /// The F9 function key.
    /// </summary>
    F9,

    /// <summary>
    /// The F10 function key.
    /// </summary>
    F10,

    /// <summary>
    /// The F11 function key.
    /// </summary>
    F11,

    /// <summary>
    /// The F12 function key.
    /// </summary>
    F12
}
