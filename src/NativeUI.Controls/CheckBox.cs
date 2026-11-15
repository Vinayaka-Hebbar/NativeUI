using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using Path = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls;

/// <summary>
/// Represents a check box that the user checks and unchecks by clicking it, followed by its <see cref="ContentControl.Content"/>
/// in a row. The template has an <c>indicator</c> part that contains an <c>icon</c> part. The icon draws a check mark while
/// the check box is checked, a dash while it is indeterminate, and nothing while it is unchecked, in the CSS <c>color</c>
/// of the icon. Neither part has an appearance until a stylesheet gives it one. The check box is in exactly one of the
/// <see cref="ElementStates.Checked"/>, <see cref="ElementStates.Unchecked"/>, and <see cref="ElementStates.Indeterminate"/>
/// states, selected by <c>:checked</c>, <c>:unchecked</c>, and <c>:indeterminate</c>.
/// </summary>
public class CheckBox : ContentControl, IStackLayoutNode, IFormControl
{
    private const string IndicatorPartName = "indicator";
    private const string IconPartName = "icon";

    static CheckBox()
    {
        WidthProperty.OverrideMetadata<CheckBox>(WidthValue.FitContent);
        CursorProperty.OverrideMetadata<CheckBox>(Cursor.Pointer);
        FocusableProperty.OverrideMetadata<CheckBox>(true);
    }

    /// <summary>
    /// Identifies the <see cref="Click"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<CheckBox, RoutedEventArgs>(nameof(Click), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool?> IsCheckedProperty =
        BindableProperty.Create<CheckBox, bool?>(
            nameof(IsChecked),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateCheckedState());

    /// <summary>
    /// Identifies the <see cref="IsThreeState"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsThreeStateProperty =
        BindableProperty.Create<CheckBox, bool>(
            nameof(IsThreeState),
            defaultValue: false);

    private readonly Path? _icon;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBox"/> class.
    /// </summary>
    public CheckBox()
    {
        _icon = GetTemplateChild<Path>(IconPartName);
        ClickGesture.Attach(this, PerformClick);
        UpdateCheckedState();
    }

    /// <inheritdoc />
    public override string TagName => "checkbox";

    /// <summary>
    /// Gets or sets a value indicating whether the check box is checked, or <see langword="null"/> when it is indeterminate.
    /// </summary>
    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a click moves a checked check box to the indeterminate state instead of
    /// unchecking it. Code can set <see cref="IsChecked"/> to <see langword="null"/> whatever this value is.
    /// </summary>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    /// <summary>
    /// Identifies the localizable required-value error message.
    /// </summary>
    public static readonly BindableProperty<string> RequiredErrorMessageProperty =
        BindableProperty.Create<CheckBox, string>(
            nameof(RequiredErrorMessage),
            defaultValue: "This option is required.",
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
        BindableProperty.Create<CheckBox, bool>(
            nameof(Required),
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Gets or sets whether the control must be checked.
    /// </summary>
    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <inheritdoc />
    public bool WillValidate => IsEffectivelyEnabled;

    /// <inheritdoc />
    public ValidityState Validity => new()
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
    /// Activates the check box as a pointer or label does.
    /// </summary>
    public void PerformClick()
    {
        if (!IsDisposed && IsEffectivelyEnabled)
        {
            ValidityState.MarkUserInteraction(this);
            Advance();
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
        View indicator = DefinePart(IndicatorPartName, new View());
        indicator.Children.Add(DefinePart(IconPartName, new Path { Fill = SvgCurrentColorBrush.Instance, Stretch = Stretch.None }));
        Children.Add(indicator);
    }

    /// <summary>
    /// Moves an unchecked check box to checked. Moves a checked check box to indeterminate when <see cref="IsThreeState"/>
    /// is <see langword="true"/>, and to unchecked otherwise. Moves an indeterminate check box to unchecked.
    /// </summary>
    private void Advance()
    {
        IsChecked = IsChecked switch
        {
            false => true,
            true when IsThreeState => null,
            _ => false
        };
    }

    private void UpdateCheckedState()
    {
        bool? isChecked = IsChecked;
        SetState(ElementStates.Checked, isChecked == true);
        SetState(ElementStates.Unchecked, isChecked == false);
        SetState(ElementStates.Indeterminate, isChecked is null);
        if (_icon is not null)
        {
            _icon.Data = isChecked switch
            {
                true => Icons.Checkmark,
                null => Icons.Dash,
                false => null
            };
        }

        ValidityState.Update(this, this);
        InvalidateStyle();
        InvalidateVisual();
    }
}
