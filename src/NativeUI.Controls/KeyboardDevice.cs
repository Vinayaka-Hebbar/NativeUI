using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Tracks keyboard focus, focus visibility, and routed keyboard input for the current UI thread.
/// Platform peers report native focus changes to this device; logical focus belongs to <see cref="FocusManager"/>.
/// </summary>
public sealed class KeyboardDevice
{
    private UIElement[] _focusPath = [];
    // The input that last moved focus or reached the focused element: a key press or a pointer press.
    private FocusState _mostRecentInput = FocusState.Pointer;

    internal KeyboardDevice()
    {
    }

    /// <summary>
    /// Gets the element that has keyboard focus, or <see langword="null"/> when no element has focus.
    /// </summary>
    public UIElement? FocusedElement { get; private set; }

    /// <summary>
    /// Gets or sets whether the most recent input came from the keyboard or a pointer.
    /// </summary>
    internal FocusState MostRecentInput
    {
        get => _mostRecentInput;
        set => _mostRecentInput = value;
    }

    /// <summary>
    /// Occurs after the focused element changes, before the focus events are raised. Hosts use it to move platform focus
    /// between its own view and platform elements.
    /// </summary>
    internal event Action<UIElement?, UIElement?>? FocusChanged;

    /// <summary>
    /// Moves focus in <paramref name="direction"/> as a key press does. <see cref="FocusNavigationDirection.Next"/> and
    /// <see cref="FocusNavigationDirection.Previous"/> move from the focused element in that root and wrap at either end.
    /// <see cref="FocusNavigationDirection.First"/> and <see cref="FocusNavigationDirection.Last"/> move to an end of the
    /// navigation order of the hosted visual root.
    /// </summary>
    /// <param name="root">The hosted root receiving keyboard input.</param>
    /// <param name="direction">The direction to move focus.</param>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    internal bool MoveFocus(UIElement root, FocusNavigationDirection direction)
    {
        UIElement origin = root;
        if (direction is not (FocusNavigationDirection.First or FocusNavigationDirection.Last) &&
            FocusedElement is UIElement focused && ReferenceEquals(focused.VisualRoot, root))
        {
            origin = focused;
        }

        return MoveFocus(origin, direction, FocusState.Keyboard);
    }

    /// <summary>
    /// Removes focus from the focused element, if any.
    /// </summary>
    public void ClearFocus()
    {
        if (FocusedElement is UIElement focused)
        {
            ClearFocus(focused);
        }
    }

    /// <summary>
    /// Moves focus to <paramref name="element"/>. A platform element that owns focus receives it and reports the change.
    /// </summary>
    /// <param name="element">The element to focus.</param>
    /// <param name="state">How focus moves to the element.</param>
    /// <returns><see langword="true"/> when <paramref name="element"/> has focus after the call.</returns>
    internal bool Focus(UIElement element, FocusState state)
    {
        if (!CanFocus(element))
        {
            return false;
        }

        if (state is FocusState.Keyboard or FocusState.Pointer)
        {
            _mostRecentInput = state;
        }

        if (element.NativeHandle is IFocusPeer peer)
        {
            peer.Focus();
            return ReferenceEquals(FocusedElement, element);
        }

        ApplyFocus(element, state);
        return ReferenceEquals(FocusedElement, element);
    }

    /// <summary>
    /// Moves focus from <paramref name="origin"/> in <paramref name="direction"/>. <see cref="FocusNavigationDirection.First"/>
    /// and <see cref="FocusNavigationDirection.Last"/> look only inside <paramref name="origin"/>.
    /// </summary>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    internal bool MoveFocus(UIElement origin, FocusNavigationDirection direction, FocusState state)
    {
        if (origin.VisualRoot is not UIElement root)
        {
            return false;
        }

        UIElement? next = KeyboardNavigation.GetNext(root, origin, direction);
        return next is not null && Focus(next, state);
    }

    /// <summary>
    /// Removes focus when the focused element is <paramref name="scope"/> or one of its descendants, such as when the
    /// element becomes disabled, hidden, unfocusable, detached, or disposed.
    /// </summary>
    internal void ClearFocus(UIElement scope)
    {
        if (FocusedElement is not UIElement focused || !_focusPath.Contains(scope))
        {
            return;
        }

        ApplyFocus(null, FocusState.Unfocused);
        if (focused.NativeHandle is IFocusPeer peer)
        {
            peer.Blur();
        }
    }

    /// <summary>
    /// Records that the platform element of <paramref name="element"/> gained focus.
    /// </summary>
    /// <param name="element">The element whose platform element gained focus.</param>
    /// <param name="state">How the platform moved focus. <see cref="FocusState.Programmatic"/> leaves visibility to the
    /// most recent input.</param>
    internal void HandleGotFocus(UIElement element, FocusState state)
    {
        if (!element.IsAttachedToVisualTree)
        {
            return;
        }

        if (!CanFocus(element))
        {
            if (ReferenceEquals(FocusedElement, element))
            {
                ClearFocus(element);
            }
            else if (element.NativeHandle is IFocusPeer peer)
            {
                peer.Blur();
            }

            return;
        }

        if (state is FocusState.Keyboard or FocusState.Pointer)
        {
            _mostRecentInput = state;
        }

        ApplyFocus(element, state);
    }

    /// <summary>
    /// Records that the platform element of <paramref name="element"/> lost focus. A loss that no longer applies to the
    /// focused element is ignored.
    /// </summary>
    internal void HandleLostFocus(UIElement element)
    {
        if (ReferenceEquals(FocusedElement, element))
        {
            ApplyFocus(null, FocusState.Unfocused);
        }
    }

    /// <summary>
    /// Focuses the nearest focusable element at or above <paramref name="target"/> after a pointer press, or removes focus
    /// when there is none.
    /// </summary>
    internal void HandlePointerPressed(UIElement root, UIElement? target)
    {
        _mostRecentInput = FocusState.Pointer;
        for (UIElement? current = target; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current.VisualRoot, root) && CanFocus(current))
            {
                Focus(current, FocusState.Pointer);
                return;
            }

            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        if (FocusedElement is UIElement focused && ReferenceEquals(focused.VisualRoot, root))
        {
            ClearFocus(focused);
        }
    }

    /// <summary>
    /// Raises <paramref name="routedEvent"/> on the focused element, or on the hosted visual root while no element has focus.
    /// </summary>
    /// <returns><see langword="true"/> when a handler set <see cref="RoutedEventArgs.Handled"/>.</returns>
    internal bool RaiseKeyEvent(UIElement root, RoutedEvent<KeyEventArgs> routedEvent, KeyEventArgs args)
    {
        _mostRecentInput = FocusState.Keyboard;
        UIElement target = FocusedElement is UIElement focused && ReferenceEquals(focused.VisualRoot, root) ? focused : root;
        if (!target.IsEffectivelyEnabled && ReferenceEquals(routedEvent, UIElement.KeyDownEvent))
        {
            return false;
        }

        target.RaiseEvent(routedEvent, args);
        return args.Handled;
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> can receive keyboard focus.
    /// </summary>
    internal bool CanFocus(UIElement element)
    {
        if (!element.Focusable || !element.IsEffectivelyEnabled || element.VisualRoot is not UIElement root)
        {
            return false;
        }

        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (!ReferenceEquals(current.VisualRoot, root) || !current.IsVisible || current is Portal { IsOpen: false })
            {
                return false;
            }

            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether focus that reaches <paramref name="element"/> with <paramref name="state"/> is visible: after a
    /// key press, on an element that edits text, or from code after a key press.
    /// </summary>
    private bool IsFocusVisible(UIElement element, FocusState state)
    {
        return state switch
        {
            FocusState.Keyboard => true,
            FocusState.Pointer => element.AcceptsTextInput,
            FocusState.Programmatic => _mostRecentInput == FocusState.Keyboard || element.AcceptsTextInput,
            _ => false
        };
    }

    /// <summary>
    /// Changes the focused element and its <see cref="UIElement.FocusState"/> in one step, then raises the focus events.
    /// </summary>
    private void ApplyFocus(UIElement? element, FocusState state)
    {
        UIElement? previous = FocusedElement;
        bool isVisible = element is not null && IsFocusVisible(element, state);
        if (ReferenceEquals(previous, element) &&
            (element is null || (element.FocusState == state && element.States.HasFlag(ElementStates.FocusVisible) == isVisible)))
        {
            return;
        }

        UIElement[] departed = _focusPath;
        UIElement[] entered = CreatePath(element);
        int shared = CountSharedPrefix(departed, entered);

        FocusedElement = element;
        _focusPath = entered;
        if (previous is not null)
        {
            previous.FocusState = FocusState.Unfocused;
            previous.SetState(ElementStates.Focus | ElementStates.FocusVisible, false);
        }

        for (int i = shared; i < departed.Length; i++)
        {
            departed[i].SetState(ElementStates.FocusWithin, false);
        }

        for (int i = shared; i < entered.Length; i++)
        {
            entered[i].SetState(ElementStates.FocusWithin, true);
        }

        if (element is not null)
        {
            element.FocusState = state;
            element.SetState(ElementStates.Focus, true);
            element.SetState(ElementStates.FocusVisible, isVisible);
        }

        // Descendant selectors make every element below a changed element depend on its state.
        UIElement? departedBranch = shared < departed.Length ? departed[shared] : null;
        UIElement? enteredBranch = shared < entered.Length ? entered[shared] : null;
        InvalidateTopmost(previous, element, departedBranch, enteredBranch);
        if (element is not null)
        {
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), element);
        }

        if (!ReferenceEquals(FocusedElement, element))
        {
            return;
        }

        FocusChanged?.Invoke(departed.Length == 0 ? null : departed[0], entered.Length == 0 ? null : entered[0]);

        if (!ReferenceEquals(previous, element))
        {
            previous?.RaiseEvent(UIElement.LostFocusEvent, new RoutedEventArgs());
            element?.RaiseEvent(UIElement.GotFocusEvent, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Recomputes the styles of each changed element that is not inside another changed element, which recomputes the rest.
    /// </summary>
    private static void InvalidateTopmost(params ReadOnlySpan<UIElement?> changed)
    {
        for (int i = 0; i < changed.Length; i++)
        {
            if (changed[i] is not UIElement candidate)
            {
                continue;
            }

            bool isCovered = false;
            for (int j = 0; j < changed.Length; j++)
            {
                if (j == i || changed[j] is not UIElement other)
                {
                    continue;
                }

                // An identical element is recomputed once, at its first position.
                bool isEarlierDuplicate = ReferenceEquals(other, candidate) && j < i;
                bool isAncestor = !ReferenceEquals(other, candidate) && other.IsAncestorOf(candidate);
                if (isEarlierDuplicate || isAncestor)
                {
                    isCovered = true;
                    break;
                }
            }

            if (!isCovered)
            {
                candidate.InvalidateStyle();
            }
        }
    }

    /// <summary>
    /// Returns the elements from the hosted visual root down to <paramref name="element"/>.
    /// </summary>
    private UIElement[] CreatePath(UIElement? element)
    {
        if (element is null)
        {
            return [];
        }

        UIElement? root = element.VisualRoot;
        List<UIElement> path = [];
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            path.Add(current);
            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        path.Reverse();
        return [.. path];
    }

    private static int CountSharedPrefix(UIElement[] first, UIElement[] second)
    {
        int count = 0;
        int limit = Math.Min(first.Length, second.Length);
        while (count < limit && ReferenceEquals(first[count], second[count]))
        {
            count++;
        }

        return count;
    }
}
