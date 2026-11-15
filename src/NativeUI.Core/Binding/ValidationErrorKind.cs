namespace NativeUI.Core;

/// <summary>
/// Identifies the origin of a binding-associated validation error.
/// </summary>
public enum ValidationErrorKind
{

    /// <summary>
    /// A ViewModel/provider reports an invalid business value.
    /// </summary>
    Data,

    /// <summary>
    /// A binding cannot convert or commit the proposed input value.
    /// </summary>
    Conversion
}
