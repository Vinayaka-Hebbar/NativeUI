using System.Windows.Input;
using NativeUI.Core;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
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
    public static void VerifyLayoutFitsText()
    {
        Button button = new() { Text = "Switch to Dark Mode", Style = new Style { Padding = new Thickness(10, 20, 10, 20) } };
        Text text = new() { Value = "Switch to Dark Mode" };
        View root = new();
        root.Children.Add(button);
        root.Children.Add(text);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(600, 200));
        LayoutBox buttonBox = layout.Children[0];

        Assert.Equal(buttonBox.TextLayout!.Size.Width + 40, buttonBox.BorderBox.Width);
        Assert.True(buttonBox.BorderBox.Width < 600, "A button must not fill the available width.");
        Assert.Equal(1, buttonBox.TextLayout.Lines.Length);
        Assert.Equal(600.0, layout.Children[1].BorderBox.Width);
    }

    public static void VerifyFitContentWidth()
    {
        Button styled = new() { Text = "Toggle", Style = new Style { Width = 300 } };
        Button narrow = new() { Text = "A long label that has to wrap", Style = new Style { Margin = new Thickness(0, 5, 0, 5) } };
        Button withChild = new();
        withChild.Children.Add(new Text { Value = "Toggle", Style = new Style { Margin = new Thickness(0, 4, 0, 4) } });
        View fitView = new() { Style = new Style { Width = WidthValue.FitContent, Padding = new Thickness(0, 3, 0, 3) } };
        fitView.Children.Add(new Text { Value = "Toggle" });
        View root = new();
        root.Children.Add(styled);
        root.Children.Add(narrow);
        root.Children.Add(withChild);
        root.Children.Add(fitView);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(100, 400));
        double toggleWidth = SkiaTextShaper.Shared.MeasureText("Toggle", new Size(double.PositiveInfinity, double.PositiveInfinity), withChild.ComputedStyle).Width;

        Assert.Equal(300.0, layout.Children[0].BorderBox.Width);
        Assert.Equal(90.0, layout.Children[1].BorderBox.Width);
        Assert.True(layout.Children[1].TextLayout!.Lines.Length > 1, "Fit-content text must wrap at the available width.");
        Assert.Equal(toggleWidth + 8, layout.Children[2].BorderBox.Width);
        Assert.Equal(toggleWidth + 6, layout.Children[3].BorderBox.Width);
    }

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
