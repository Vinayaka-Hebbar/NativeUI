namespace NativeUI.Controls;

/// <summary>
/// Specifies the direction in which keyboard focus moves through the navigation order.
/// </summary>
public enum FocusNavigationDirection
{
    /// <summary>
    /// Focus moves to the next element, as the Tab key moves it.
    /// </summary>
    Next,

    /// <summary>
    /// Focus moves to the previous element, as Shift+Tab moves it.
    /// </summary>
    Previous,

    /// <summary>
    /// Focus moves to the first element in the navigation order of the element that moves focus.
    /// </summary>
    First,

    /// <summary>
    /// Focus moves to the last element in the navigation order of the element that moves focus.
    /// </summary>
    Last
}
