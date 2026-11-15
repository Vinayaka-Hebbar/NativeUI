using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Defines participation in form validation, activation, and UI-value reset.
/// </summary>
public interface IFormControl
{
    /// <summary>
    /// Gets whether the control currently participates in validation.
    /// </summary>
    bool WillValidate { get; }

    /// <summary>
    /// Gets HTML-style constraint validity, independently of binding errors.
    /// </summary>
    ValidityState Validity { get; }

    /// <summary>
    /// Gets the first effective binding or constraint validation message.
    /// </summary>
    string ValidationMessage { get; }

    /// <summary>
    /// Checks effective validity, combining constraints and binding errors.
    /// </summary>
    bool CheckValidity();

    /// <summary>
    /// Checks validity and presents invalid state and focus when appropriate.
    /// </summary>
    bool ReportValidity();

    /// <summary>
    /// Restores the captured control value through normal bindings.
    /// </summary>
    void Reset();

    /// <summary>
    /// Captures initial UI state when joining a form; custom controls can manage their own baseline.
    /// </summary>
    void CaptureInitialValue()
    {
    }

    /// <summary>
    /// Performs label activation; custom controls can override focus-only activation.
    /// </summary>
    void Activate()
    {
        if (this is UIElement element)
        {
            element.Focus(FocusState.Pointer);
        }
    }
}
