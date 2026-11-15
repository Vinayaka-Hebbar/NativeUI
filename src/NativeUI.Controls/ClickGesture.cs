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
    /// Handles the pointer and key events of <paramref name="element"/> until the returned object is disposed.
    /// </summary>
    /// <param name="element">The element that receives the pointer and key events.</param>
    /// <param name="clicked">Invoked for each click while <paramref name="element"/> is enabled.</param>
    /// <returns>An object that stops handling the events of <paramref name="element"/> when disposed.</returns>
    public static IDisposable Attach(UIElement element, Action clicked)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(clicked);

        Subscription subscription = new(element, clicked);
        element.PointerPressed += subscription.OnPointerPressed;
        element.PointerReleased += subscription.OnPointerReleased;
        element.PointerExited += subscription.OnPointerExited;
        element.KeyDown += subscription.OnKeyDown;
        element.KeyUp += subscription.OnKeyUp;
        return subscription;
    }

    private static bool IsActivationKey(UIElement element, KeyEventArgs args)
    {
        return args.Key is Key.Enter or Key.Space &&
            (args.Modifiers & ShortcutModifiers) == InputModifiers.None &&
            ReferenceEquals(args.Source, element) &&
            element.IsEffectivelyEnabled;
    }

    private static void SetPressed(UIElement element, bool isPressed)
    {
        element.SetState(ElementStates.Pressed, isPressed);
        element.InvalidateStyle();
        element.InvalidateVisual();
    }

    private sealed class Subscription(UIElement element, Action clicked) : IDisposable
    {
        public void OnPointerPressed(object? sender, PointerEventArgs args)
        {
            if (!args.Handled && element.IsEffectivelyEnabled && args.Buttons.HasFlag(PointerButtons.Primary))
            {
                SetPressed(element, true);
            }
        }

        public void OnPointerReleased(object? sender, PointerEventArgs args)
        {
            if (!element.States.HasFlag(ElementStates.Pressed))
            {
                return;
            }

            SetPressed(element, false);
            if (!args.Handled && element.States.HasFlag(ElementStates.Hover) && element.IsEffectivelyEnabled)
            {
                args.Handled = true;
                clicked();
            }
        }

        public void OnPointerExited(object? sender, PointerEventArgs args)
        {
            if (element.States.HasFlag(ElementStates.Pressed))
            {
                SetPressed(element, false);
            }
        }

        public void OnKeyDown(object? sender, KeyEventArgs args)
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
        }

        public void OnKeyUp(object? sender, KeyEventArgs args)
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
        }

        public void Dispose()
        {
            element.PointerPressed -= OnPointerPressed;
            element.PointerReleased -= OnPointerReleased;
            element.PointerExited -= OnPointerExited;
            element.KeyDown -= OnKeyDown;
            element.KeyUp -= OnKeyUp;
            if (element.States.HasFlag(ElementStates.Pressed))
            {
                SetPressed(element, false);
            }
        }
    }
}
