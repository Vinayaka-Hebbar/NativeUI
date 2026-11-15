using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Arranges radio choices and exposes their selected value for data binding.
/// Nested radio groups establish independent selection boundaries.
/// </summary>
public class RadioGroup : StackPanel, IFormControl
{
    /// <summary>
    /// Identifies the value of the selected item. Null clears the selection.
    /// </summary>
    public static readonly BindableProperty<object?> SelectedValueProperty =
        BindableProperty.Create<RadioGroup, object?>(
            nameof(SelectedValue),
            propertyChanged: static (owner, previous, value) => owner.OnSelectedValueChanged(previous, value));

    /// <summary>
    /// Identifies whether this group requires a selection.
    /// </summary>
    public static readonly BindableProperty<bool> RequiredProperty =
        BindableProperty.Create<RadioGroup, bool>(
            nameof(Required),
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the application-localizable required error message.
    /// </summary>
    public static readonly BindableProperty<string> RequiredErrorMessageProperty =
        BindableProperty.Create<RadioGroup, string>(
            nameof(RequiredErrorMessage),
            defaultValue: "Select an option.",
            propertyChanged: static (owner, _, _) => owner.CheckValidity());

    /// <summary>
    /// Identifies the routed event raised when the selected value changes.
    /// </summary>
    public static readonly RoutedEvent<SelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<RadioGroup, SelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategy.Bubble);

    private RadioGroupItem[] _items = [];
    private readonly Queue<(object? Previous, object? Current)> _selectionChanges = new();
    private bool _isUpdatingSelection;
    private object? _initialValue;

    /// <summary>
    /// Initializes selection coordination for items inside arbitrary layout wrappers.
    /// </summary>
    public RadioGroup()
    {
        AddHandler(RadioButton.CheckedEvent, OnChecked, handledEventsToo: true);
        KeyDown += OnKeyDown;
        ValidityState.Update(this, this);
    }

    /// <inheritdoc />
    public override string TagName => "radiogroup";

    /// <summary>
    /// Gets or sets the selected item value. An unmatched value is retained until a matching item is added.
    /// </summary>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// Gets or sets whether at least one enabled choice must be selected.
    /// </summary>
    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <summary>
    /// Gets or sets the required-selection validation message.
    /// </summary>
    public string RequiredErrorMessage
    {
        get => GetValue(RequiredErrorMessageProperty);
        set => SetValue(RequiredErrorMessageProperty, value);
    }

    /// <summary>
    /// Reports every SelectedValue transition, including transitions made by reentrant handlers.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc />
    public bool WillValidate => IsEffectivelyEnabled;

    /// <inheritdoc />
    public ValidityState Validity
    {
        get
        {
            if (!Required)
            {
                return ValidityState.Empty;
            }

            foreach (RadioGroupItem item in _items)
            {
                if (item.IsChecked == true && IsValidSelection(item))
                {
                    return ValidityState.Empty;
                }
            }

            return new ValidityState
            {
                ValueMissing = true
            };
        }
    }

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
            foreach (RadioGroupItem radio in _items)
            {
                if (radio.Focus())
                {
                    break;
                }
            }
        }

        return valid;
    }

    /// <inheritdoc />
    public void CaptureInitialValue()
    {
        _initialValue = SelectedValue;
    }

    /// <inheritdoc />
    public void Reset()
    {
        ValidityState.ResetInteraction(this);
        SelectedValue = _initialValue;
        foreach (RadioGroupItem item in _items)
        {
            ValidityState.ResetInteraction(item);
            ValidityState.Update(item, item);
        }

        UpdateSelection();
        CheckValidity();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        CaptureInitialValue();
    }

    /// <inheritdoc />
    protected override void OnEffectiveEnabledChanged(bool isEnabled)
    {
        base.OnEffectiveEnabledChanged(isEnabled);
        UpdateSelection();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            KeyDown -= OnKeyDown;
            _isUpdatingSelection = true;
            foreach (RadioGroupItem item in _items)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            _items = [];
            _selectionChanges.Clear();
        }

        base.Dispose(disposing);
    }

    internal void RegisterItem(RadioGroupItem item)
    {
        if (IsDisposed ||
            item.IsDisposed ||
            !ReferenceEquals(item.Group, this) ||
            !IsAncestorOf(item))
        {
            return;
        }

        List<RadioGroupItem> registered = new(_items.Length + 1);
        foreach (RadioGroupItem current in _items)
        {
            if (!current.IsDisposed &&
                ReferenceEquals(current.Group, this) &&
                IsAncestorOf(current))
            {
                registered.Add(current);
            }
            else
            {
                current.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (!registered.Contains(item))
        {
            registered.Add(item);
            item.PropertyChanged += OnItemPropertyChanged;
        }

        RadioGroupItem[] items = registered.ToArray();
        Array.Sort(items, CompareTreeOrder);
        _items = items;
        UpdateSelection();
    }

    internal void UnregisterItem(RadioGroupItem item)
    {
        int index = Array.IndexOf(_items, item);
        if (index < 0)
        {
            return;
        }

        item.PropertyChanged -= OnItemPropertyChanged;
        RadioGroupItem[] items = new RadioGroupItem[_items.Length - 1];
        Array.Copy(_items, 0, items, 0, index);
        Array.Copy(_items, index + 1, items, index, items.Length - index);
        _items = items;
        UpdateSelection();
    }

    private void OnChecked(object? sender, RoutedEventArgs args)
    {
        if (!_isUpdatingSelection &&
            args.Source is RadioGroupItem item &&
            item.IsChecked == true &&
            ReferenceEquals(item.Group, this))
        {
            SelectedValue = item.Value;
            UpdateSelection();
        }
    }

    private void OnItemPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (IsDisposed ||
            sender is not RadioGroupItem item ||
            !ReferenceEquals(item.Group, this))
        {
            return;
        }

        if (ReferenceEquals(args.Property, RadioGroupItem.ValueProperty) ||
            ReferenceEquals(args.Property, RadioButton.IsCheckedProperty) ||
            ReferenceEquals(args.Property, TabIndexProperty) ||
            ReferenceEquals(args.Property, FocusableProperty) ||
            ReferenceEquals(args.Property, IsEffectivelyEnabledProperty) ||
            ReferenceEquals(args.Property, IsVisibleProperty))
        {
            UpdateSelection();
        }
    }

    private void OnSelectedValueChanged(object? previous, object? current)
    {
        _selectionChanges.Enqueue((previous, current));
        UpdateSelection();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled ||
            args.Source is not RadioGroupItem origin ||
            !ReferenceEquals(origin.Group, this) ||
            args.Modifiers != InputModifiers.None)
        {
            return;
        }

        int step = args.Key switch
        {
            Key.ArrowLeft or Key.ArrowUp => -1,
            Key.ArrowRight or Key.ArrowDown => 1,
            _ => 0
        };
        if (step == 0)
        {
            return;
        }

        RadioGroupItem[] items = _items;
        int index = Array.IndexOf(items, origin);
        if (index < 0)
        {
            return;
        }

        for (int offset = 1; offset < items.Length; offset++)
        {
            RadioGroupItem item = items[(index + step * offset + items.Length) % items.Length];
            if (!CanNavigateTo(item) || !item.Focus(FocusState.Keyboard))
            {
                continue;
            }

            // Focus callbacks can move or disable the destination.
            if (!CanNavigateTo(item) || Array.IndexOf(_items, item) < 0)
            {
                continue;
            }

            args.Handled = true;
            ValidityState.MarkUserInteraction(this);
            ValidityState.MarkUserInteraction(item);
            SelectedValue = item.Value;
            break;
        }
    }

    internal void UpdateSelection()
    {
        if (_isUpdatingSelection || IsDisposed)
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            while (!IsDisposed)
            {
                SynchronizeSelection();
                if (IsDisposed)
                {
                    break;
                }

                object? value = SelectedValue;
                RadioGroupItem[] items = _items;
                UpdateTabStops();
                ValidityState.Update(this, this);
                if (!ReferenceEquals(items, _items) || !Equals(value, SelectedValue))
                {
                    continue;
                }

                if (_selectionChanges.Count == 0)
                {
                    break;
                }

                (object? Previous, object? Current) change = _selectionChanges.Dequeue();
                RaiseSelectionChanged(change.Previous, change.Current);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void SynchronizeSelection()
    {
        while (!IsDisposed)
        {
            object? value = SelectedValue;
            RadioGroupItem[] items = _items;
            bool selected = false;
            foreach (RadioGroupItem item in items)
            {
                if (!ReferenceEquals(items, _items) || !Equals(value, SelectedValue))
                {
                    break;
                }

                if (item.IsDisposed || !ReferenceEquals(item.Group, this))
                {
                    continue;
                }

                bool matches = !selected && value is not null && Equals(item.Value, value);
                selected |= matches;
                item.IsChecked = matches;
            }

            if (ReferenceEquals(items, _items) && Equals(value, SelectedValue))
            {
                return;
            }
        }
    }

    private void RaiseSelectionChanged(object? previous, object? current)
    {
        RaiseEvent(SelectionChangedEvent, new SelectionChangedEventArgs(
            previous is null ? [] : new object?[] { previous },
            current is null ? [] : new object?[] { current }));
    }

    private void UpdateTabStops()
    {
        RadioGroupItem? first = null;
        RadioGroupItem? selected = null;
        RadioGroupItem[] items = _items;
        foreach (RadioGroupItem item in items)
        {
            if (item.TabIndex < 0 || !CanNavigateTo(item))
            {
                continue;
            }

            first ??= item;
            if (item.IsChecked == true)
            {
                selected = item;
                break;
            }
        }

        foreach (RadioGroupItem item in items)
        {
            if (!item.IsDisposed && ReferenceEquals(item.Group, this))
            {
                item.IsTabStop = ReferenceEquals(item, selected ?? first);
            }
        }
    }

    private bool IsValidSelection(RadioGroupItem item)
    {
        return !item.IsDisposed &&
            item.IsEffectivelyEnabled &&
            ReferenceEquals(item.Group, this);
    }

    private bool CanNavigateTo(RadioGroupItem item)
    {
        return IsValidSelection(item) &&
            InputManager.Current.PrimaryKeyboardDevice.CanFocus(item);
    }

    /// <summary>
    /// Compares distinct registered items within this group's tree.
    /// Disconnected items are removed during registration before sorting.
    /// </summary>
    private int CompareTreeOrder(RadioGroupItem first, RadioGroupItem second)
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        List<UIElement> firstPath = [];
        for (UIElement? element = first; !ReferenceEquals(element, this); element = element.Parent)
        {
            if (element is null)
            {
                throw new InvalidOperationException("A registered radio item is outside its group's tree.");
            }

            firstPath.Add(element);
        }

        List<UIElement> secondPath = [];
        for (UIElement? element = second; !ReferenceEquals(element, this); element = element.Parent)
        {
            if (element is null)
            {
                throw new InvalidOperationException("A registered radio item is outside its group's tree.");
            }

            secondPath.Add(element);
        }

        int firstIndex = firstPath.Count - 1;
        int secondIndex = secondPath.Count - 1;
        while (firstIndex >= 0 &&
            secondIndex >= 0 &&
            ReferenceEquals(firstPath[firstIndex], secondPath[secondIndex]))
        {
            firstIndex--;
            secondIndex--;
        }

        if (firstIndex < 0)
        {
            return -1;
        }

        if (secondIndex < 0)
        {
            return 1;
        }

        UIElement firstBranch = firstPath[firstIndex];
        UIElement secondBranch = secondPath[secondIndex];
        if (firstBranch.Parent is not View parent || !ReferenceEquals(secondBranch.Parent, parent))
        {
            throw new InvalidOperationException("Registered radio items must have a common parent in the group.");
        }

        int firstPosition = parent.Children.IndexOf(firstBranch);
        int secondPosition = parent.Children.IndexOf(secondBranch);
        if (firstPosition < 0 || secondPosition < 0)
        {
            throw new InvalidOperationException("A registered radio item's parent does not contain it.");
        }

        return firstPosition.CompareTo(secondPosition);
    }
}
