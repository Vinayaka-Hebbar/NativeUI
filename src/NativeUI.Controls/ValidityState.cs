using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Describes HTML-style constraints independently of binding/model validation.
/// </summary>
public readonly record struct ValidityState
{
    /// <summary>
    /// Represents a state with no constraint failures.
    /// </summary>
    public static readonly ValidityState Empty = default;

    /// <summary>
    /// Gets whether a required value is absent.
    /// </summary>
    public bool ValueMissing { get; init; }

    /// <summary>
    /// Gets whether the value does not match the expected input type.
    /// </summary>
    public bool TypeMismatch { get; init; }

    /// <summary>
    /// Gets whether the value does not match the configured pattern.
    /// </summary>
    public bool PatternMismatch { get; init; }

    /// <summary>
    /// Gets whether the value exceeds the maximum length.
    /// </summary>
    public bool TooLong { get; init; }

    /// <summary>
    /// Gets whether the value is below the minimum length.
    /// </summary>
    public bool TooShort { get; init; }

    /// <summary>
    /// Gets whether the value is below the minimum.
    /// </summary>
    public bool RangeUnderflow { get; init; }

    /// <summary>
    /// Gets whether the value exceeds the maximum.
    /// </summary>
    public bool RangeOverflow { get; init; }

    /// <summary>
    /// Gets whether the value is not aligned with the configured step.
    /// </summary>
    public bool StepMismatch { get; init; }

    /// <summary>
    /// Gets whether the control cannot interpret its edit buffer.
    /// </summary>
    public bool BadInput { get; init; }

    /// <summary>
    /// Gets whether the control reports a custom constraint failure.
    /// </summary>
    public bool CustomError { get; init; }

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
