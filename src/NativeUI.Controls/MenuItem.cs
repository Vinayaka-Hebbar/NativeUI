using System.Globalization;
using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
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
public class MenuItem : ContentControl, IGridLayoutNode
{
    private const string IconPartName = "icon";
    private const string LabelPartName = "label";
    private const string ShortcutPartName = "shortcut";
    private const string CheckMarkPartName = "checkmark";

    private static readonly PathGeometry s_checkMark = Geometry.Parse("M6.2 11.6 L2.6 8 L3.8 6.8 L6.2 9.2 L12.2 3.2 L13.4 4.4 Z");

    private static readonly IReadOnlyList<GridLength> s_columns =
    [
        GridLength.Auto,
        GridLength.Star(),
        GridLength.Auto
    ];

    static MenuItem()
    {
        CursorProperty.OverrideMetadata<MenuItem>(Cursor.Pointer);
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
            propertyChanged: static (owner, previous, icon) => owner.OnIconChanged(previous, icon));

    /// <summary>
    /// Identifies the <see cref="InputGestureText"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> InputGestureTextProperty =
        BindableProperty.Create<MenuItem, string?>(
            nameof(InputGestureText),
            defaultValue: null,
            propertyChanged: static (owner, _, text) => owner.OnInputGestureTextChanged(text));

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

    private readonly EventHandler _canExecuteChangedHandler;
    private readonly IDisposable _gestureSubscription;
    private View? _iconHost;
    private View? _contentHost;
    private Text? _shortcutText;
    private Path? _checkMark;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuItem"/> class.
    /// </summary>
    public MenuItem()
    {
        _canExecuteChangedHandler = OnCanExecuteChanged;
        _gestureSubscription = ClickGesture.Attach(this, PerformClick);
        UpdateCheckedState();
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

    IReadOnlyList<GridLength> IGridLayoutNode.Rows => [];

    IReadOnlyList<GridLength> IGridLayoutNode.Columns => s_columns;

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
        if (!IsEnabled)
        {
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
        _checkMark = DefinePart(new Path
        {
            Fill = SvgCurrentColorBrush.Instance,
            Width = 16,
            Height = 16,
            IsVisible = false
        }, CheckMarkPartName);

        _iconHost = DefinePart(new View
        {
            Style = new Style { Margin = new Thickness(0, 8, 0, 0) }
        }, IconPartName);
        _iconHost.Children.Add(_checkMark);

        _contentHost = DefinePart(new View(), LabelPartName);

        _shortcutText = DefinePart(new Text
        {
            IsTextSelectionEnabled = false,
            IsVisible = false,
            Style = new Style { Margin = new Thickness(0, 0, 0, 16) }
        }, ShortcutPartName);

        Children.Add(_iconHost);
        Children.Add(_contentHost);
        Children.Add(_shortcutText);
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return _contentHost ?? this;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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

    private void OnIconChanged(UIElement? previous, UIElement? icon)
    {
        if (_iconHost is null)
        {
            return;
        }

        if (previous is not null)
        {
            _iconHost.Children.Remove(previous);
        }

        if (icon is not null)
        {
            if (_checkMark is not null)
            {
                _checkMark.IsVisible = false;
            }

            _iconHost.Children.Add(icon);
            _iconHost.IsVisible = true;
        }
        else
        {
            UpdateCheckedState();
        }
    }

    private void OnInputGestureTextChanged(string? text)
    {
        if (_shortcutText is not null)
        {
            _shortcutText.Value = text ?? string.Empty;
            _shortcutText.IsVisible = !string.IsNullOrEmpty(text);
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
        if (Command is ICommand command)
        {
            IsEnabled = command.CanExecute(CommandParameter);
        }
    }

    private void UpdateCheckedState()
    {
        bool isChecked = IsCheckable && IsChecked;
        SetState(ElementStates.Checked, isChecked);
        SetState(ElementStates.Unchecked, IsCheckable && !IsChecked);

        if (_checkMark is not null && _iconHost is not null)
        {
            if (IsCheckable)
            {
                if (Icon is null)
                {
                    _checkMark.Data = isChecked ? s_checkMark : null;
                    _checkMark.IsVisible = isChecked;
                    _iconHost.IsVisible = true;
                }
            }
            else if (Icon is null)
            {
                _checkMark.Data = null;
                _checkMark.IsVisible = false;
                _iconHost.IsVisible = true;
            }
        }

        InvalidateStyle();
        InvalidateVisual();
    }

    private void CloseParentMenu()
    {
        for (UIElement? current = Parent; current is not null; current = current.Parent)
        {
            if (current is DropdownMenu menu)
            {
                menu.IsOpen = false;
                break;
            }
        }
    }
}
