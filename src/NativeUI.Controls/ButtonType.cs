namespace NativeUI.Controls;

/// <summary>
/// Specifies a button's action within a form.
/// </summary>
public enum ButtonType
{
    /// <summary>
    /// Performs ordinary button activation without submitting.
    /// </summary>
    Button,

    /// <summary>
    /// Requests validated submission of the containing form.
    /// </summary>
    Submit,

    /// <summary>
    /// Resets the containing form's control values.
    /// </summary>
    Reset
}
