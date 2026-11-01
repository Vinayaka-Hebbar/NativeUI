using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Represents a clickable button control supporting text, composite child content,
/// pointer state transitions (<see cref="ElementStates.Hover"/>, <see cref="ElementStates.Pressed"/>, <see cref="ElementStates.Disabled"/>),
/// routed <see cref="ClickEvent"/>, and <see cref="ICommand"/> execution.
/// </summary>
public class Button : View
{
    private static readonly Style s_defaultStyle = new() { Width = WidthValue.FitContent };

    /// <summary>
    /// Identifies the <see cref="Click"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<Button, RoutedEventArgs>(nameof(Click), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="Text"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> TextProperty =
        BindableProperty.Create<Button, string?>(
            nameof(Text),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.OnTextPropertyChanged());

    /// <summary>
    /// Identifies the <see cref="Command"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ICommand?> CommandProperty =
        BindableProperty.Create<Button, ICommand?>(
            nameof(Command),
            defaultValue: null,
            propertyChanged: static (owner, oldCommand, newCommand) => owner.OnCommandPropertyChanged(oldCommand, newCommand));

    /// <summary>
    /// Identifies the <see cref="CommandParameter"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> CommandParameterProperty =
        BindableProperty.Create<Button, object?>(
            nameof(CommandParameter),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateIsEnabledFromCommand());

    private readonly EventHandler _canExecuteChangedHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="Button"/> class.
    /// </summary>
    public Button()
    {
        _canExecuteChangedHandler = OnCanExecuteChanged;

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerExited += OnPointerExited;
    }

    /// <inheritdoc />
    public override string TagName => "button";

    /// <inheritdoc />
    public override Cursor Cursor => ComputedStyle.Cursor != Cursor.Default ? ComputedStyle.Cursor : Cursor.Pointer;

    /// <summary>
    /// Gets or sets the button text content displayed when no child elements are added.
    /// </summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed when the button is clicked.
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
    /// Occurs when the button is clicked either via pointer interaction or programmatic invocation.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>
    /// Programmatically triggers the button's click behavior, raising <see cref="ClickEvent"/>
    /// and executing <see cref="Command"/> if <see cref="UIElement.IsEnabled"/> is <see langword="true"/>.
    /// </summary>
    public void PerformClick()
    {
        if (!IsEnabled)
        {
            return;
        }

        RaiseEvent(ClickEvent, new RoutedEventArgs());

        if (Command is ICommand command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
    }

    /// <inheritdoc />
    protected override string? GetLayoutTextContent()
    {
        return Children.Count == 0 ? Text : null;
    }

    /// <inheritdoc />
    protected override IStyle? DefaultStyle => s_defaultStyle;

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        SetState(ElementStates.Pressed, true);
        InvalidateStyle();
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        bool wasPressed = States.HasFlag(ElementStates.Pressed);
        if (wasPressed)
        {
            SetState(ElementStates.Pressed, false);
            InvalidateStyle();
            InvalidateVisual();

            if (States.HasFlag(ElementStates.Hover) && IsEnabled)
            {
                PerformClick();
            }
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (States.HasFlag(ElementStates.Pressed))
        {
            SetState(ElementStates.Pressed, false);
            InvalidateStyle();
            InvalidateVisual();
        }
    }

    private void OnTextPropertyChanged()
    {
        InvalidateStyle();
        InvalidateVisual();
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
}
