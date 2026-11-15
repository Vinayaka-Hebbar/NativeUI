using System.Globalization;
using System.Collections.Specialized;
using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;
using Path = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls;

/// <summary>
/// Represents a selectable item inside a <see cref="DropdownMenu"/>.
/// </summary>
/// <remarks>
/// <para>
/// A menu item presents an optional <see cref="Icon"/>, <see cref="ContentControl.Content"/>, and an optional
/// <see cref="InputGestureText"/> shortcut in a horizontal grid row. It supports activation by pointer click or Enter and Space keys, executing
/// an associated <see cref="Command"/>, raising the <see cref="Click"/> routed event, and toggling <see cref="IsChecked"/>
/// when <see cref="IsCheckable"/> is <see langword="true"/>.
/// </para>
/// <para>
/// The template exposes the <c>icon</c>, <c>label</c>, <c>shortcut</c>, and <c>checkmark</c> template parts. The item is styled using the
/// <c>:checked</c>, <c>:disabled</c>, and <c>:focus-visible</c> pseudo-classes.
/// </para>
/// </remarks>
[ContentProperty(nameof(Items))]
public class MenuItem : ContentControl, IGridLayoutNode
{
    private const string IconPartName = "icon";
    private const string LabelPartName = "label";
    private const string ShortcutPartName = "shortcut";
    private const string CheckMarkPartName = "checkmark";
    private const string SubmenuIndicatorPartName = "submenu-indicator";

    private readonly IReadOnlyList<GridLength> _columns =
    [
        GridLength.Auto,
        GridLength.Star(),
        GridLength.Auto,
        GridLength.Auto
    ];

    static MenuItem()
    {
        CursorProperty.OverrideMetadata<MenuItem>(Cursor.Default);
        FocusableProperty.OverrideMetadata<MenuItem>(true);
    }

    /// <summary>
    /// Identifies the <see cref="Click"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MenuItem, RoutedEventArgs>(nameof(Click), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="Command"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ICommand?> CommandProperty =
        BindableProperty.Create<MenuItem, ICommand?>(
            nameof(Command),
            defaultValue: null,
            propertyChanged: static (owner, oldCommand, newCommand) => owner.OnCommandPropertyChanged(oldCommand, newCommand));

    /// <summary>
    /// Identifies the <see cref="CommandParameter"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> CommandParameterProperty =
        BindableProperty.Create<MenuItem, object?>(
            nameof(CommandParameter),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateIsEnabledFromCommand());

    /// <summary>
    /// Identifies the <see cref="Icon"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> IconProperty =
        BindableProperty.Create<MenuItem, UIElement?>(
            nameof(Icon),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateIcon());

    /// <summary>
    /// Identifies the <see cref="InputGestureText"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> InputGestureTextProperty =
        BindableProperty.Create<MenuItem, string?>(
            nameof(InputGestureText),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateShortcut());

    /// <summary>
    /// Identifies the <see cref="ShortcutText"/> property, an alias for <see cref="InputGestureText"/>.
    /// </summary>
    public static readonly BindableProperty<string?> ShortcutTextProperty = InputGestureTextProperty;

    /// <summary>
    /// Identifies the <see cref="IsCheckable"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsCheckableProperty =
        BindableProperty.Create<MenuItem, bool>(
            nameof(IsCheckable),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateCheckedState());

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsCheckedProperty =
        BindableProperty.Create<MenuItem, bool>(
            nameof(IsChecked),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateCheckedState());

    /// <summary>
    /// Identifies the <see cref="IsSubmenuOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsSubmenuOpenProperty =
        BindableProperty.Create<MenuItem, bool>(nameof(IsSubmenuOpen), defaultValue: false,
            propertyChanged: static (owner, _, isOpen) => owner.OnIsSubmenuOpenChanged(isOpen));

    /// <summary>
    /// Identifies the <see cref="HasItems"/> computed property.
    /// </summary>
    public static readonly ComputedBindableProperty<MenuItem, bool> HasItemsProperty =
        BindableProperty.CreateComputed<MenuItem, bool>(nameof(HasItems), static item => item.HasItems);

    private readonly EventHandler _canExecuteChangedHandler;
    private readonly IDisposable _gestureSubscription;
    private bool _canExecuteCommand = true;
    private bool _previousHasItems;
    private View? _iconHost;
    private View? _contentHost;
    private Text? _shortcutText;
    private Path? _checkMark;
    private Path? _submenuIndicator;
    private DropdownMenu? _submenu;
    private readonly DispatcherTimer _openTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuItem"/> class.
    /// </summary>
    public MenuItem()
    {
        _canExecuteChangedHandler = OnCanExecuteChanged;
        _gestureSubscription = ClickGesture.Attach(this, PerformClick);
        _openTimer.Tick += OnOpenTimerTick;
        _closeTimer.Tick += OnCloseTimerTick;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        KeyDown += OnSubmenuKeyDown;
        UpdateCheckedState();
        UpdateIsEnabledFromCommand();
        UpdateEffectiveEnabledState();
    }

    /// <inheritdoc />
    public override string TagName => "menuitem";

    /// <summary>
    /// Gets or sets the command executed when the menu item is activated.
    /// </summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter passed to <see cref="Command"/>.
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon element displayed before the menu item content.
    /// </summary>
    public UIElement? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the text description of the keyboard shortcut associated with the item, such as <c>⌘S</c>.
    /// </summary>
    public string? InputGestureText
    {
        get => GetValue(InputGestureTextProperty);
        set => SetValue(InputGestureTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the shortcut label. This property shares its value with <see cref="InputGestureText"/>.
    /// </summary>
    public string? ShortcutText
    {
        get => InputGestureText;
        set => InputGestureText = value;
    }

    /// <inheritdoc />
    protected override bool IsEnabledCore => _canExecuteCommand;

    /// <summary>
    /// Gets or sets a value indicating whether the menu item can be checked and unchecked.
    /// </summary>
    public bool IsCheckable
    {
        get => GetValue(IsCheckableProperty);
        set => SetValue(IsCheckableProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the menu item is currently checked.
    /// </summary>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Gets the child menu items displayed in this item's submenu.
    /// </summary>
    public IList<UIElement> Items => EnsureSubmenu().Items;

    /// <summary>
    /// Gets a value indicating whether this item has a submenu.
    /// </summary>
    public bool HasItems => _submenu is not null && _submenu.Items.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether this item's submenu is open.
    /// </summary>
    public bool IsSubmenuOpen
    {
        get => GetValue(IsSubmenuOpenProperty);
        set => SetValue(IsSubmenuOpenProperty, value);
    }

    /// <summary>
    /// Gets the nearest menu that contains this item.
    /// </summary>
    internal DropdownMenu? ParentMenu
    {
        get
        {
            for (UIElement? current = Parent; current is not null; current = current.Parent)
            {
                if (current is DropdownMenu menu)
                {
                    return menu;
                }
            }

            return null;
        }
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        return name switch
        {
            "data-has-items" => HasItems ? string.Empty : null,
            "data-open" => IsSubmenuOpen ? string.Empty : null,
            "data-closed" => HasItems && !IsSubmenuOpen ? string.Empty : null,
            _ => base.GetAttribute(name)
        };
    }

    /// <summary>
    /// Cancels hover timers and closes this item's submenu.
    /// </summary>
    internal void CloseSubmenu()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        IsSubmenuOpen = false;
    }

    IReadOnlyList<GridLength> IGridLayoutNode.Rows => [];

    IReadOnlyList<GridLength> IGridLayoutNode.Columns => _columns;

    double IGridLayoutNode.RowSpacing => 0;

    double IGridLayoutNode.ColumnSpacing => 0;

    GridPlacement IGridLayoutNode.GetPlacement(ILayoutNode child)
    {
        if (ReferenceEquals(child, _iconHost))
        {
            return new GridPlacement(0, 0, 1, 1);
        }

        if (ReferenceEquals(child, _contentHost))
        {
            return new GridPlacement(0, 1, 1, 1);
        }

        if (ReferenceEquals(child, _shortcutText))
        {
            return new GridPlacement(0, 2, 1, 1);
        }

        if (ReferenceEquals(child, _submenuIndicator))
        {
            return new GridPlacement(0, 3, 1, 1);
        }

        return new GridPlacement(0, 1, 1, 1);
    }

    /// <summary>
    /// Occurs when the menu item is activated either via pointer click, Enter or Space key press, or programmatic invocation.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>
    /// Programmatically triggers the menu item activation, toggling <see cref="IsChecked"/> when checkable, raising
    /// <see cref="ClickEvent"/>, executing <see cref="Command"/>, and closing the parent <see cref="DropdownMenu"/>.
    /// </summary>
    public void PerformClick()
    {
        UpdateIsEnabledFromCommand();
        if (!IsEffectivelyEnabled)
        {
            return;
        }

        if (HasItems)
        {
            IsSubmenuOpen = true;
            if (InputManager.Current.PrimaryKeyboardDevice.MostRecentInput == FocusState.Keyboard)
            {
                _submenu?.FocusFirstItem();
            }

            return;
        }

        if (IsCheckable)
        {
            IsChecked = !IsChecked;
        }

        RaiseEvent(ClickEvent, new RoutedEventArgs());

        if (Command is ICommand command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }

        CloseParentMenu();
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _checkMark = DefinePart(CheckMarkPartName, new Path
        {
            Fill = SvgCurrentColorBrush.Instance,
            Stretch = Stretch.None,
            Width = 16,
            Height = 16,
            IsVisible = false
        });

        _iconHost = DefinePart(IconPartName, new View
        {
            Style = new Style { Margin = new Thickness(0, 8, 0, 0) }
        });
        _iconHost.Children.Add(_checkMark);

        _contentHost = DefinePart(LabelPartName, new View());

        _shortcutText = DefinePart(ShortcutPartName, new Text
        {
            IsTextSelectionEnabled = false,
            IsVisible = false,
            Style = new Style { Margin = new Thickness(0, 0, 0, 16) }
        });

        Children.Add(_iconHost);
        Children.Add(_contentHost);
        Children.Add(_shortcutText);
        _submenuIndicator = DefinePart(SubmenuIndicatorPartName, new Path
        {
            Data = Icons.ChevronRight,
            Fill = SvgCurrentColorBrush.Instance,
            Stretch = Stretch.None,
            Width = 16,
            Height = 16,
            IsVisible = false
        });
        Children.Add(_submenuIndicator);
        UpdateIcon();
        UpdateShortcut();
        UpdateCheckedState();
        UpdateSubmenuIndicator();
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return _contentHost ?? this;
    }

    /// <inheritdoc />
    protected override void OnParentChanged()
    {
        base.OnParentChanged();
        if (ParentMenu is not { IsOpen: true })
        {
            CloseSubmenu();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindableProperty property, object? oldValue, object? newValue)
    {
        base.OnPropertyChanged(property, oldValue, newValue);
        if ((ReferenceEquals(property, IsEffectivelyEnabledProperty) && newValue is false) ||
            (ReferenceEquals(property, IsVisibleProperty) && newValue is false))
        {
            CloseSubmenu();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _openTimer.Stop();
            _closeTimer.Stop();
            _openTimer.Tick -= OnOpenTimerTick;
            _closeTimer.Tick -= OnCloseTimerTick;
            PointerEntered -= OnPointerEntered;
            PointerExited -= OnPointerExited;
            KeyDown -= OnSubmenuKeyDown;
            if (_submenu is not null)
            {
                _submenu.Closed -= OnSubmenuClosed;
                if (_submenu.Items is INotifyCollectionChanged items)
                {
                    items.CollectionChanged -= OnItemsChanged;
                }
            }

            _gestureSubscription.Dispose();
            if (Command is ICommand command)
            {
                command.CanExecuteChanged -= _canExecuteChangedHandler;
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Returns the text content for type-ahead navigation in menus.
    /// </summary>
    internal string? GetSearchText()
    {
        if (Content is string text)
        {
            return text;
        }

        if (Content is Text textElement)
        {
            return textElement.Value;
        }

        if (Content is not null)
        {
            return Convert.ToString(Content, CultureInfo.CurrentCulture);
        }

        return null;
    }

    private void UpdateIcon()
    {
        if (_iconHost is null)
        {
            return;
        }

        foreach (UIElement child in _iconHost.Children.ToArray())
        {
            if (!ReferenceEquals(child, _checkMark) && !ReferenceEquals(child, Icon))
            {
                _iconHost.Children.Remove(child);
            }
        }

        if (Icon is UIElement icon && !_iconHost.Children.Contains(icon))
        {
            _iconHost.Children.Add(icon);
        }

        UpdateCheckedState();
    }

    private void UpdateShortcut()
    {
        if (_shortcutText is not null)
        {
            _shortcutText.Value = InputGestureText ?? string.Empty;
            _shortcutText.IsVisible = !string.IsNullOrEmpty(InputGestureText);
        }
    }

    private void UpdateSubmenuIndicator()
    {
        if (_submenuIndicator is not null)
        {
            _submenuIndicator.IsVisible = HasItems;
        }
    }

    private void OnCommandPropertyChanged(ICommand? oldCommand, ICommand? newCommand)
    {
        if (oldCommand is not null)
        {
            oldCommand.CanExecuteChanged -= _canExecuteChangedHandler;
        }

        if (newCommand is not null)
        {
            newCommand.CanExecuteChanged += _canExecuteChangedHandler;
        }

        UpdateIsEnabledFromCommand();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateIsEnabledFromCommand();
    }

    private void UpdateIsEnabledFromCommand()
    {
        bool canExecute = Command?.CanExecute(CommandParameter) ?? true;
        if (_canExecuteCommand == canExecute)
        {
            return;
        }

        _canExecuteCommand = canExecute;
        UpdateEffectiveEnabledState();
    }

    private void UpdateCheckedState()
    {
        bool isChecked = IsCheckable && IsChecked;
        SetState(ElementStates.Checked, isChecked);
        SetState(ElementStates.Unchecked, IsCheckable && !IsChecked);

        if (_checkMark is not null && _iconHost is not null)
        {
            _checkMark.Data = isChecked && Icon is null ? Icons.Checkmark : null;
            _checkMark.IsVisible = isChecked && Icon is null;
            _iconHost.IsVisible = true;
        }

        InvalidateStyle();
        InvalidateVisual();
    }

    private void CloseParentMenu()
    {
        ParentMenu?.CloseMenu();
    }

    private DropdownMenu EnsureSubmenu()
    {
        if (_submenu is not null)
        {
            return _submenu;
        }

        _submenu = new DropdownMenu
        {
            OwnerItem = this,
            Side = PortalSide.Right,
            Align = Alignment.Start,
            Position = NativeUI.Layout.Position.Absolute
        };
        _submenu.Closed += OnSubmenuClosed;
        if (_submenu.Items is INotifyCollectionChanged items)
        {
            items.CollectionChanged += OnItemsChanged;
        }

        Children.Add(_submenu);
        _submenu.Target = this;
        return _submenu;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        bool hasItems = HasItems;
        if (_previousHasItems != hasItems)
        {
            bool previous = _previousHasItems;
            _previousHasItems = hasItems;
            NotifyPropertyChanged(HasItemsProperty, previous, hasItems);
        }

        UpdateSubmenuIndicator();
        if (!hasItems)
        {
            CloseSubmenu();
        }

        InvalidateStyle();
        InvalidateVisual();
    }

    private void OnIsSubmenuOpenChanged(bool isOpen)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        SetState(ElementStates.Open, isOpen);
        InvalidateStyle();
        if (!isOpen)
        {
            if (_submenu is not null)
            {
                _submenu.IsOpen = false;
            }

            return;
        }

        if (!HasItems || !IsEffectivelyEnabled || ParentMenu is not { IsOpen: true } parent)
        {
            IsSubmenuOpen = false;
            return;
        }

        parent.CloseOtherSubmenus(this);
        EnsureSubmenu().IsOpen = true;
    }

    private void OnSubmenuClosed(object? sender, EventArgs args)
    {
        IsSubmenuOpen = false;
    }

    private void OnSubmenuKeyDown(object? sender, KeyEventArgs args)
    {
        if (!args.Handled && ReferenceEquals(args.Source, this) && args.Key == Key.ArrowRight && HasItems && IsEffectivelyEnabled)
        {
            args.Handled = true;
            IsSubmenuOpen = true;
            _submenu?.FocusFirstItem();
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs args)
    {
        if (!IsEffectivelyEnabled || ParentMenu is not { IsOpen: true })
        {
            return;
        }

        _closeTimer.Stop();
        Focus(FocusState.Pointer);
        if (!IsSubmenuOpen)
        {
            _openTimer.Start();
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs args)
    {
        _openTimer.Stop();
        if (IsSubmenuOpen)
        {
            _closeTimer.Start();
        }
    }

    private void OnOpenTimerTick(object? sender, EventArgs args)
    {
        _openTimer.Stop();
        if (States.HasFlag(ElementStates.Hover) && IsEffectivelyEnabled && ParentMenu is { IsOpen: true } menu)
        {
            menu.CloseOtherSubmenus(this);
            if (HasItems)
            {
                IsSubmenuOpen = true;
            }
        }
    }

    private void OnCloseTimerTick(object? sender, EventArgs args)
    {
        _closeTimer.Stop();
        if (States.HasFlag(ElementStates.Hover) || IsPointerOverSubmenu())
        {
            return;
        }

        CloseSubmenu();
    }

    private bool IsPointerOverSubmenu()
    {
        if (_submenu is not { IsOpen: true })
        {
            return false;
        }

        if (_submenu.States.HasFlag(ElementStates.Hover) || _submenu.ContentPart.States.HasFlag(ElementStates.Hover))
        {
            return true;
        }

        foreach (MenuItem item in _submenu.GetMenuItems())
        {
            if (item.IsPointerOverSubmenu())
            {
                return true;
            }
        }

        return false;
    }
}
