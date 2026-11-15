using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Provides attached properties and methods to configure and evaluate keyboard tab navigation.
/// </summary>
/// <remarks>
/// The tab order holds the focusable elements with a non-negative <see cref="UIElement.TabIndex"/>: positive values first in
/// ascending order, then zero in tree order, as in HTML. Hidden subtrees, subtrees with their own focus manager, and
/// portals that are closed are skipped. The elements of an open portal with an anchor follow the anchor's subtree instead
/// of the portal's own position. Tab moves from a popup's target into the popup.
/// </remarks>
public static class KeyboardNavigation
{
    /// <summary>
    /// Identifies whether an element participates in sequential keyboard navigation.
    /// </summary>
    public static readonly BindableProperty<bool> IsTabStopProperty =
        BindableProperty.CreateAttached<bool>("IsTabStop", typeof(KeyboardNavigation), defaultValue: true);

    /// <summary>
    /// Gets whether Tab navigation may stop at the element. Direct focus remains available.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    public static bool GetIsTabStop(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(IsTabStopProperty);
    }

    /// <summary>
    /// Sets whether the element participates in sequential keyboard navigation.
    /// </summary>
    /// <param name="element">The element to configure.</param>
    /// <param name="value">Whether Tab navigation may stop at the element.</param>
    public static void SetIsTabStop(UIElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsTabStopProperty, value);
    }

    /// <summary>
    /// Identifies the <c>KeyboardNavigation.TabNavigation</c> attached property.
    /// </summary>
    public static readonly BindableProperty<KeyboardNavigationMode> TabNavigationProperty =
        BindableProperty.CreateAttached<KeyboardNavigationMode>(
            "TabNavigation",
            typeof(KeyboardNavigation),
            KeyboardNavigationMode.Continue);

    /// <summary>
    /// Gets the <see cref="KeyboardNavigationMode"/> that specifies tab navigation behavior for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose tab navigation mode is requested.</param>
    /// <returns>The configured mode, or <see cref="KeyboardNavigationMode.Continue"/> by default.</returns>
    public static KeyboardNavigationMode GetTabNavigation(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(TabNavigationProperty);
    }

    /// <summary>
    /// Sets the <see cref="KeyboardNavigationMode"/> that specifies tab navigation behavior for <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element on which to set the tab navigation mode.</param>
    /// <param name="mode">The tab navigation mode to set.</param>
    public static void SetTabNavigation(UIElement element, KeyboardNavigationMode mode)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TabNavigationProperty, mode);
    }

    /// <summary>
    /// Returns the element that focus moves to from <paramref name="origin"/> in <paramref name="direction"/>, or
    /// <see langword="null"/> when there is none. When moving inside <paramref name="origin"/>,
    /// <see cref="FocusNavigationDirection.First"/> and <see cref="FocusNavigationDirection.Last"/> search descendants.
    /// </summary>
    /// <param name="root">The hosted visual root containing the navigation tree.</param>
    /// <param name="origin">The element that focus moves from, or the element to search inside.</param>
    /// <param name="direction">The direction to move focus.</param>
    public static UIElement? GetNext(UIElement root, UIElement origin, FocusNavigationDirection direction)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(origin);

        List<UIElement> order = GetTabOrder(root);
        if (direction is FocusNavigationDirection.First or FocusNavigationDirection.Last)
        {
            List<UIElement> inside = [];
            foreach (UIElement element in order)
            {
                if (origin.IsAncestorOf(element))
                {
                    inside.Add(element);
                }
            }

            if (inside.Count == 0)
            {
                return null;
            }

            return direction == FocusNavigationDirection.First ? inside[0] : inside[^1];
        }

        if (order.Count == 0)
        {
            return null;
        }

        int current = order.IndexOf(origin);
        if (current < 0)
        {
            return direction == FocusNavigationDirection.Next ? order[0] : order[^1];
        }

        return FindNextRespectingModes(root, order, current, direction);
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> or any of its ancestors up to the root sets a non-default
    /// <see cref="KeyboardNavigationMode"/>.
    /// </summary>
    internal static bool HasActiveMode(UIElement element)
    {
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (current.GetValue(TabNavigationProperty) != KeyboardNavigationMode.Continue)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the next focusable element from <paramref name="currentIndex"/> respecting the
    /// <see cref="KeyboardNavigationMode"/> of containers in the focus path.
    /// </summary>
    private static UIElement? FindNextRespectingModes(
        UIElement root,
        List<UIElement> order,
        int currentIndex,
        FocusNavigationDirection direction)
    {
        UIElement origin = order[currentIndex];
        UIElement? modeContainer = FindModeContainer(origin, root);
        if (modeContainer is not null)
        {
            KeyboardNavigationMode mode = modeContainer.GetValue(TabNavigationProperty);
            if (mode == KeyboardNavigationMode.None)
            {
                return null;
            }

            List<UIElement> containerTabStops = [];
            foreach (UIElement element in order)
            {
                if (modeContainer.IsAncestorOf(element))
                {
                    containerTabStops.Add(element);
                }
            }

            int indexInContainer = containerTabStops.IndexOf(origin);
            if (mode == KeyboardNavigationMode.Cycle)
            {
                if (containerTabStops.Count == 0)
                {
                    return null;
                }

                if (direction == FocusNavigationDirection.Next)
                {
                    return containerTabStops[(indexInContainer + 1) % containerTabStops.Count];
                }

                return containerTabStops[(indexInContainer - 1 + containerTabStops.Count) % containerTabStops.Count];
            }

            if (mode == KeyboardNavigationMode.Contained)
            {
                if (direction == FocusNavigationDirection.Next)
                {
                    return indexInContainer < containerTabStops.Count - 1 ? containerTabStops[indexInContainer + 1] : null;
                }

                return indexInContainer > 0 ? containerTabStops[indexInContainer - 1] : null;
            }

            if (mode == KeyboardNavigationMode.Once)
            {
                // When moving inside a Once container, moving next or previous leaves the container.
                return FindNextOutsideContainer(order, modeContainer, currentIndex, direction);
            }
        }

        int step = direction == FocusNavigationDirection.Next ? 1 : -1;
        int nextIndex = currentIndex + step;
        while (nextIndex >= 0 && nextIndex < order.Count)
        {
            UIElement candidate = order[nextIndex];
            UIElement? candidateContainer = FindModeContainer(candidate, root);
            if (candidateContainer is not null && !candidateContainer.IsAncestorOf(origin))
            {
                KeyboardNavigationMode candidateMode = candidateContainer.GetValue(TabNavigationProperty);
                if (candidateMode == KeyboardNavigationMode.None)
                {
                    nextIndex = SkipContainer(order, candidateContainer, nextIndex, direction);
                    continue;
                }
            }

            return candidate;
        }

        // Wrap around at the tree boundaries for normal navigation.
        int wrappedIndex = direction == FocusNavigationDirection.Next ? 0 : order.Count - 1;
        while (wrappedIndex != currentIndex && wrappedIndex >= 0 && wrappedIndex < order.Count)
        {
            UIElement candidate = order[wrappedIndex];
            UIElement? candidateContainer = FindModeContainer(candidate, root);
            if (candidateContainer is not null && !candidateContainer.IsAncestorOf(origin))
            {
                KeyboardNavigationMode candidateMode = candidateContainer.GetValue(TabNavigationProperty);
                if (candidateMode == KeyboardNavigationMode.None)
                {
                    wrappedIndex = SkipContainer(order, candidateContainer, wrappedIndex, direction);
                    continue;
                }
            }

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest ancestor of <paramref name="element"/> up to <paramref name="root"/> that specifies a
    /// non-default <see cref="KeyboardNavigationMode"/>.
    /// </summary>
    private static UIElement? FindModeContainer(UIElement element, UIElement root)
    {
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (current.GetValue(TabNavigationProperty) != KeyboardNavigationMode.Continue)
            {
                return current;
            }

            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the next element in <paramref name="order"/> that is outside <paramref name="container"/>.
    /// </summary>
    private static UIElement? FindNextOutsideContainer(
        List<UIElement> order,
        UIElement container,
        int currentIndex,
        FocusNavigationDirection direction)
    {
        int step = direction == FocusNavigationDirection.Next ? 1 : -1;
        int nextIndex = currentIndex + step;
        while (nextIndex >= 0 && nextIndex < order.Count)
        {
            if (!container.IsAncestorOf(order[nextIndex]))
            {
                return order[nextIndex];
            }

            nextIndex += step;
        }

        int wrapped = direction == FocusNavigationDirection.Next ? 0 : order.Count - 1;
        while (wrapped != currentIndex && wrapped >= 0 && wrapped < order.Count)
        {
            if (!container.IsAncestorOf(order[wrapped]))
            {
                return order[wrapped];
            }

            wrapped += step;
        }

        return null;
    }

    /// <summary>
    /// Skips past all elements in <paramref name="order"/> belonging to <paramref name="container"/> in the given direction.
    /// </summary>
    private static int SkipContainer(
        List<UIElement> order,
        UIElement container,
        int startIndex,
        FocusNavigationDirection direction)
    {
        int step = direction == FocusNavigationDirection.Next ? 1 : -1;
        int index = startIndex;
        while (index >= 0 && index < order.Count && container.IsAncestorOf(order[index]))
        {
            index += step;
        }

        return index;
    }

    /// <summary>
    /// Returns the tab stops of the tree in navigation order. Positive <see cref="UIElement.TabIndex"/> values come first
    /// in ascending order, then zero in tree order.
    /// </summary>
    private static List<UIElement> GetTabOrder(UIElement root)
    {
        Dictionary<UIElement, List<Portal>> portalsByAnchor = new(ReferenceEqualityComparer.Instance);
        Stack<UIElement> unvisited = new();
        unvisited.Push(root);
        while (unvisited.TryPop(out UIElement? element))
        {
            if (!IsInNavigationTree(root, element))
            {
                continue;
            }

            if (element is Portal { Anchor: UIElement anchor } portal)
            {
                if (!portalsByAnchor.TryGetValue(anchor, out List<Portal>? portals))
                {
                    portals = [];
                    portalsByAnchor[anchor] = portals;
                }

                portals.Add(portal);
            }

            if (element is View view)
            {
                for (int i = view.Children.Count - 1; i >= 0; i--)
                {
                    unvisited.Push(view.Children[i]);
                }
            }
        }

        List<UIElement> tabStops = [];
        HashSet<Portal> reachedPortals = new(ReferenceEqualityComparer.Instance);
        Stack<(UIElement Element, bool IsSubtreeEnd)> pending = new();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            while (pending.TryPop(out (UIElement Element, bool IsSubtreeEnd) entry))
            {
                if (entry.IsSubtreeEnd)
                {
                    if (portalsByAnchor.Remove(entry.Element, out List<Portal>? portals))
                    {
                        for (int i = portals.Count - 1; i >= 0; i--)
                        {
                            reachedPortals.Add(portals[i]);
                            pending.Push((portals[i], false));
                        }
                    }

                    continue;
                }

                UIElement element = entry.Element;
                if (!IsInNavigationTree(root, element) ||
                    (element is Portal { Anchor: not null } portal && !reachedPortals.Contains(portal)))
                {
                    continue;
                }

                if (IsTabStop(element))
                {
                    tabStops.Add(element);
                }

                pending.Push((element, true));
                if (element is View view)
                {
                    for (int i = view.Children.Count - 1; i >= 0; i--)
                    {
                        pending.Push((view.Children[i], false));
                    }
                }
            }

            foreach (UIElement anchor in portalsByAnchor.Keys)
            {
                pending.Push((anchor, true));
                break;
            }
        }

        List<UIElement> positive = [];
        foreach (UIElement tabStop in tabStops)
        {
            if (tabStop.TabIndex > 0)
            {
                positive.Add(tabStop);
            }
        }

        List<UIElement> order = positive.Count > 1 ? [.. positive.OrderBy(static element => element.TabIndex)] : positive;
        foreach (UIElement tabStop in tabStops)
        {
            if (tabStop.TabIndex == 0)
            {
                order.Add(tabStop);
            }
        }

        return order;
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> and its subtree take part in navigation. A hidden element, a closed
    /// portal that is still drawn during its exit, and a subtree hosted by another visual root do not.
    /// </summary>
    private static bool IsInNavigationTree(UIElement root, UIElement element)
    {
        if (!element.IsVisible || element is Portal { IsOpen: false })
        {
            return false;
        }

        return ReferenceEquals(element.VisualRoot, root);
    }

    /// <summary>
    /// Returns whether Tab stops at <paramref name="element"/>: it can receive focus and its
    /// <see cref="UIElement.TabIndex"/> is not negative.
    /// </summary>
    private static bool IsTabStop(UIElement element)
    {
        return element.TabIndex >= 0 &&
            GetIsTabStop(element) &&
            InputManager.Current.PrimaryKeyboardDevice.CanFocus(element);
    }
}
