namespace NativeUI.Controls;

/// <summary>
/// Specifies the keyboard tab navigation behavior for a container and its children.
/// </summary>
public enum KeyboardNavigationMode
{
    /// <summary>
    /// Tab navigation moves through each focusable element in the container and then leaves the container.
    /// </summary>
    Continue,

    /// <summary>
    /// Tab navigation stays within the container and cycles from the last element back to the first.
    /// </summary>
    Cycle,

    /// <summary>
    /// Tab navigation stays within the container but does not cycle when reaching an edge.
    /// </summary>
    Contained,

    /// <summary>
    /// The container and its children receive focus only once as a single group.
    /// </summary>
    Once,

    /// <summary>
    /// Tab navigation does not navigate into or through the container.
    /// </summary>
    None
}
