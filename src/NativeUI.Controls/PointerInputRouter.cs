using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Tracks the hovered element path and raises pointer events for one platform host.
/// Hosts supply the hit target and own the pointer position.
/// </summary>
public sealed class PointerInputRouter
{
    private UIElement[] _hoverPath = [];

    /// <summary>
    /// Gets the deepest hovered element, or <see langword="null"/> when no element is hovered.
    /// </summary>
    public UIElement? HoveredElement => _hoverPath.Length > 0 ? _hoverPath[^1] : null;

    /// <summary>
    /// Replaces the hovered path. Hover state and styles are updated for the complete path before
    /// departed elements receive exit events, deepest first, and new elements receive enter events, ancestor first.
    /// </summary>
    /// <param name="target">The topmost visible element under the pointer, or <see langword="null"/> when none is.</param>
    /// <param name="pointerId">The identity of the pointer that moved.</param>
    /// <param name="position">The position relative to the root of the element tree.</param>
    /// <param name="buttons">The buttons currently held.</param>
    /// <param name="modifiers">The keyboard modifiers currently held.</param>
    public void UpdateHover(
        UIElement? target,
        long pointerId,
        Point position,
        PointerButtons buttons = PointerButtons.None,
        PointerModifiers modifiers = PointerModifiers.None)
    {
        UIElement[] departed = _hoverPath;
        UIElement[] entered = CreatePath(target);
        int shared = CountSharedPrefix(departed, entered);
        if (shared == departed.Length && shared == entered.Length)
        {
            return;
        }

        ApplyHoverState(departed, entered, shared);

        for (int i = departed.Length - 1; i >= shared; i--)
        {
            departed[i].RaiseEvent(
                UIElement.PointerExitedEvent,
                new PointerEventArgs(pointerId, position, buttons, modifiers));
        }

        for (int i = shared; i < entered.Length; i++)
        {
            entered[i].RaiseEvent(
                UIElement.PointerEnteredEvent,
                new PointerEventArgs(pointerId, position, buttons, modifiers));
        }
    }

    /// <summary>
    /// Clears the hovered path without raising events, for use when its host stops routing input.
    /// </summary>
    public void Reset()
    {
        UIElement[] departed = _hoverPath;
        if (departed.Length > 0)
        {
            ApplyHoverState(departed, [], 0);
        }
    }

    private void ApplyHoverState(UIElement[] departed, UIElement[] entered, int shared)
    {
        _hoverPath = entered;
        for (int i = shared; i < departed.Length; i++)
        {
            departed[i].SetState(ElementStates.Hover, false);
        }

        for (int i = shared; i < entered.Length; i++)
        {
            entered[i].SetState(ElementStates.Hover, true);
        }

        // Descendant selectors make every element below a changed element depend on its hover state.
        if (shared < departed.Length)
        {
            departed[shared].InvalidateStyle();
        }

        if (shared < entered.Length)
        {
            entered[shared].InvalidateStyle();
        }
    }

    private static UIElement[] CreatePath(UIElement? target)
    {
        int depth = 0;
        for (UIElement? current = target; current is not null; current = current.Parent)
        {
            depth++;
        }

        UIElement[] path = new UIElement[depth];
        UIElement? element = target;
        for (int i = depth - 1; i >= 0; i--)
        {
            path[i] = element!;
            element = element!.Parent;
        }

        return path;
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
