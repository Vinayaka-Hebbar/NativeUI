using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Owns keyboard focus and pointer capture for the current UI thread.
/// </summary>
public sealed class InputManager
{
    [ThreadStatic]
    private static InputManager? _current;

    private readonly Dictionary<long, UIElement> _pointerCaptures = [];

    private InputManager()
    {
    }

    /// <summary>
    /// Gets the input manager for the calling UI thread.
    /// </summary>
    public static InputManager Current => _current ??= new InputManager();

    /// <summary>
    /// Gets the device that owns keyboard focus and dispatches keyboard input on this thread.
    /// </summary>
    public KeyboardDevice PrimaryKeyboardDevice { get; } = new();

    internal bool CapturePointer(UIElement element, long pointerId)
    {
        if (element.VisualRoot is null || !ReferenceEquals(element.GetInputTarget(), element))
        {
            return false;
        }

        if (_pointerCaptures.TryGetValue(pointerId, out UIElement? previous))
        {
            if (ReferenceEquals(previous, element))
            {
                return true;
            }

            ReleasePointerCapture(previous, pointerId);
            if (_pointerCaptures.ContainsKey(pointerId))
            {
                return false;
            }
        }

        _pointerCaptures.Add(pointerId, element);
        return true;
    }

    internal UIElement? GetPointerCapture(UIElement root, long pointerId)
    {
        if (!_pointerCaptures.TryGetValue(pointerId, out UIElement? element) ||
            !ReferenceEquals(element.VisualRoot, root))
        {
            return null;
        }

        if (ReferenceEquals(element.GetInputTarget(), element))
        {
            return element;
        }

        ReleasePointerCapture(element, pointerId);
        return null;
    }

    internal void ReleasePointerCapture(UIElement element, long pointerId)
    {
        if (!_pointerCaptures.TryGetValue(pointerId, out UIElement? captured) || !ReferenceEquals(captured, element))
        {
            return;
        }

        _pointerCaptures.Remove(pointerId);
        element.RaiseEvent(UIElement.PointerCaptureLostEvent, new PointerEventArgs(pointerId, Point.Zero));
    }

    internal void ReleasePointerCaptures(UIElement subtree)
    {
        foreach (KeyValuePair<long, UIElement> capture in _pointerCaptures.ToArray())
        {
            if (subtree.IsAncestorOf(capture.Value))
            {
                ReleasePointerCapture(capture.Value, capture.Key);
            }
        }
    }
}
