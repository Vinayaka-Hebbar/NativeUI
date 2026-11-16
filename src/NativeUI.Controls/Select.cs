using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using Path = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls;

/// <summary>
/// Represents a control that shows the selected item in a trigger and lets the user pick another item from a drop-down
/// list. The drop-down is a <see cref="Popover"/> anchored to the trigger, so it closes on an outside press and on Escape.
/// </summary>
/// <remarks>
/// <para>
/// The template has a <c>trigger</c> part that shows the <c>value</c> part and an <c>icon</c> part, a <c>popup</c> part
/// that is the popover, and the <c>items</c> part inside it that hosts the item elements. Each item element is hosted by a
/// <see cref="ListItem"/>, the <c>item</c> part, as in <see cref="List"/>. The control has <c>data-open</c> or
/// <c>data-closed</c> with the drop-down, and <c>data-placeholder</c> while no item is selected.
/// </para>
/// <para>
/// A click on the trigger, Enter, Space, F4, or Alt with the Down arrow opens the drop-down. A click on an item selects
/// it and closes the drop-down. While the drop-down is closed the Up and Down arrows, Home, and End select an adjacent,
/// first, or last item. While it is open, Enter, Space, and Escape close it.
/// </para>
/// </remarks>
public class Select : Selector, IFormControl
{
    private const string TriggerPartName = "trigger";
    private const string ValuePartName = "value";
    private const string IconPartName = "icon";
    private const string PopupPartName = "popup";
    private const string ItemPartName = "item";

    /// <summary>
    /// Identifies the <see cref="IsDropDownOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsDropDownOpenProperty =
        BindableProperty.Create<Select, bool>(
            nameof(IsDropDownOpen),
            defaultValue: false,
            propertyChanged: static (owner, _, isOpen) => owner.OnIsDropDownOpenChanged(isOpen));

    /// <summary>
    /// Identifies the <see cref="Placeholder"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> PlaceholderProperty =
        BindableProperty.Create<Select, string?>(
            nameof(Placeholder),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateValue());

    /// <summary>
    /// Identifies the <see cref="Required"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> RequiredProperty =
        BindableProperty.Create<Select, bool>(
            nameof(Required),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the <see cref="RequiredErrorMessage"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> RequiredErrorMessageProperty =
        BindableProperty.Create<Select, string>(
            nameof(RequiredErrorMessage),
            defaultValue: "Select an item.",
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    private StackPanel _trigger = null!;
    private View _value = null!;
    private View _items = null!;
    private Popover _popover = null!;
    private UIElement? _valueElement;
    private object? _initialItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="Select"/> class.
    /// </summary>
    public Select()
    {
        PointerPressed += OnItemPointerPressed;
        SelectionChanged += OnSelectionChanged;
        UpdateValue();
    }

    /// <summary>
    /// Occurs after the drop-down opens.
    /// </summary>
    public event EventHandler? DropDownOpened;

    /// <summary>
    /// Occurs after the drop-down closes, whatever closed it.
    /// </summary>
    public event EventHandler? DropDownClosed;

    /// <inheritdoc />
    public override string TagName => "select";

    /// <summary>
    /// Gets or sets a value indicating whether the drop-down is open.
    /// </summary>
    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the text that the trigger shows while no item is selected.
    /// </summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an item must be selected.
    /// </summary>
    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <summary>
    /// Gets or sets the application-localizable message that reports a missing selection.
    /// </summary>
    public string RequiredErrorMessage
    {
        get => GetValue(RequiredErrorMessageProperty);
        set => SetValue(RequiredErrorMessageProperty, value);
    }

    /// <inheritdoc />
    public bool WillValidate => IsEffectivelyEnabled;

    /// <inheritdoc />
    public ValidityState Validity => new()
    {
        ValueMissing = Required && SelectedIndex < 0
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

    /// <inheritdoc />
    public void CaptureInitialValue()
    {
        _initialItem = SelectedItem;
    }

    /// <inheritdoc />
    public void Reset()
    {
        ValidityState.ResetInteraction(this);
        SelectedItem = _initialItem;
        CheckValidity();
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        return name switch
        {
            "data-open" => IsDropDownOpen ? string.Empty : null,
            "data-closed" => IsDropDownOpen ? null : string.Empty,
            "data-placeholder" => SelectedIndex < 0 ? string.Empty : null,
            _ => base.GetAttribute(name)
        };
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _value = DefinePart(ValuePartName, new View());
        Path icon = DefinePart(IconPartName, new Path
        {
            Data = Icons.ChevronDown,
            Fill = SvgCurrentColorBrush.Instance,
            Stretch = Stretch.None,
            Width = 16,
            Height = 16
        });
        _trigger = DefinePart(TriggerPartName, new StackPanel { Orientation = Orientation.Horizontal });
        _trigger.Children.Add(_value);
        _trigger.Children.Add(icon);

        _items = DefinePart(ItemsPartName, new View { OverflowY = Overflow.Auto });
        _popover = DefinePart(PopupPartName, new Popover { Content = _items, Align = Alignment.Start, SideOffset = 4 });
        Children.Add(_trigger);
        Children.Add(_popover);
        _popover.Target = _trigger;
        _popover.Opened += (_, _) => OnPopoverOpenChanged(true);
        _popover.Closed += (_, _) => OnPopoverOpenChanged(false);
    }

    /// <inheritdoc />
    protected override View? FindItemsHost()
    {
        return _items;
    }

    /// <inheritdoc />
    protected override UIElement CreateItemElement(object? item)
    {
        ListItem container = DefinePart(ItemPartName, new ListItem());
        container.Children.Add(base.CreateItemElement(item));
        return container;
    }

    /// <inheritdoc />
    protected override void ApplySelectedState(UIElement element, bool isSelected)
    {
        if (element is ListItem listItem)
        {
            listItem.IsSelected = isSelected;
        }
        else
        {
            base.ApplySelectedState(element, isSelected);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs args)
    {
        if (!args.Handled && IsEffectivelyEnabled)
        {
            bool isOpen = IsDropDownOpen;
            bool opens = args.Key is Key.Enter or Key.Space or Key.F4 ||
                (args.Key == Key.ArrowDown && args.Modifiers.HasFlag(InputModifiers.Alt));
            bool closes = args.Key is Key.Enter or Key.Space or Key.Escape;
            if (isOpen ? closes : opens)
            {
                IsDropDownOpen = !isOpen;
                args.Handled = true;
            }
        }

        base.OnKeyDown(args);
    }

    /// <inheritdoc />
    protected override void OnEffectiveEnabledChanged(bool isEnabled)
    {
        base.OnEffectiveEnabledChanged(isEnabled);
        ValidityState.Update(this, this);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        CaptureInitialValue();
    }

    private void OnIsDropDownOpenChanged(bool isOpen)
    {
        if (isOpen && _trigger.ActualWidth > 0)
        {
            ItemsHost.Width = WidthValue.Pixels(_trigger.ActualWidth);
        }

        _popover.IsOpen = isOpen;
        InvalidateStyle();
    }

    private void OnPopoverOpenChanged(bool isOpen)
    {
        IsDropDownOpen = isOpen;
        InvalidateStyle();
        if (isOpen)
        {
            DropDownOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnItemPointerPressed(object? sender, PointerEventArgs args)
    {
        if (IsDropDownOpen && args.Source is UIElement source && IndexOfItemElement(source) >= 0)
        {
            IsDropDownOpen = false;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        UpdateValue();
        ValidityState.MarkUserInteraction(this);
        ValidityState.Update(this, this);
    }

    /// <summary>
    /// Shows the selected item in the <c>value</c> part, or the placeholder while no item is selected. The item is built
    /// again from <see cref="ItemsControl.ItemTemplate"/> or shown as text, because an item element lives in the drop-down.
    /// </summary>
    private void UpdateValue()
    {
        if (_valueElement is not null)
        {
            _value.Children.Remove(_valueElement);
            _valueElement.Dispose();
        }

        object? item = SelectedItem;
        _valueElement = item is not null && ItemTemplate is DataTemplate template
            ? template.Build(item)
            : new Text
            {
                Value = item is null ? Placeholder ?? string.Empty : Convert.ToString(item, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            };
        _value.Children.Add(_valueElement);
        InvalidateStyle();
    }
}
