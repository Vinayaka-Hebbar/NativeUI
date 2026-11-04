namespace NativeUI.Controls;

/// <summary>
/// Specifies how many toggles of a <see cref="ToggleGroup"/> can be pressed at the same time.
/// </summary>
public enum ToggleSelectionMode
{
    /// <summary>
    /// At most one toggle is pressed. Pressing a toggle releases the toggle that was pressed before it.
    /// </summary>
    Single,

    /// <summary>
    /// Any number of toggles can be pressed.
    /// </summary>
    Multiple
}
