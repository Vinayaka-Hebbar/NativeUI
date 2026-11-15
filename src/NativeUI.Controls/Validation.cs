using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Exposes any element's binding/model errors independently of forms and input constraints.
/// </summary>
public static class Validation
{
    /// <summary>
    /// Identifies the observable binding error state, maintained by the binding system.
    /// </summary>
    public static readonly BindableProperty<bool> HasErrorProperty =
        BindableProperty.CreateAttached<bool>("HasError", typeof(Validation));

    /// <summary>
    /// Identifies the observable, read-only binding error snapshot.
    /// </summary>
    public static readonly BindableProperty<IReadOnlyList<ValidationError>> ErrorsProperty =
        BindableProperty.CreateAttached<IReadOnlyList<ValidationError>>("Errors", typeof(Validation), []);

    /// <summary>
    /// Gets whether an element has binding or model errors, including display elements.
    /// </summary>
    public static bool GetHasError(UIElement element) => element.GetValue(HasErrorProperty);

    /// <summary>
    /// Gets the element's binding/model errors; constraint validity belongs to IFormControl.Validity.
    /// </summary>
    public static IReadOnlyList<ValidationError> GetErrors(UIElement element) => element.GetValue(ErrorsProperty);

    internal static void Refresh(UIElement element)
    {
        IReadOnlyList<ValidationError> errors = element.GetBindingErrors();
        if (!GetErrors(element).SequenceEqual(errors))
        {
            element.SetValue(ErrorsProperty, errors);
        }

        element.SetValue(HasErrorProperty, errors.Count != 0);
        if (element is IFormControl control)
        {
            ValidityState.Update(element, control);
        }
    }
}
