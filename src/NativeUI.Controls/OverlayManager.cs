using System.Runtime.CompilerServices;

namespace NativeUI.Controls;

/// <summary>
/// Owns overlay stacking, popup relationships, and dismissal policies for one platform-hosted root.
/// </summary>
internal sealed class OverlayManager : IDisposable
{
    private static readonly ConditionalWeakTable<UIElement, OverlayManager> s_hostedRoots = new();

    private readonly UIElement _root;
    private readonly List<Portal> _stack = [];
    private readonly Dictionary<Portal, Portal?> _parents = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    internal OverlayManager(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        s_hostedRoots.Add(root, this);
    }

    /// <summary>
    /// Resolves the manager owned by the element's attached visual root.
    /// </summary>
    internal static OverlayManager? FindFor(UIElement element)
    {
        if (element.VisualRoot is not UIElement root)
        {
            return null;
        }

        s_hostedRoots.TryGetValue(root, out OverlayManager? manager);
        return manager;
    }

    /// <summary>
    /// Closes every eligible outside-pressed overlay in reverse stacking order.
    /// </summary>
    internal void HandlePointerPressed(UIElement? target)
    {
        if (_disposed)
        {
            return;
        }

        Portal[] overlays = _stack.ToArray();
        List<Portal> dismiss = [];
        for (int i = overlays.Length - 1; i >= 0; i--)
        {
            Portal overlay = overlays[i];
            if (overlay.IsOpen && _parents.ContainsKey(overlay) && overlay.ClosesOnOutsidePress &&
                !ContainsOverlay(overlay, target) && !Contains(overlay.Anchor, target))
            {
                dismiss.Add(overlay);
            }
        }

        foreach (Portal overlay in dismiss)
        {
            if (_parents.ContainsKey(overlay))
            {
                overlay.Close();
            }
        }
    }

    /// <summary>
    /// Places a newly opened or reopened overlay at the top of the stack and refreshes popup relationships.
    /// </summary>
    internal void Register(Portal overlay)
    {
        if (_disposed)
        {
            return;
        }

        _stack.Remove(overlay);
        _stack.Add(overlay);
        _parents[overlay] = null;
        UpdateRelationships();
    }

    /// <summary>
    /// Refreshes parent relationships after an open popup changes its anchor.
    /// </summary>
    internal void UpdateRelationships()
    {
        // Previous links must not reject relationships implied by the current anchors.
        foreach (Portal overlay in _stack)
        {
            _parents[overlay] = null;
        }

        foreach (Portal overlay in _stack)
        {
            _parents[overlay] = FindParent(overlay);
        }
    }

    /// <summary>
    /// Removes an overlay after its exit finishes or it leaves this host.
    /// </summary>
    internal void Unregister(Portal overlay)
    {
        _stack.Remove(overlay);
        _parents.Remove(overlay);
        UpdateRelationships();
    }

    /// <summary>
    /// Closes the registered child popups of a closing parent in reverse stacking order.
    /// </summary>
    internal void CloseChildren(Portal parent)
    {
        Portal[] overlays = _stack.ToArray();
        List<Portal> children = [];
        for (int i = overlays.Length - 1; i >= 0; i--)
        {
            if (IsDescendant(overlays[i], parent))
            {
                children.Add(overlays[i]);
            }
        }

        foreach (Portal child in children)
        {
            if (_parents.ContainsKey(child))
            {
                child.Close();
            }
        }
    }

    /// <summary>
    /// Orders visible overlays by open and reopen order, including overlays completing their exit.
    /// </summary>
    internal void OrderOverlays(List<Portal> overlays)
    {
        List<Portal> ordered = [];
        foreach (Portal overlay in _stack)
        {
            if (overlays.Contains(overlay))
            {
                ordered.Add(overlay);
            }
        }

        int next = 0;
        for (int i = 0; i < overlays.Count; i++)
        {
            if (_parents.ContainsKey(overlays[i]))
            {
                overlays[i] = ordered[next++];
            }
        }
    }

    /// <summary>
    /// Orders popup layout by parent relationships independently of visual stacking.
    /// </summary>
    internal void OrderForLayout(List<Portal> overlays)
    {
        List<Portal> ordered = [];
        foreach (Portal overlay in overlays)
        {
            AddForLayout(overlay, overlays, ordered);
        }

        overlays.Clear();
        overlays.AddRange(ordered);
    }

    private void AddForLayout(Portal overlay, List<Portal> candidates, List<Portal> ordered)
    {
        if (ordered.Contains(overlay))
        {
            return;
        }

        if (_parents.TryGetValue(overlay, out Portal? parent) && parent is not null && candidates.Contains(parent))
        {
            AddForLayout(parent, candidates, ordered);
        }

        ordered.Add(overlay);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Portal[] overlays = _stack.ToArray();
        s_hostedRoots.Remove(_root);
        for (int i = overlays.Length - 1; i >= 0; i--)
        {
            overlays[i].Close();
        }

        _stack.Clear();
        _parents.Clear();
    }

    private Portal? FindParent(Portal overlay)
    {
        Portal? parent = FindContainingOverlay(overlay.Anchor, overlay);
        if (IsValidParent(overlay, parent))
        {
            return parent;
        }

        parent = FindContainingOverlay(overlay.Parent, overlay);
        return IsValidParent(overlay, parent) ? parent : null;
    }

    private bool IsValidParent(Portal overlay, Portal? parent)
    {
        return parent is not null && !ReferenceEquals(overlay, parent) &&
            !Contains(overlay, parent) && !IsDescendant(parent, overlay);
    }

    private Portal? FindContainingOverlay(UIElement? element, Portal? excluded = null)
    {
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (current is Portal portal && !ReferenceEquals(portal, excluded) && _parents.ContainsKey(portal))
            {
                return portal;
            }
        }

        return null;
    }

    private bool ContainsOverlay(Portal overlay, UIElement? element)
    {
        if (Contains(overlay, element))
        {
            return true;
        }

        Portal? containing = FindContainingOverlay(element);
        return containing is not null && IsDescendant(containing, overlay);
    }

    private bool IsDescendant(Portal overlay, Portal ancestor)
    {
        while (_parents.TryGetValue(overlay, out Portal? parent) && parent is not null)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }

            overlay = parent;
        }

        return false;
    }

    private static bool Contains(UIElement? ancestor, UIElement? element)
    {
        return ancestor is not null && element is not null && ancestor.IsAncestorOf(element);
    }
}
