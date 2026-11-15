using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Describes HTML-style constraints independently of binding/model validation.
/// </summary>
/// <param name="ValueMissing">A required value is absent.</param>
/// <param name="TypeMismatch">The value does not match the expected input type.</param>
/// <param name="PatternMismatch">The value does not match the configured pattern.</param>
/// <param name="TooLong">The value exceeds the maximum length.</param>
/// <param name="TooShort">The value is below the minimum length.</param>
/// <param name="RangeUnderflow">The value is below the minimum.</param>
/// <param name="RangeOverflow">The value exceeds the maximum.</param>
/// <param name="StepMismatch">The value is not aligned with the configured step.</param>
/// <param name="BadInput">The control cannot interpret its edit buffer.</param>
/// <param name="CustomError">The control reports a custom constraint failure.</param>
public readonly record struct ValidityState(
    bool ValueMissing = false,
    bool TypeMismatch = false,
    bool PatternMismatch = false,
    bool TooLong = false,
    bool TooShort = false,
    bool RangeUnderflow = false,
    bool RangeOverflow = false,
    bool StepMismatch = false,
    bool BadInput = false,
    bool CustomError = false)
{
    private const ElementStates Mask = ElementStates.Valid | ElementStates.Invalid | ElementStates.UserInvalid;
    internal static readonly BindableProperty<ElementStates> StateProperty =
        BindableProperty.CreateAttached<ElementStates>("ValidationState", typeof(ValidityState));
    private static readonly BindableProperty<bool> UserInteractedProperty =
        BindableProperty.CreateAttached<bool>("UserInteracted", typeof(ValidityState));

    /// <summary>
    /// Gets whether the control has no constraint failures; binding errors are queried through Validation.
    /// </summary>
    public bool Valid => !ValueMissing && !TypeMismatch && !PatternMismatch && !TooLong && !TooShort &&
        !RangeUnderflow && !RangeOverflow && !StepMismatch && !BadInput && !CustomError;

    internal static bool IsEffectivelyValid(UIElement element, IFormControl control)
    {
        return !control.WillValidate || (control.Validity.Valid && !Validation.GetHasError(element));
    }

    internal static void Update(UIElement element, IFormControl control)
    {
        bool invalid = control.WillValidate && !IsEffectivelyValid(element, control);
        ElementStates next = ElementStates.None;
        if (control.WillValidate)
        {
            next = invalid ? ElementStates.Invalid : ElementStates.Valid;
        }

        if (invalid && element.GetValue(UserInteractedProperty))
        {
            next |= ElementStates.UserInvalid;
        }

        element.SetState(Mask, false);
        element.SetState(next, true);
        if (element.SetValue(StateProperty, next))
        {
            element.InvalidateStyle();
        }
    }

    internal static void MarkUserInteraction(UIElement element)
    {
        element.SetValue(UserInteractedProperty, true);
        if (element is IFormControl control)
        {
            Update(element, control);
        }
    }

    internal static void ResetInteraction(UIElement element)
    {
        element.SetValue(UserInteractedProperty, false);
    }
}
