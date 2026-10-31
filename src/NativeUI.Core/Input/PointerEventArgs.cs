using NativeUI.Layout;

namespace NativeUI.Core;

/// <summary>
/// Identifies the pointer buttons held during a pointer event.
/// </summary>
[Flags]
public enum PointerButtons
{
    /// <summary>
    /// No button is held.
    /// </summary>
    None = 0,

    /// <summary>
    /// The primary button is held.
    /// </summary>
    Primary = 1,

    /// <summary>
    /// The secondary button is held.
    /// </summary>
    Secondary = 2,

    /// <summary>
    /// The middle button is held.
    /// </summary>
    Middle = 4
}

/// <summary>
/// Identifies the keyboard modifiers held during a pointer event.
/// </summary>
[Flags]
public enum PointerModifiers
{
    /// <summary>
    /// No modifier is held.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Shift key is held.
    /// </summary>
    Shift = 1,

    /// <summary>
    /// The Control key is held.
    /// </summary>
    Control = 2,

    /// <summary>
    /// The Alt or Option key is held.
    /// </summary>
    Alt = 4,

    /// <summary>
    /// The Command or Windows key is held.
    /// </summary>
    Meta = 8
}

/// <summary>
/// Describes a platform-independent pointer event.
/// </summary>
/// <param name="pointerId">The identity of the pointer that produced the event.</param>
/// <param name="position">The position relative to the root of the element tree, in logical pixels.</param>
/// <param name="buttons">The buttons held during the event.</param>
/// <param name="modifiers">The keyboard modifiers held during the event.</param>
/// <param name="clickCount">The number of consecutive clicks, or zero when the event is not part of a click.</param>
public sealed class PointerEventArgs(
    long pointerId,
    Point position,
    PointerButtons buttons = PointerButtons.None,
    PointerModifiers modifiers = PointerModifiers.None,
    int clickCount = 0) : RoutedEventArgs
{
    /// <summary>
    /// Gets the identity of the pointer that produced the event.
    /// </summary>
    public long PointerId { get; } = pointerId;

    /// <summary>
    /// Gets the position relative to the root of the element tree, in logical pixels.
    /// </summary>
    public Point Position { get; } = position;

    /// <summary>
    /// Gets the buttons held during the event.
    /// </summary>
    public PointerButtons Buttons { get; } = buttons;

    /// <summary>
    /// Gets the keyboard modifiers held during the event.
    /// </summary>
    public PointerModifiers Modifiers { get; } = modifiers;

    /// <summary>
    /// Gets the number of consecutive clicks, or zero when the event is not part of a click.
    /// </summary>
    public int ClickCount { get; } = clickCount;
}
