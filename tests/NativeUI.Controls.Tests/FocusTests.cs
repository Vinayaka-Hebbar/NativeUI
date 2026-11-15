using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class FocusTests
{
    public static void VerifyKeyboardNavigationAndPointerFocus()
    {
        using View root = new() { Width = 300, Height = 200 };
        Button first = new() { Width = 80, Height = 40 };
        Button second = new() { Width = 80, Height = 40 };
        root.Children.Add(first);
        root.Children.Add(second);
        Assert.True(root.IsAncestorOf(root));
        Assert.True(root.IsAncestorOf(first));
        Assert.False(first.IsAncestorOf(root));
        Assert.False(first.IsAncestorOf(second));
        Assert.False(first.Focus());
        using Window window = new() { Width = 300, Height = 200, Content = root };
        KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;
        List<string> events = [];
        first.GotFocus += (_, _) => events.Add("first got");
        first.LostFocus += (_, _) => events.Add("first lost");
        second.GotFocus += (_, _) => events.Add("second got");
        int keys = 0;
        second.KeyDown += (_, _) => keys++;

        Assert.True(first.Focus(FocusState.Pointer));
        Assert.False(first.States.HasFlag(ElementStates.FocusVisible));
        Assert.True(window.HandleKeyDownAsync("\t", commandOrControl: false).GetAwaiter().GetResult());
        Assert.Same(second, keyboard.FocusedElement ?? throw new InvalidOperationException("Missing focus."));
        Assert.True(second.States.HasFlag(ElementStates.FocusVisible));
        Assert.True(root.States.HasFlag(ElementStates.FocusWithin));
        window.HandleKeyDownAsync("x", commandOrControl: false).GetAwaiter().GetResult();
        Assert.Equal(1, keys);
        Assert.Equal("first got", events[0]);
        Assert.Equal("first lost", events[1]);
        Assert.Equal("second got", events[2]);

        window.RenderFrame()?.Dispose();
        window.HandlePointerPressed(new Point(20, 20));
        Assert.True(first.IsFocused);
        Assert.Equal(FocusState.Pointer, first.FocusState);
        Assert.False(first.States.HasFlag(ElementStates.FocusVisible));
        KeyboardNavigation.SetTabNavigation(root, KeyboardNavigationMode.Cycle);
        Assert.True(first.MoveFocus(FocusNavigationDirection.Previous));
        Assert.True(second.IsFocused);
    }

    public static void VerifyLogicalScopesAndDetach()
    {
        using View root = new();
        View firstScope = new();
        View secondScope = new();
        Button first = new();
        Button second = new();
        firstScope.Children.Add(first);
        secondScope.Children.Add(second);
        root.Children.Add(firstScope);
        root.Children.Add(secondScope);
        FocusManager.SetIsFocusScope(firstScope, true);
        FocusManager.SetIsFocusScope(secondScope, true);
        using Window window = new() { Content = root };
        KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;

        Assert.True(first.Focus(FocusState.Keyboard));
        Assert.True(second.Focus(FocusState.Keyboard));
        Assert.Same(firstScope, FocusManager.GetFocusScope(first));
        Assert.Same(first, FocusManager.GetFocusedElement(firstScope) ?? throw new InvalidOperationException("Missing logical focus."));
        Assert.Same(second, FocusManager.GetFocusedElement(secondScope) ?? throw new InvalidOperationException("Missing logical focus."));
        Assert.False(first.IsFocused);
        keyboard.ClearFocus();
        Assert.Null(keyboard.FocusedElement);
        Assert.Same(second, FocusManager.GetFocusedElement(secondScope) ?? throw new InvalidOperationException("Logical focus was lost."));

        FocusManager.SetFocusedElement(firstScope, null);
        FocusManager.SetFocusedElement(firstScope, first);
        Assert.True(first.IsFocused);
        firstScope.Children.Remove(first);
        Assert.Null(keyboard.FocusedElement);
        Assert.Null(FocusManager.GetFocusedElement(firstScope));
        Assert.False(root.States.HasFlag(ElementStates.FocusWithin));
        Assert.False(first.Focus());
        second.Dispose();
        Assert.Null(FocusManager.GetFocusedElement(secondScope));
    }

    public static void VerifyHostedRootIsolation()
    {
        using View root = new();
        Button first = new();
        Dialog dialog = new();
        Button second = new();
        dialog.Children.Add(second);
        root.Children.Add(first);
        root.Children.Add(dialog);
        using Window window = new() { Content = root };
        dialog.Show();
        using Window dialogWindow = new() { Content = dialog };
        KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;

        Assert.True(first.Focus(FocusState.Keyboard));
        Assert.True(second.Focus(FocusState.Keyboard));
        Assert.False(first.IsFocused);
        Assert.False(root.States.HasFlag(ElementStates.FocusWithin));
        Assert.True(dialog.States.HasFlag(ElementStates.FocusWithin));
        Assert.Same(dialog, FocusManager.GetFocusScope(second));
        window.Content = null;
        Assert.Same(second, keyboard.FocusedElement ?? throw new InvalidOperationException("Dialog focus was cleared."));
        dialogWindow.Content = null;
        Assert.Null(keyboard.FocusedElement);
        Assert.False(second.IsFocused);
    }

    public static void VerifyPlatformFocusAuthority()
    {
        using View root = new();
        Button target = new();
        root.Children.Add(target);
        using Window window = new() { Content = root };
        KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;
        DeferredFocusPeer peer = new(target);
        target.NativeHandle = peer;

        Assert.False(target.Focus(FocusState.Keyboard));
        Assert.Equal(1, peer.Requests);
        Assert.Null(keyboard.FocusedElement);
        keyboard.HandleGotFocus(target, FocusState.Programmatic);
        Assert.True(target.IsFocused);
        Assert.True(target.States.HasFlag(ElementStates.FocusVisible));
        root.Children.Remove(target);
        Assert.Null(keyboard.FocusedElement);
        Assert.Equal(1, peer.Blurs);
        keyboard.HandleGotFocus(target, FocusState.Programmatic);
        Assert.Null(keyboard.FocusedElement);
        target.Dispose();
    }

    public static void VerifyParentChangeLifecycle()
    {
        using View firstRoot = new();
        using View secondRoot = new();
        using Window firstWindow = new() { Content = firstRoot };
        using Window secondWindow = new() { Content = secondRoot };
        Button target = new();
        target.ParentChanged += (_, _) =>
        {
            if (target.Parent?.VisualRoot is not null)
            {
                Assert.Same(target.Parent.VisualRoot ?? throw new InvalidOperationException("Missing parent root."),
                    target.VisualRoot ?? throw new InvalidOperationException("Parent notification preceded attachment."));
                Assert.True(target.Focus());
            }
        };
        firstRoot.Children.Add(target);
        Assert.True(target.IsFocused);
        int losses = 0;
        target.LostFocus += (_, _) => losses++;
        firstRoot.Children.Remove(target);
        Assert.Equal(1, losses);
        Assert.Null(target.VisualRoot);

        View subtree = new();
        subtree.Children.Add(target);
        firstRoot.Children.Add(subtree);
        Assert.True(target.Focus());
        bool redirected = false;
        target.LostFocus += (_, _) =>
        {
            if (!redirected && subtree.Parent is null)
            {
                redirected = true;
                secondRoot.Children.Add(subtree);
                Assert.True(target.Focus());
            }
        };
        firstRoot.Children.Remove(subtree);
        Assert.Same(secondRoot, subtree.Parent ?? throw new InvalidOperationException("Reparenting was overwritten."));
        Assert.Same(secondRoot, target.VisualRoot ?? throw new InvalidOperationException("New attachment was lost."));
        Assert.True(target.IsFocused);
        Assert.False(firstRoot.States.HasFlag(ElementStates.FocusWithin));
        secondRoot.Children.Remove(subtree);
    }

    public static void VerifyChildOwnershipValidation()
    {
        using View root = new();
        using View other = new();
        View child = new();
        View replacement = new();
        root.Children.Add(child);
        other.Children.Add(replacement);
        Reject(() => other.Children.Add(child));
        Reject(() => root.Children.Add(child));
        Reject(() => child.Children.Add(root));
        Reject(() => root.Children.Add(root));
        Reject(() => root.Children[0] = replacement);
        Assert.Equal(1, root.Children.Count);
        Assert.Equal(1, other.Children.Count);
        Assert.Same(child, root.Children[0]);
        Assert.Same(replacement, other.Children[0]);
        Assert.Same(root, child.Parent ?? throw new InvalidOperationException("Original parent was lost."));
        root.Children[0] = child;
        root.Children.Remove(child);
        other.Children.Add(child);
        Assert.Same(other, child.Parent ?? throw new InvalidOperationException("Explicit reparenting failed."));

        View removedFirst = new();
        View addedDuringClear = new();
        root.Children.Add(removedFirst);
        root.Children.Add(new View());
        removedFirst.ParentChanged += (_, _) =>
        {
            if (removedFirst.Parent is null)
            {
                root.Children.Add(addedDuringClear);
            }
        };
        root.Children.Clear();
        Assert.Equal(1, root.Children.Count);
        Assert.Same(root, addedDuringClear.Parent ?? throw new InvalidOperationException("Reentrant insertion was lost."));
        root.Children.Clear();
        Assert.Equal(0, root.Children.Count);
        Assert.Null(addedDuringClear.Parent);
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert.True(rejected);
    }

    private sealed class DeferredFocusPeer(UIElement element) : INativeHandle, IFocusPeer
    {
        internal int Requests { get; private set; }

        internal int Blurs { get; private set; }

        public void Focus() => Requests++;

        public void Blur()
        {
            Blurs++;
            InputManager.Current.PrimaryKeyboardDevice.HandleLostFocus(element);
        }

        public void Dispose()
        {
        }
    }
}
