using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents an individual radio choice with checked state and activation events.
/// The indicator and icon template parts expose the selection through CSS.
/// </summary>
public class RadioButton : ContentControl, IStackLayoutNode, IFormControl
{
    private const string IndicatorPartName = "indicator";
    private const string IconPartName = "icon";

    static RadioButton()
    {
        WidthProperty.OverrideMetadata<RadioButton>(WidthValue.FitContent);
        CursorProperty.OverrideMetadata<RadioButton>(Cursor.Pointer);
        FocusableProperty.OverrideMetadata<RadioButton>(true);
    }

    /// <summary>
    /// Identifies the <see cref="Click"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<RadioButton, RoutedEventArgs>(nameof(Click), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool?> IsCheckedProperty =
        BindableProperty.Create<RadioButton, bool?>(
            nameof(IsChecked),
            defaultValue: false,
            propertyChanged: static (owner, previous, value) => owner.OnIsCheckedChanged(previous, value));

    /// <summary>
    /// Identifies the event raised when this radio button becomes selected.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CheckedEvent =
        RoutedEvent.Register<RadioButton, RoutedEventArgs>(nameof(Checked), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the event raised when this radio button ceases to be selected.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> UncheckedEvent =
        RoutedEvent.Register<RadioButton, RoutedEventArgs>(nameof(Unchecked), RoutingStrategy.Bubble);

    private readonly IDisposable _gesture;

    /// <summary>
    /// Initializes a radio button with pointer and keyboard activation.
    /// </summary>
    public RadioButton()
    {
        _gesture = ClickGesture.Attach(this, PerformClick);
        UpdateCheckedState();
        ValidityState.Update(this, this);
    }

    /// <inheritdoc />
    public override string TagName => "radiobutton";

    /// <summary>
    /// Gets or sets a value indicating whether the radio button is checked, or <see langword="null"/> when no selection is specified.
    /// </summary>
    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Occurs when this radio button becomes selected.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Checked
    {
        add => AddHandler(CheckedEvent, value);
        remove => RemoveHandler(CheckedEvent, value);
    }

    /// <summary>
    /// Occurs when this radio button ceases to be selected.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Unchecked
    {
        add => AddHandler(UncheckedEvent, value);
        remove => RemoveHandler(UncheckedEvent, value);
    }

    /// <summary>
    /// Identifies the localizable required-value error message.
    /// </summary>
    public static readonly BindableProperty<string> RequiredErrorMessageProperty =
        BindableProperty.Create<RadioButton, string>(
            nameof(RequiredErrorMessage),
            defaultValue: "Select an option.",
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Gets or sets the application-localizable required error message.
    /// </summary>
    public string RequiredErrorMessage
    {
        get => GetValue(RequiredErrorMessageProperty);
        set => SetValue(RequiredErrorMessageProperty, value);
    }

    private bool? _initialValue = false;

    /// <summary>
    /// Identifies whether the checked value is required.
    /// </summary>
    public static readonly BindableProperty<bool> RequiredProperty =
        BindableProperty.Create<RadioButton, bool>(
            nameof(Required),
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Gets or sets whether this choice must be selected.
    /// </summary>
    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <inheritdoc />
    public bool WillValidate => IsEffectivelyEnabled;

    /// <inheritdoc />
    public virtual ValidityState Validity => new()
    {
        ValueMissing = Required && IsChecked != true
    };

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

    /// <summary>
    /// Occurs after pointer, keyboard, label, or programmatic activation changes the checked state.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>
    /// Activates the radio button as a pointer or label does.
    /// </summary>
    public void PerformClick()
    {
        if (!IsDisposed && IsEffectivelyEnabled)
        {
            ValidityState.MarkUserInteraction(this);
            IsChecked = true;
            RaiseEvent(ClickEvent, new RoutedEventArgs());
        }
    }

    /// <inheritdoc />
    void IFormControl.Activate()
    {
        Focus(FocusState.Pointer);
        PerformClick();
    }

    /// <inheritdoc />
    protected override void OnEffectiveEnabledChanged(bool isEnabled)
    {
        base.OnEffectiveEnabledChanged(isEnabled);
        ValidityState.Update(this, this);
    }

    /// <inheritdoc />
    public void CaptureInitialValue() => _initialValue = IsChecked;

    /// <inheritdoc />
    public void Reset()
    {
        ValidityState.ResetInteraction(this);
        IsChecked = _initialValue;
        CheckValidity();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        CaptureInitialValue();
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Horizontal;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        View indicator = DefinePart(new View(), IndicatorPartName);
        indicator.Children.Add(DefinePart(new View(), IconPartName));
        Children.Add(indicator);
        Children.Add(DefinePart(new View(), ContentPartName));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gesture.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnIsCheckedChanged(bool? previous, bool? value)
    {
        UpdateCheckedState();
        ValidityState.Update(this, this);
        if ((previous == true) != (value == true) && IsChecked == value)
        {
            RaiseEvent(value == true ? CheckedEvent : UncheckedEvent, new RoutedEventArgs());
        }
    }

    private void UpdateCheckedState()
    {
        SetState(ElementStates.Checked, IsChecked == true);
        SetState(ElementStates.Unchecked, IsChecked != true);
        InvalidateStyle();
        InvalidateVisual();
    }
}
