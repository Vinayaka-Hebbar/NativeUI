namespace NativeUI.Controls;

/// <summary>
/// Computes the keyboard navigation order of the tree that a <see cref="FocusManager"/> owns, and finds the element that
/// a <see cref="FocusNavigationDirection"/> moves focus to.
/// </summary>
/// <remarks>
/// The order holds the focusable elements with a non-negative <see cref="UIElement.TabIndex"/>: positive values first in
/// ascending order, then zero in tree order, as in HTML. Hidden subtrees, subtrees with their own focus manager, and
/// portals that are closed are skipped. The elements of an open portal with an anchor follow the anchor's subtree instead
/// of the portal's own position. Tab moves from a popup's target into the popup.
/// </remarks>
internal static class KeyboardNavigation
{
    /// <summary>
    /// Returns the element that focus moves to from <paramref name="origin"/> in <paramref name="direction"/>, or
    /// <see langword="null"/> when there is none. <see cref="FocusNavigationDirection.Next"/> and
    /// <see cref="FocusNavigationDirection.Previous"/> wrap at either end and start at an end when
    /// <paramref name="origin"/> is not in the order. <see cref="FocusNavigationDirection.First"/> and
    /// <see cref="FocusNavigationDirection.Last"/> look only inside <paramref name="origin"/>.
    /// </summary>
    /// <param name="focusManager">The focus manager that owns the tree.</param>
    /// <param name="origin">The element that focus moves from, or the element to search inside.</param>
    /// <param name="direction">The direction to move focus.</param>
    public static UIElement? GetNext(FocusManager focusManager, UIElement origin, FocusNavigationDirection direction)
    {
        List<UIElement> order = GetTabOrder(focusManager);
        if (direction is FocusNavigationDirection.First or FocusNavigationDirection.Last)
        {
            List<UIElement> inside = [];
            foreach (UIElement element in order)
            {
                if (IsInclusiveAncestor(origin, element))
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
        if (direction == FocusNavigationDirection.Next)
        {
            return current < 0 ? order[0] : order[(current + 1) % order.Count];
        }

        return current < 0 ? order[^1] : order[(current - 1 + order.Count) % order.Count];
    }

    /// <summary>
    /// Returns whether <paramref name="ancestor"/> is <paramref name="element"/> or one of its ancestors.
    /// </summary>
    public static bool IsInclusiveAncestor(UIElement ancestor, UIElement element)
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
    /// Returns the tab stops of the tree in navigation order. Positive <see cref="UIElement.TabIndex"/> values come first
    /// in ascending order, then zero in tree order.
    /// </summary>
    private static List<UIElement> GetTabOrder(FocusManager focusManager)
    {
        UIElement root = focusManager.Root;
        Dictionary<UIElement, List<Portal>> portalsByAnchor = new(ReferenceEqualityComparer.Instance);
        Stack<UIElement> unvisited = new();
        unvisited.Push(root);
        while (unvisited.TryPop(out UIElement? element))
        {
            if (!IsInNavigationTree(focusManager, element))
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

        // Each entry is an element to visit, or the end of an element's subtree, where its anchored portals follow.
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
                if (!IsInNavigationTree(focusManager, element) ||
                    (element is Portal { Anchor: not null } portal && !reachedPortals.Contains(portal)))
                {
                    continue;
                }

                if (IsTabStop(focusManager, element))
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

            // A portal whose anchor is not in the navigation tree follows every other element.
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

        // A stable sort keeps tree order among equal positive values.
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
    /// portal that is still drawn during its exit, and a subtree with its own focus manager do not.
    /// </summary>
    private static bool IsInNavigationTree(FocusManager focusManager, UIElement element)
    {
        if (!element.IsVisible || element is Portal { IsOpen: false })
        {
            return false;
        }

        return element.FocusManager is null || ReferenceEquals(element, focusManager.Root);
    }

    /// <summary>
    /// Returns whether Tab stops at <paramref name="element"/>: it can receive focus and its
    /// <see cref="UIElement.TabIndex"/> is not negative.
    /// </summary>
    private static bool IsTabStop(FocusManager focusManager, UIElement element)
    {
        return element.TabIndex >= 0 && focusManager.CanFocus(element);
    }
}
