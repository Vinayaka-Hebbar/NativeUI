using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Recognizes a click on an element: a pointer press followed by a release while the pointer is still over the element, an
/// Enter key press while the element has focus, or a Space key press and release while the element has focus. The element
/// holds <see cref="ElementStates.Pressed"/> between a press and its release.
/// </summary>
/// <remarks>
/// The keys act only on key events that the element itself raised. Keys typed into a focused descendant are not clicks.
/// A handled key event prevents the platform's default action, such as a browser button click or a page scroll.
/// </remarks>
internal static class ClickGesture
{
    private const InputModifiers ShortcutModifiers = InputModifiers.Control | InputModifiers.Alt | InputModifiers.Meta;

    /// <summary>
    /// Handles the pointer and key events of <paramref name="element"/> for the lifetime of the element.
    /// </summary>
    /// <param name="element">The element that receives the pointer and key events.</param>
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

        element.KeyDown += (_, args) =>
        {
            if (!IsActivationKey(element, args))
            {
                return;
            }

            args.Handled = true;
            if (args.IsRepeat)
            {
                return;
            }

            if (args.Key == Key.Enter)
            {
                clicked();
            }
            else
            {
                SetPressed(element, true);
            }
        };

        element.KeyUp += (_, args) =>
        {
            if (args.Key != Key.Space || !IsActivationKey(element, args))
            {
                return;
            }

            args.Handled = true;
            if (element.States.HasFlag(ElementStates.Pressed))
            {
                SetPressed(element, false);
                clicked();
            }
        };
    }

    private static bool IsActivationKey(UIElement element, KeyEventArgs args)
    {
        return args.Key is Key.Enter or Key.Space &&
            (args.Modifiers & ShortcutModifiers) == InputModifiers.None &&
            ReferenceEquals(args.Source, element) &&
            element.IsEnabled;
    }

    private static void SetPressed(UIElement element, bool isPressed)
    {
        element.SetState(ElementStates.Pressed, isPressed);
        element.InvalidateStyle();
        element.InvalidateVisual();
    }
}
