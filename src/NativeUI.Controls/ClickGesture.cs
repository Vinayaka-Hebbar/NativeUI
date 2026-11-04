using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Recognizes a click on an element: a pointer press followed by a release while the pointer is still over the element.
/// The element holds <see cref="ElementStates.Pressed"/> between the press and the release.
/// </summary>
internal static class ClickGesture
{
    /// <summary>
    /// Handles the pointer events of <paramref name="element"/> for the lifetime of the element.
    /// </summary>
    /// <param name="element">The element that receives the pointer events.</param>
    /// <param name="clicked">Invoked for each click while <paramref name="element"/> is enabled.</param>
    public static void Attach(UIElement element, Action clicked)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(clicked);

        element.PointerPressed += (_, _) =>
        {
            if (element.IsEnabled)
            {
                SetPressed(element, true);
            }
        };

        element.PointerReleased += (_, _) =>
        {
            if (!element.States.HasFlag(ElementStates.Pressed))
            {
                return;
            }

            SetPressed(element, false);
            if (element.States.HasFlag(ElementStates.Hover) && element.IsEnabled)
            {
                clicked();
            }
        };

        element.PointerExited += (_, _) =>
        {
            if (element.States.HasFlag(ElementStates.Pressed))
            {
                SetPressed(element, false);
            }
        };
    }

    private static void SetPressed(UIElement element, bool isPressed)
    {
        element.SetState(ElementStates.Pressed, isPressed);
        element.InvalidateStyle();
        element.InvalidateVisual();
    }
}
