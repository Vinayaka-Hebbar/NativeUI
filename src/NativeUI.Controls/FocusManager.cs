using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Owns keyboard focus for one root element that a platform host presents. The focused element has
/// <see cref="ElementStates.Focus"/>, and also <see cref="ElementStates.FocusVisible"/> when its
/// <see cref="UIElement.FocusState"/> makes the focus visible. The focused element and its ancestors up to the root have
/// <see cref="ElementStates.FocusWithin"/>. A subtree whose root has its own focus manager, such as a dialog presented in
/// its own window, is not part of this root.
/// </summary>
/// <remarks>
/// A focus change is atomic: the states of the previous and the new focus path change together, styles recompute once
/// from the topmost changed elements, and then <see cref="UIElement.LostFocusEvent"/> and
/// <see cref="UIElement.GotFocusEvent"/> are raised. Where a platform element owns focus, as a browser element or a
/// native text field does, the platform is the authority and reports each change to this manager.
/// <see cref="KeyboardNavigation"/> computes the navigation order.
/// </remarks>
public sealed class FocusManager : IDisposable
{
    private UIElement[] _focusPath = [];
    // The input that last moved focus or reached the focused element: a key press or a pointer press.
    private FocusState _mostRecentInput = FocusState.Pointer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FocusManager"/> class for <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The root element that a platform host presents.</param>
    /// <exception cref="InvalidOperationException"><paramref name="root"/> already has a focus manager.</exception>
    public FocusManager(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.FocusManager is not null)
        {
            throw new InvalidOperationException("The element already has a focus manager.");
        }

        Root = root;
        root.FocusManager = this;
    }

    /// <summary>
    /// Gets the root element whose subtree this manager owns.
    /// </summary>
    public UIElement Root { get; }

    /// <summary>
    /// Gets the element that has keyboard focus, or <see langword="null"/> when no element has focus.
    /// </summary>
    public UIElement? FocusedElement { get; private set; }

    /// <summary>
    /// Occurs after the focused element changes, before the focus events are raised. A host uses it to move platform focus
    /// between its own view and platform elements.
    /// </summary>
    internal event EventHandler? FocusChanged;

    /// <summary>
    /// Moves focus in <paramref name="direction"/> as a key press does. <see cref="FocusNavigationDirection.Next"/> and
    /// <see cref="FocusNavigationDirection.Previous"/> move from <see cref="FocusedElement"/> and wrap at either end.
    /// <see cref="FocusNavigationDirection.First"/> and <see cref="FocusNavigationDirection.Last"/> move to an end of the
    /// navigation order of <see cref="Root"/>.
    /// </summary>
    /// <param name="direction">The direction to move focus.</param>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    public bool MoveFocus(FocusNavigationDirection direction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        UIElement origin = direction is FocusNavigationDirection.First or FocusNavigationDirection.Last
            ? Root
            : FocusedElement ?? Root;
        return MoveFocus(origin, direction, FocusState.Keyboard);
    }

    /// <summary>
    /// Removes focus from the focused element, if any.
    /// </summary>
    public void ClearFocus()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (FocusedElement is UIElement focused)
        {
            ClearFocus(focused);
        }
    }

    /// <summary>
    /// Removes focus from the subtree and detaches this manager from <see cref="Root"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ApplyFocus(null, FocusState.Unfocused);
        if (ReferenceEquals(Root.FocusManager, this))
        {
            Root.FocusManager = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// Moves focus to <paramref name="element"/>. A platform element that owns focus receives it and reports the change.
    /// </summary>
    /// <param name="element">The element to focus.</param>
    /// <param name="state">How focus moves to the element.</param>
    /// <returns><see langword="true"/> when <paramref name="element"/> has focus after the call.</returns>
    internal bool Focus(UIElement element, FocusState state)
    {
        if (_disposed || !CanFocus(element))
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
        return true;
    }

    /// <summary>
    /// Moves focus from <paramref name="origin"/> in <paramref name="direction"/>. <see cref="FocusNavigationDirection.First"/>
    /// and <see cref="FocusNavigationDirection.Last"/> look only inside <paramref name="origin"/>.
    /// </summary>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    internal bool MoveFocus(UIElement origin, FocusNavigationDirection direction, FocusState state)
    {
        if (_disposed)
        {
            return false;
        }

        UIElement? next = KeyboardNavigation.GetNext(this, origin, direction);
        return next is not null && Focus(next, state);
    }

    /// <summary>
    /// Removes focus when the focused element is <paramref name="scope"/> or one of its descendants, such as when the
    /// element becomes disabled, hidden, unfocusable, detached, or disposed.
    /// </summary>
    internal void ClearFocus(UIElement scope)
    {
        if (_disposed || FocusedElement is not UIElement focused || !KeyboardNavigation.IsInclusiveAncestor(scope, focused))
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
        if (_disposed || !IsInRoot(element))
        {
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
        if (!_disposed && ReferenceEquals(FocusedElement, element))
        {
            ApplyFocus(null, FocusState.Unfocused);
        }
    }

    /// <summary>
    /// Focuses the nearest focusable element at or above <paramref name="target"/> after a pointer press, or removes focus
    /// when there is none.
    /// </summary>
    internal void HandlePointerPressed(UIElement? target)
    {
        if (_disposed)
        {
            return;
        }

        _mostRecentInput = FocusState.Pointer;
        for (UIElement? current = target; current is not null; current = current.Parent)
        {
            if (CanFocus(current))
            {
                Focus(current, FocusState.Pointer);
                return;
            }

            if (ReferenceEquals(current, Root))
            {
                break;
            }
        }

        if (FocusedElement is UIElement focused)
        {
            ClearFocus(focused);
        }
    }

    /// <summary>
    /// Raises <paramref name="routedEvent"/> on the focused element, or on <see cref="Root"/> while no element has focus.
    /// </summary>
    /// <returns><see langword="true"/> when a handler set <see cref="RoutedEventArgs.Handled"/>.</returns>
    internal bool RaiseKeyEvent(RoutedEvent<KeyEventArgs> routedEvent, KeyEventArgs args)
    {
        if (_disposed)
        {
            return false;
        }

        _mostRecentInput = FocusState.Keyboard;
        UIElement target = FocusedElement ?? Root;
        target.RaiseEvent(routedEvent, args);
        return args.Handled;
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> can receive focus from this manager.
    /// </summary>
    internal bool CanFocus(UIElement element)
    {
        return element.Focusable && element.IsEnabled && element.IsVisibleInTree && IsInRoot(element);
    }

    private bool IsInRoot(UIElement element)
    {
        return ReferenceEquals(element.FindFocusManager(), this);
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
        FocusChanged?.Invoke(this, EventArgs.Empty);

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
                bool isAncestor = !ReferenceEquals(other, candidate) && KeyboardNavigation.IsInclusiveAncestor(other, candidate);
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
    /// Returns the elements from <see cref="Root"/> down to <paramref name="element"/>.
    /// </summary>
    private UIElement[] CreatePath(UIElement? element)
    {
        if (element is null)
        {
            return [];
        }

        List<UIElement> path = [];
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            path.Add(current);
            if (ReferenceEquals(current, Root))
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
