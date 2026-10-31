using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Identifies a native browser pointer event. Values match <c>PointerKind</c> in <c>nativeui-dom.js</c>.
/// </summary>
internal enum PointerEventKind
{
    /// <summary>
    /// The pointer moved.
    /// </summary>
    Move,

    /// <summary>
    /// A button was pressed.
    /// </summary>
    Press,

    /// <summary>
    /// A button was released.
    /// </summary>
    Release,

    /// <summary>
    /// The browser cancelled the pointer.
    /// </summary>
    Cancel,

    /// <summary>
    /// The pointer left the mounted tree.
    /// </summary>
    Leave
}

/// <summary>
/// Describes one native browser pointer event.
/// </summary>
internal readonly record struct PointerEventData
{
    /// <summary>
    /// Gets the native event kind.
    /// </summary>
    public PointerEventKind Kind { get; init; }

    /// <summary>
    /// Gets the identifier of the attached element under the pointer, or zero when there is none.
    /// </summary>
    public int NodeId { get; init; }

    /// <summary>
    /// Gets the identity of the pointer.
    /// </summary>
    public long PointerId { get; init; }

    /// <summary>
    /// Gets the position relative to the <c>native-ui</c> element.
    /// </summary>
    public Point Position { get; init; }

    /// <summary>
    /// Gets the buttons held during the event.
    /// </summary>
    public PointerButtons Buttons { get; init; }

    /// <summary>
    /// Gets the keyboard modifiers held during the event.
    /// </summary>
    public PointerModifiers Modifiers { get; init; }

    /// <summary>
    /// Gets the native click count, or zero when the event is not part of a click.
    /// </summary>
    public int ClickCount { get; init; }
}
