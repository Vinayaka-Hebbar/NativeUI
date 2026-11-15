using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Stores logical focus in focus scopes independently of the keyboard device's current focus.
/// </summary>
public static class FocusManager
{
    /// <summary>
    /// Identifies whether an element defines a logical focus scope.
    /// </summary>
    public static readonly BindableProperty<bool> IsFocusScopeProperty =
        BindableProperty.CreateAttached<bool>("IsFocusScope", typeof(FocusManager));

    /// <summary>
    /// Identifies the element that retains logical focus within a scope.
    /// </summary>
    public static readonly BindableProperty<UIElement?> FocusedElementProperty =
        BindableProperty.CreateAttached<UIElement?>("FocusedElement", typeof(FocusManager),
            propertyChanged: static (scope, _, element) =>
            {
                KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;
                if (scope is UIElement owner && element is not null &&
                    ReferenceEquals(owner.VisualRoot, element.VisualRoot) &&
                    (keyboard.FocusedElement is null || ReferenceEquals(keyboard.FocusedElement.VisualRoot, element.VisualRoot)) &&
                    !ReferenceEquals(keyboard.FocusedElement, element))
                {
                    keyboard.Focus(element, FocusState.Programmatic);
                }
            });

    /// <summary>
    /// Gets whether the element defines a logical focus scope.
    /// </summary>
    public static bool GetIsFocusScope(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(IsFocusScopeProperty);
    }

    /// <summary>
    /// Sets whether the element defines a logical focus scope.
    /// </summary>
    public static void SetIsFocusScope(UIElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsFocusScopeProperty, value);
    }

    /// <summary>
    /// Gets logical focus for a scope, discarding references to elements that left its hosted root.
    /// </summary>
    public static UIElement? GetFocusedElement(UIElement scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        UIElement? element = scope.GetValue(FocusedElementProperty);
        if (element is not null && !ReferenceEquals(scope.VisualRoot, element.VisualRoot))
        {
            scope.SetValue(FocusedElementProperty, null);
            return null;
        }

        return element;
    }

    /// <summary>
    /// Sets the element retaining logical focus in the specified scope.
    /// </summary>
    public static void SetFocusedElement(UIElement scope, UIElement? element)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.SetValue(FocusedElementProperty, element);
    }

    /// <summary>
    /// Returns the nearest declared focus scope, stopping at the hosted visual root.
    /// Hosted roots are implicit focus scopes.
    /// </summary>
    public static UIElement GetFocusScope(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        UIElement? root = element.VisualRoot;
        UIElement scope = element;
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            scope = current;
            if (GetIsFocusScope(current) || ReferenceEquals(current, root))
            {
                break;
            }
        }

        return scope;
    }

    /// <summary>
    /// Clears logical focus references to a subtree before it leaves its visual root.
    /// </summary>
    internal static void ClearFocus(UIElement subtree, UIElement? parent)
    {
        for (UIElement? current = subtree; current is not null; current = ReferenceEquals(current, subtree) ? parent : current.Parent)
        {
            if (current.GetValue(FocusedElementProperty) is UIElement focused &&
                subtree.IsAncestorOf(focused))
            {
                current.SetValue(FocusedElementProperty, null);
            }

            if (ReferenceEquals(current, subtree.VisualRoot))
            {
                break;
            }
        }
    }
}
