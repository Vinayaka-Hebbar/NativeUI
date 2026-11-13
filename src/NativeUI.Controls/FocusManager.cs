using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Owns keyboard focus for one root element that a platform host presents. The focused element has
/// <see cref="ElementStates.Focus"/>, and also <see cref="ElementStates.FocusVisible"/> when the platform indicates focus
/// visibly. The focused element and its ancestors up to the root have <see cref="ElementStates.FocusWithin"/>. A subtree
/// whose root has its own focus manager, such as a dialog presented in its own window, is not part of this root.
/// </summary>
/// <remarks>
/// A focus change is atomic: the states of the previous and the new focus path change together, styles recompute once
/// from the topmost changed elements, and then <see cref="UIElement.LostFocusEvent"/> and
/// <see cref="UIElement.GotFocusEvent"/> are raised. Where a platform element owns focus, as a browser element or a
/// native text field does, the platform is the authority and reports each change to this manager.
/// </remarks>
public sealed class FocusManager : IDisposable
{
    private UIElement[] _focusPath = [];
    private bool _isKeyboardModality;
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
    /// Moves focus to the next or previous element in the navigation order, wrapping at either end.
    /// </summary>
    /// <param name="direction">The direction to move focus.</param>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    public bool MoveFocus(FocusNavigationDirection direction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _isKeyboardModality = true;
        List<UIElement> order = GetNavigationOrder();
        if (order.Count == 0)
        {
            return false;
        }

        int current = FocusedElement is UIElement focused ? order.IndexOf(focused) : -1;
        int next;
        if (direction == FocusNavigationDirection.Next)
        {
            next = current < 0 ? 0 : (current + 1) % order.Count;
        }
        else
        {
            next = current < 0 ? order.Count - 1 : (current - 1 + order.Count) % order.Count;
        }

        return Focus(order[next]);
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

        ApplyFocus(null, isVisible: false);
        if (ReferenceEquals(Root.FocusManager, this))
        {
            Root.FocusManager = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// Moves focus to <paramref name="element"/>. A platform element that owns focus receives it and reports the change.
    /// </summary>
    /// <returns><see langword="true"/> when <paramref name="element"/> has focus after the call.</returns>
    internal bool Focus(UIElement element)
    {
        if (_disposed || !CanFocus(element))
        {
            return false;
        }

        if (element.NativeHandle is IFocusPeer peer)
        {
            peer.Focus();
            return ReferenceEquals(FocusedElement, element);
        }

        ApplyFocus(element, IsFocusVisible(element));
        return true;
    }

    /// <summary>
    /// Records a focus change that a platform element reports. A loss of focus that no longer applies to the focused
    /// element is ignored.
    /// </summary>
    /// <param name="element">The element whose platform element gained or lost focus.</param>
    /// <param name="isFocused">Whether the platform element has focus.</param>
    /// <param name="isVisible">Whether the platform indicates the focus visibly, or <see langword="null"/> to decide from
    /// the input modality.</param>
    internal void SyncFocus(UIElement element, bool isFocused, bool? isVisible = null)
    {
        if (_disposed)
        {
            return;
        }

        if (isFocused)
        {
            if (IsInRoot(element))
            {
                ApplyFocus(element, isVisible ?? IsFocusVisible(element));
            }
        }
        else if (ReferenceEquals(FocusedElement, element))
        {
            ApplyFocus(null, isVisible: false);
        }
    }

    /// <summary>
    /// Removes focus when the focused element is <paramref name="element"/> or one of its descendants, because the element
    /// became disabled, hidden, unfocusable, detached, or disposed.
    /// </summary>
    internal void ReleaseFocus(UIElement element)
    {
        if (_disposed || FocusedElement is not UIElement focused || !IsInclusiveAncestor(element, focused))
        {
            return;
        }

        ApplyFocus(null, isVisible: false);
        (focused.NativeHandle as IFocusPeer)?.Blur();
    }

    /// <summary>
    /// Focuses the nearest focusable element at or above <paramref name="target"/> after a pointer press, or removes focus
    /// when there is none. Focus that a pointer press moves is not visible, except on text inputs.
    /// </summary>
    internal void FocusFromPointer(UIElement? target)
    {
        if (_disposed)
        {
            return;
        }

        _isKeyboardModality = false;
        for (UIElement? current = target; current is not null; current = current.Parent)
        {
            if (CanFocus(current))
            {
                Focus(current);
                return;
            }

            if (ReferenceEquals(current, Root))
            {
                break;
            }
        }

        if (FocusedElement is UIElement focused)
        {
            ReleaseFocus(focused);
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

        _isKeyboardModality = true;
        (FocusedElement ?? Root).RaiseEvent(routedEvent, args);
        return args.Handled;
    }

    private bool IsFocusVisible(UIElement element)
    {
        return _isKeyboardModality || element is Input;
    }

    private bool CanFocus(UIElement element)
    {
        return element.Focusable && element.IsEnabled && element.IsVisibleInTree && IsInRoot(element);
    }

    private bool IsInRoot(UIElement element)
    {
        return ReferenceEquals(element.FindFocusManager(), this);
    }

    /// <summary>
    /// Returns whether <paramref name="ancestor"/> is <paramref name="element"/> or one of its ancestors.
    /// </summary>
    private static bool IsInclusiveAncestor(UIElement ancestor, UIElement element)
    {
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Moves focus to the first element of <paramref name="scope"/> in the navigation order, such as the content of a
    /// popup that just opened.
    /// </summary>
    /// <returns><see langword="true"/> when an element of <paramref name="scope"/> received focus.</returns>
    internal bool FocusFirst(UIElement scope)
    {
        if (_disposed)
        {
            return false;
        }

        foreach (UIElement candidate in GetNavigationOrder())
        {
            if (IsInclusiveAncestor(scope, candidate))
            {
                return Focus(candidate);
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the focusable elements with a non-negative <see cref="UIElement.TabIndex"/>: positive values first in
    /// ascending order, then zero in tree order. Hidden subtrees and subtrees with their own focus manager are skipped.
    /// The elements of an open portal with an anchor follow the anchor's subtree instead of the portal's own position.
    /// </summary>
    private List<UIElement> GetNavigationOrder()
    {
        Dictionary<UIElement, List<Portal>> anchoredPortals = new(ReferenceEqualityComparer.Instance);
        CollectAnchoredPortals(Root, anchoredPortals);

        List<UIElement> candidates = [];
        CollectCandidates(Root, candidates, anchoredPortals);

        // A portal whose anchor is not in the visited tree follows every other element.
        foreach (List<Portal> portals in anchoredPortals.Values.ToArray())
        {
            foreach (Portal portal in portals)
            {
                CollectPortalCandidates(portal, candidates, anchoredPortals);
            }
        }

        List<UIElement> order = [];
        foreach (UIElement candidate in candidates)
        {
            if (candidate.TabIndex > 0)
            {
                order.Add(candidate);
            }
        }

        // A stable sort keeps tree order among equal positive values.
        order = [.. order.OrderBy(static element => element.TabIndex)];
        foreach (UIElement candidate in candidates)
        {
            if (candidate.TabIndex == 0)
            {
                order.Add(candidate);
            }
        }

        return order;
    }

    private void CollectAnchoredPortals(UIElement element, Dictionary<UIElement, List<Portal>> anchoredPortals)
    {
        if (!element.IsVisible || (element.FocusManager is not null && !ReferenceEquals(element, Root)))
        {
            return;
        }

        if (element is Portal { Anchor: UIElement anchor } portal)
        {
            if (!anchoredPortals.TryGetValue(anchor, out List<Portal>? portals))
            {
                portals = [];
                anchoredPortals[anchor] = portals;
            }

            portals.Add(portal);
        }

        if (element is View view)
        {
            foreach (UIElement child in view.Children)
            {
                CollectAnchoredPortals(child, anchoredPortals);
            }
        }
    }

    private void CollectCandidates(UIElement element, List<UIElement> candidates, Dictionary<UIElement, List<Portal>> anchoredPortals)
    {
        if (!element.IsVisible || (element.FocusManager is not null && !ReferenceEquals(element, Root)))
        {
            return;
        }

        if (element is Portal { Anchor: not null })
        {
            return;
        }

        if (element.TabIndex >= 0 && CanFocus(element))
        {
            candidates.Add(element);
        }

        if (element is View view)
        {
            foreach (UIElement child in view.Children)
            {
                CollectCandidates(child, candidates, anchoredPortals);
            }
        }

        if (anchoredPortals.Remove(element, out List<Portal>? portals))
        {
            foreach (Portal portal in portals)
            {
                CollectPortalCandidates(portal, candidates, anchoredPortals);
            }
        }
    }

    /// <summary>
    /// Collects the candidates of an open portal with an anchor, which <see cref="CollectCandidates"/> skips at its tree position.
    /// </summary>
    private void CollectPortalCandidates(Portal portal, List<UIElement> candidates, Dictionary<UIElement, List<Portal>> anchoredPortals)
    {
        if (portal.TabIndex >= 0 && CanFocus(portal))
        {
            candidates.Add(portal);
        }

        foreach (UIElement child in portal.Children)
        {
            CollectCandidates(child, candidates, anchoredPortals);
        }
    }

    /// <summary>
    /// Changes the focused element and its visibility in one step, then raises the focus events.
    /// </summary>
    private void ApplyFocus(UIElement? element, bool isVisible)
    {
        UIElement? previous = FocusedElement;
        if (ReferenceEquals(previous, element) &&
            (element is null || element.States.HasFlag(ElementStates.FocusVisible) == isVisible))
        {
            return;
        }

        UIElement[] departed = _focusPath;
        UIElement[] entered = CreatePath(element);
        int shared = CountSharedPrefix(departed, entered);

        FocusedElement = element;
        _focusPath = entered;
        previous?.SetState(ElementStates.Focus | ElementStates.FocusVisible, false);
        for (int i = shared; i < departed.Length; i++)
        {
            departed[i].SetState(ElementStates.FocusWithin, false);
        }

        for (int i = shared; i < entered.Length; i++)
        {
            entered[i].SetState(ElementStates.FocusWithin, true);
        }

        element?.SetState(ElementStates.Focus, true);
        element?.SetState(ElementStates.FocusVisible, isVisible);

        // Descendant selectors make every element below a changed element depend on its state.
        InvalidateTopmost(previous, element, shared < departed.Length ? departed[shared] : null, shared < entered.Length ? entered[shared] : null);
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
                bool isAncestor = !ReferenceEquals(other, candidate) && IsInclusiveAncestor(other, candidate);
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
