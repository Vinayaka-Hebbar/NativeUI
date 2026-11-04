namespace NativeUI.Controls;

/// <summary>
/// Specifies how many items of a <see cref="Selector"/> the user can select.
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// The user cannot select items.
    /// </summary>
    None,

    /// <summary>
    /// At most one item is selected.
    /// </summary>
    Single,

    /// <summary>
    /// Any number of items can be selected. Command or Control toggles an item, and Shift selects a range.
    /// </summary>
    Multiple
}
