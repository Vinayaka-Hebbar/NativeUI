using System.ComponentModel;

namespace NativeUI.Controls;

/// <summary>
/// Provides the submitter and cancellation for a validated submission request.
/// </summary>
public sealed class FormSubmitEventArgs(Button? submitter) : CancelEventArgs
{
    /// <summary>
    /// Gets the initiating submit button, or null for a direct request.
    /// </summary>
    public Button? Submitter { get; } = submitter;
}
