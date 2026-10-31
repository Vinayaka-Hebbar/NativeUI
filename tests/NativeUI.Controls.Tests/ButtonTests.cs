using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies <see cref="Button"/> behavior including pointer lifecycle, click events,
/// commands, <see cref="ElementStates"/>, and cursor styling.
/// </summary>
internal static class ButtonTests
{
    internal static void VerifyPointerLifecycleAndClick()
    {
        using Button button = new()
        {
            Text = "Click Me"
        };

        bool clicked = false;
        button.Click += (sender, args) =>
        {
            clicked = true;
            Assert.Same(button, sender!);
        };

        Assert.False(button.States.HasFlag(ElementStates.Pressed));
        Assert.Equal(Cursor.Pointer, button.Cursor);

        // Simulate PointerPressed
        button.RaiseEvent(UIElement.PointerPressedEvent, new PointerEventArgs(1, new Layout.Point(10, 10)));
        Assert.True(button.States.HasFlag(ElementStates.Pressed));
        Assert.False(clicked);

        // Simulate hover and PointerReleased
        button.SetState(ElementStates.Hover, true);
        button.RaiseEvent(UIElement.PointerReleasedEvent, new PointerEventArgs(1, new Layout.Point(10, 10)));
        Assert.False(button.States.HasFlag(ElementStates.Pressed));
        Assert.True(clicked);
    }

    internal static void VerifyPointerExitCancelsPressed()
    {
        using Button button = new()
        {
            Text = "Cancelable"
        };

        bool clicked = false;
        button.Click += (_, _) => clicked = true;

        button.RaiseEvent(UIElement.PointerPressedEvent, new PointerEventArgs(1, new Layout.Point(10, 10)));
        Assert.True(button.States.HasFlag(ElementStates.Pressed));

        // Pointer moves outside button before release
        button.RaiseEvent(UIElement.PointerExitedEvent, new PointerEventArgs(1, new Layout.Point(500, 500)));
        Assert.False(button.States.HasFlag(ElementStates.Pressed));

        button.RaiseEvent(UIElement.PointerReleasedEvent, new PointerEventArgs(1, new Layout.Point(500, 500)));
        Assert.False(clicked);
    }

    internal static void VerifyCommandExecutionAndCanExecute()
    {
        TestCommand command = new(canExecute: true);
        using Button button = new()
        {
            Command = command,
            CommandParameter = "TestParam"
        };

        Assert.True(button.IsEnabled);
        Assert.False(button.States.HasFlag(ElementStates.Disabled));

        button.PerformClick();
        Assert.True(command.Executed);
        Assert.Equal("TestParam", command.LastParameter);

        // Disable command and verify IsEnabled updates
        command.SetCanExecute(false);
        Assert.False(button.IsEnabled);
        Assert.True(button.States.HasFlag(ElementStates.Disabled));

        command.Executed = false;
        button.PerformClick();
        Assert.False(command.Executed);
    }

    internal static void VerifyCssCursorStyling()
    {
        StyleSheet sheet = new()
        {
            Rules =
            {
                StyleRule.Selector(".btn-not-allowed")
                    .Style(new Style { Cursor = Cursor.NotAllowed })
            }
        };

        using View container = new()
        {
            StyleSheet = sheet
        };

        Button btn = new()
        {
            Class = ["btn-not-allowed"]
        };
        container.Children.Add(btn);

        Button defaultBtn = new();
        Assert.Equal(Cursor.Pointer, defaultBtn.Cursor);

        Assert.Equal(Cursor.NotAllowed, btn.ComputedStyle.Cursor);
        Assert.Equal(Cursor.NotAllowed, btn.Cursor);
    }

    private sealed class TestCommand : ICommand
    {
        private bool _canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool Executed { get; set; }
        public object? LastParameter { get; private set; }

        public TestCommand(bool canExecute)
        {
            _canExecute = canExecute;
        }

        public void SetCanExecute(bool canExecute)
        {
            _canExecute = canExecute;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object? parameter) => _canExecute;

        public void Execute(object? parameter)
        {
            Executed = true;
            LastParameter = parameter;
        }
    }
}
