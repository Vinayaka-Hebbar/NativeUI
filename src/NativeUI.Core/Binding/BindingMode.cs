namespace NativeUI.Core;

/// <summary>
/// Defines the direction and timing of data flow for a property binding.
/// </summary>
public enum BindingMode
{
    /// <summary>
    /// Updates the target control property whenever the source ViewModel property changes.
    /// </summary>
    OneWay,

    /// <summary>
    /// Updates both the target control property and the source ViewModel property when either changes.
    /// </summary>
    TwoWay,

    /// <summary>
    /// Initializes the target control property once when the binding context is attached without subscribing to changes.
    /// </summary>
    OneTime
}
