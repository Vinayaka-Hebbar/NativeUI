using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class EnabledStateTests
{
    public static void VerifyInitialAndCommandState()
    {
        using RestrictedView view = new(false);
        Assert.False(view.IsEffectivelyEnabled);
        Assert.True(view.States.HasFlag(ElementStates.Disabled));
        view.AllowInput = true;
        Assert.True(view.IsEffectivelyEnabled);
        Assert.False(view.States.HasFlag(ElementStates.Disabled));

        TestCommand command = new();
        using MenuItem item = new() { Command = command };
        Assert.True(item.IsEnabled);
        Assert.False(item.IsEffectivelyEnabled);
        item.IsEnabled = false;
        command.AllowExecution = true;
        Assert.False(item.IsEnabled);
        Assert.False(item.IsEffectivelyEnabled);
        item.IsEnabled = true;
        Assert.True(item.IsEffectivelyEnabled);
        command.AllowExecution = false;
        Assert.False(item.IsEffectivelyEnabled);
        item.Command = null;
        Assert.True(item.IsEffectivelyEnabled);
    }

    public static void VerifyNotificationReentrancy()
    {
        using View root = new();
        using NativeUI.Controls.MacOS.Window host = new() { Content = root };
        KeyboardDevice manager = InputManager.Current.PrimaryKeyboardDevice;
        Button child = new();
        Portal portal = new();
        MenuItem item = new() { Content = "Item" };
        portal.Children.Add(item);
        root.Children.Add(child);
        root.Children.Add(portal);
        portal.Show();
        List<bool> rootChanges = [];
        List<bool> itemChanges = [];
        item.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.IsEffectivelyEnabledProperty) && args.NewValue is bool isEnabled)
            {
                itemChanges.Add(isEnabled);
            }
        };
        root.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.IsEffectivelyEnabledProperty) && args.NewValue is bool isEnabled)
            {
                rootChanges.Add(isEnabled);
                if (!isEnabled)
                {
                    root.IsEnabled = true;
                }
            }
        };
        Assert.True(item.Focus(FocusState.Keyboard));

        root.IsEnabled = false;

        Assert.Equal(2, rootChanges.Count);
        Assert.False(rootChanges[0]);
        Assert.True(rootChanges[1]);
        Assert.Equal(2, itemChanges.Count);
        Assert.False(itemChanges[0]);
        Assert.True(itemChanges[1]);
        Assert.Null(manager.FocusedElement);
        Assert.True(root.IsEffectivelyEnabled);
        Assert.True(child.IsEffectivelyEnabled);
        Assert.True(item.IsEffectivelyEnabled);
        Assert.False(root.States.HasFlag(ElementStates.Disabled));
        Assert.False(item.States.HasFlag(ElementStates.Disabled));
        Assert.False(item.Children[1].States.HasFlag(ElementStates.Disabled));
    }

    public static void VerifyFocusLossReentrancy()
    {
        using View root = new();
        using NativeUI.Controls.MacOS.Window host = new() { Content = root };
        KeyboardDevice manager = InputManager.Current.PrimaryKeyboardDevice;
        Button child = new();
        root.Children.Add(child);
        child.LostFocus += (_, _) => root.IsEnabled = true;
        Assert.True(child.Focus(FocusState.Keyboard));

        root.IsEnabled = false;

        Assert.Null(manager.FocusedElement);
        Assert.True(root.IsEffectivelyEnabled);
        Assert.True(child.IsEffectivelyEnabled);
        Assert.False(root.States.HasFlag(ElementStates.Disabled));
        Assert.False(child.States.HasFlag(ElementStates.Disabled));
    }

    public static void VerifyGenericRoutingAndReleaseCleanup()
    {
        using Button button = new();
        int releases = 0;
        int keys = 0;
        button.PointerReleased += (_, _) => releases++;
        button.KeyDown += (_, _) => keys++;
        button.RaiseEvent(UIElement.PointerPressedEvent, new PointerEventArgs(1, default, PointerButtons.Primary));
        Assert.True(button.States.HasFlag(ElementStates.Pressed));
        button.IsEnabled = false;
        Assert.False(button.States.HasFlag(ElementStates.Pressed));

        button.RaiseEvent(UIElement.PointerReleasedEvent, new PointerEventArgs(1, default));
        button.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Enter));

        Assert.Equal(1, releases);
        Assert.Equal(1, keys);
    }

    private sealed class RestrictedView : View
    {
        private bool _allowInput;

        public RestrictedView(bool allowInput)
        {
            _allowInput = allowInput;
            UpdateEffectiveEnabledState();
        }

        public bool AllowInput
        {
            set
            {
                _allowInput = value;
                UpdateEffectiveEnabledState();
            }
        }

        protected override bool IsEnabledCore => _allowInput;
    }

    private sealed class TestCommand : ICommand
    {
        private bool _allowExecution;

        public bool AllowExecution
        {
            set
            {
                _allowExecution = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _allowExecution;

        public void Execute(object? parameter)
        {
        }
    }
}
