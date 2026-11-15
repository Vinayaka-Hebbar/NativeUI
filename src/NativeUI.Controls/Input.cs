using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// A single-line text field edited by a platform control: <c>NSTextField</c> on macOS and <c>&lt;input&gt;</c> on Web,
/// which own the caret, selection, IME, and clipboard. The platform control presents <see cref="Value"/> and
/// <see cref="Placeholder"/>; the desktop renderer draws only the element's background.
/// </summary>
public class Input : UIElement, IFormControl
{
    // Layout measures one line of the font and never the value, because a long value scrolls instead of wrapping.
    private const string LineMeasureText = "Mg";

    static Input()
    {
        FocusableProperty.OverrideMetadata<Input>(true);
    }

    /// <summary>
    /// Initializes a text input with its initial validity state.
    /// </summary>
    public Input()
    {
        ValidityState.Update(this, this);
    }

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Input, string>(
            nameof(Value),
            defaultValue: string.Empty,
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the <see cref="Placeholder"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> PlaceholderProperty =
        BindableProperty.Create<Input, string?>(
            nameof(Placeholder),
            defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="IsReadOnly"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Create<Input, bool>(
            nameof(IsReadOnly),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the required-value constraint.
    /// </summary>
    public static readonly BindableProperty<bool> RequiredProperty =
        BindableProperty.Create<Input, bool>(
            nameof(Required),
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the localizable required-value error message.
    /// </summary>
    public static readonly BindableProperty<string> RequiredErrorMessageProperty =
        BindableProperty.Create<Input, string>(
            nameof(RequiredErrorMessage),
            defaultValue: "A value is required.",
            propertyChanged: static (owner, _, _) => owner.CheckValidity());
    private string _initialValue = string.Empty;

    /// <summary>
    /// Gets or sets whether a nonempty value is required.
    /// </summary>
    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <summary>
    /// Gets or sets the application-localizable required error message.
    /// </summary>
    public string RequiredErrorMessage
    {
        get => GetValue(RequiredErrorMessageProperty);
        set => SetValue(RequiredErrorMessageProperty, value);
    }

    /// <inheritdoc />
    public bool WillValidate => IsEffectivelyEnabled && !IsReadOnly;

    /// <inheritdoc />
    public ValidityState Validity => new(ValueMissing: Required && !IsReadOnly && string.IsNullOrEmpty(Value));

    /// <inheritdoc />
    public string ValidationMessage
    {
        get
        {
            if (!WillValidate)
            {
                return string.Empty;
            }

            ValidationError? error = Validation.GetErrors(this).FirstOrDefault();
            if (error is not null)
            {
                return error.ErrorMessage;
            }

            return Validity.ValueMissing ? RequiredErrorMessage : string.Empty;
        }
    }

    /// <inheritdoc />
    public bool CheckValidity()
    {
        ValidateBindings();
        ValidityState.Update(this, this);
        return ValidityState.IsEffectivelyValid(this, this);
    }

    /// <inheritdoc />
    public bool ReportValidity()
    {
        ValidityState.MarkUserInteraction(this);
        bool valid = CheckValidity();
        if (!valid)
        {
            Focus();
        }

        return valid;
    }

    /// <inheritdoc />
    protected override void OnEffectiveEnabledChanged(bool isEnabled)
    {
        base.OnEffectiveEnabledChanged(isEnabled);
        ValidityState.Update(this, this);
    }

    /// <inheritdoc />
    public void CaptureInitialValue() => _initialValue = Value;

    /// <inheritdoc />
    public void Reset()
    {
        ValidityState.ResetInteraction(this);
        Value = _initialValue;
        CheckValidity();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        CaptureInitialValue();
    }

    /// <inheritdoc />
    public override string TagName => "input";

    /// <inheritdoc />
    internal override bool AcceptsTextInput => true;

    /// <summary>
    /// Gets or sets the text in the field. The user's edits update this value on each change, including during IME composition.
    /// </summary>
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the hint shown while <see cref="Value"/> is empty.
    /// </summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can select and copy the text but not change it.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <inheritdoc />
    protected override string? GetLayoutTextContent()
    {
        return LineMeasureText;
    }
}
