using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// A single-line text field edited by a platform control: <c>NSTextField</c> on macOS and <c>&lt;input&gt;</c> on Web,
/// which own the caret, selection, IME, and clipboard. The platform control presents <see cref="Value"/> and
/// <see cref="Placeholder"/>; the desktop renderer draws only the element's background.
/// </summary>
public class Input : UIElement
{
    // Layout measures one line of the font and never the value, because a long value scrolls instead of wrapping.
    private const string LineMeasureText = "Mg";

    /// <summary>
    /// Identifies the <see cref="GotFocus"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> GotFocusEvent =
        RoutedEvent.Register<Input, RoutedEventArgs>(nameof(GotFocus), RoutingStrategy.Direct);

    /// <summary>
    /// Identifies the <see cref="LostFocus"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> LostFocusEvent =
        RoutedEvent.Register<Input, RoutedEventArgs>(nameof(LostFocus), RoutingStrategy.Direct);

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Input, string>(nameof(Value), defaultValue: string.Empty);

    /// <summary>
    /// Identifies the <see cref="Placeholder"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> PlaceholderProperty =
        BindableProperty.Create<Input, string?>(nameof(Placeholder), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="IsReadOnly"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Create<Input, bool>(nameof(IsReadOnly), defaultValue: false);

    /// <inheritdoc />
    public override string TagName => "input";

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

    /// <summary>
    /// Gets a value indicating whether the platform control has keyboard focus.
    /// </summary>
    public bool IsFocused => States.HasFlag(ElementStates.Focus);

    /// <summary>
    /// Occurs when the platform control receives keyboard focus.
    /// </summary>
    public event EventHandler<RoutedEventArgs> GotFocus
    {
        add => AddHandler(GotFocusEvent, value);
        remove => RemoveHandler(GotFocusEvent, value);
    }

    /// <summary>
    /// Occurs when the platform control loses keyboard focus.
    /// </summary>
    public event EventHandler<RoutedEventArgs> LostFocus
    {
        add => AddHandler(LostFocusEvent, value);
        remove => RemoveHandler(LostFocusEvent, value);
    }

    /// <summary>
    /// Moves keyboard focus to this field. Does nothing until a platform host presents the field.
    /// </summary>
    public void Focus()
    {
        (NativePeer as IInputPeer)?.Focus();
    }

    /// <summary>
    /// Records a focus change reported by the platform control.
    /// </summary>
    internal void SetFocused(bool isFocused)
    {
        if (IsFocused == isFocused)
        {
            return;
        }

        SetState(ElementStates.Focus, isFocused);
        InvalidateStyle();
        InvalidateVisual();
        RaiseEvent(isFocused ? GotFocusEvent : LostFocusEvent, new RoutedEventArgs());
    }

    /// <inheritdoc />
    protected override string? GetLayoutTextContent()
    {
        return LineMeasureText;
    }
}
