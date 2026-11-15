using System.Collections.ObjectModel;
using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Controls.Shapes;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class TabControlTests
{
    public static void VerifySelectionAndContent()
    {
        using TabControl tabs = new();
        Input input = new() { Value = "retained" };
        TabItem first = new() { Header = "First", Content = input };
        TabItem second = new() { Header = "Second", Content = new Button() };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Same(first, tabs.SelectedItem!);
        Assert.Same(input, tabs.SelectedContent!);
        Assert.True(first.Page.IsVisible);
        Assert.False(second.Page.IsVisible);
        int changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 0;
        Assert.Equal(2, changes);
        Assert.Same(input, first.Page.Children[0]);
        Assert.Equal("retained", input.Value);
        tabs.Items.Remove(first);
        Assert.Same(second, tabs.SelectedItem!);
        Assert.True(first.Page.Parent is null);
        Assert.False(first.IsSelected);
        tabs.Items.Add(first);
        Assert.Same(input, first.Page.Children[0]);
    }

    public static void VerifyKeyboardAndHeaderButtons()
    {
        using TabControl tabs = new();
        TabItem first = new() { Header = "First", Content = new Input() };
        TabItem disabled = new() { Header = "Disabled", IsEnabled = false };
        TabItem hidden = new() { Header = "Hidden", IsVisible = false };
        Button close = new() { Text = "Close" };
        TabItem last = new() { Header = close, Content = new Input() };
        tabs.Items.Add(first);
        tabs.Items.Add(disabled);
        tabs.Items.Add(hidden);
        tabs.Items.Add(last);
        using Window window = new() { Content = tabs };
        Assert.True(first.Focus(FocusState.Keyboard));
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowRight));
        Assert.True(last.IsFocused);
        Assert.Equal(3, tabs.SelectedIndex);
        Assert.False(KeyboardNavigation.GetIsTabStop(first));
        Assert.True(KeyboardNavigation.GetIsTabStop(last));
        last.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowRight));
        Assert.True(first.IsFocused);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(-1, tabs.IndexOfItemElement(close));
        close.PerformClick();
        Assert.Equal(0, tabs.SelectedIndex);
        tabs.TabStripPlacement = Dock.Left;
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.True(last.IsFocused);
        last.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Home));
        Assert.True(first.IsFocused);
        first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.End));
        Assert.True(last.IsFocused);
        Assert.Same(last.Page.Children[0], KeyboardNavigation.GetNext(tabs, last, FocusNavigationDirection.Next)!);
    }

    public static void VerifyTemplatesAndCollectionChanges()
    {
        ObservableCollection<string> source = ["One", "Two"];
        int headers = 0;
        int pages = 0;
        using TabControl tabs = new()
        {
            ItemTemplate = new DataTemplate<string>(value =>
            {
                headers++;
                return new Text { Value = value };
            }),
            ContentTemplate = new DataTemplate<string>(value =>
            {
                pages++;
                return new Input { Value = value };
            }),
            ItemsSource = source
        };
        TabItem first = (TabItem)tabs.GetItemElement(0)!;
        UIElement page = first.Page.Children[0];
        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 0;
        Assert.Equal(2, headers);
        Assert.Equal(2, pages);
        Assert.Same(page, first.Page.Children[0]);
        source.Move(0, 1);
        Assert.Same(first, tabs.GetItemElement(1)!);
        Assert.Equal(1, tabs.SelectedIndex);
        source.Remove("One");
        Assert.True(first.Page.Parent is null);
        source.Add("Three");
        Assert.Equal(2, tabs.Items.Count);
        tabs.ItemTemplate = new DataTemplate<string>(value => new Text { Value = "Tab " + value });
        Assert.True(((TabItem)tabs.GetItemElement(tabs.SelectedIndex)!).IsSelected);
        Assert.True(((TabItem)tabs.GetItemElement(tabs.SelectedIndex)!).Page.IsVisible);
    }

    public static void VerifyCompiledSample()
    {
        using TabSampleView sample = new();
        TabControl tabs = sample.EditorTabs;
        TabItem first = (TabItem)tabs.GetItemElement(0)!;
        StackPanel page = (StackPanel)first.Page.Children[0];
        Input editor = (Input)page.Children[1];
        TabDocument document = (TabDocument)tabs.Items[0]!;
        Assert.Equal("Settings document", editor.Value);
        editor.Value = "Edited";
        Assert.Equal("Edited", document.Text);
        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 0;
        Assert.Same(page, first.Page.Children[0]);
        Assert.Equal("Edited", editor.Value);
        StackPanel header = (StackPanel)((View)first.Children[0]).Children[0];
        Button close = (Button)header.Children[1];
        Assert.Equal("Close tab", close.Text);
        Assert.False(KeyboardNavigation.GetIsTabStop(close));
        Assert.True(close.Children[0] is NativeUI.Controls.Shapes.Path);
        NativeUI.Controls.Shapes.Path icon = (NativeUI.Controls.Shapes.Path)close.Children[0];
        Assert.True(icon.Data is PathGeometry);
        Assert.Same(SvgCurrentColorBrush.Instance, icon.Stroke!);
        close.PerformClick();
        Assert.Equal(1, tabs.Items.Count);
        Assert.Equal("App.xaml", ((TabDocument)tabs.SelectedItem!).Title);
    }

    public static void VerifyAlignmentAndAnimatedVerticalTabs()
    {
        AnimationClock.AttachDriver();
        try
        {
            using TabSampleView sample = new();
            using Window window = new() { Content = sample, Width = 800, Height = 1100 };
            window.RenderFrame()?.Dispose();
            window.RenderFrame()?.Dispose();
            foreach (StackPanel section in sample.Children.OfType<StackPanel>())
            {
                Text heading = (Text)section.Children[0];
                TabControl control = (TabControl)section.Children[1];
                Assert.True(control.Bounds.Y - heading.Bounds.Bottom >= 12, "Each heading needs a 12px gap before its tabs.");
            }

            TabControl segmented = (TabControl)((StackPanel)sample.Children.OfType<StackPanel>().ElementAt(1)).Children[1];
            Assert.True(segmented.Strip!.ActualHeight <= 34, "Segmented headers should remain compact.");
            TabItem editorTab = (TabItem)sample.EditorTabs.GetItemElement(0)!;
            StackPanel editorHeader = (StackPanel)((View)editorTab.Children[0]).Children[0];
            Text title = (Text)editorHeader.Children[0];
            Button close = (Button)editorHeader.Children[1];
            UIElement icon = close.Children[0];
            double titleCenter = title.Bounds.Y + title.ActualHeight / 2;
            double iconCenter = icon.Bounds.Y + icon.ActualHeight / 2;
            Assert.True(Math.Abs(titleCenter - iconCenter) < 0.01, $"Title center {titleCenter}, icon center {iconCenter}.");

            TabControl tabs = sample.VerticalTabs;
            TabItem first = (TabItem)tabs.GetItemElement(0)!;
            TabItem second = (TabItem)tabs.GetItemElement(1)!;
            Assert.Equal(Dock.Left, tabs.TabStripPlacement);
            Assert.True(second.Bounds.Y > first.Bounds.Y, "Vertical tab order.");
            Assert.True(first.Page.Bounds.X > tabs.Strip!.Bounds.Right, "Page must follow strip.");
            Assert.True(first.Page.ActualWidth > tabs.Strip.ActualWidth, "Page must fill remaining width.");
            View indicator = tabs.Indicator!;
            double before = indicator.ComputedStyle.Top ?? 0;
            tabs.SelectedIndex = 1;
            Assert.True(indicator.HasRunningAnimations, "Selection must start an indicator transition.");
            double target = indicator.Top ?? throw new InvalidOperationException("Missing indicator target.");
            Assert.True(target > before, "New indicator target.");
            AnimationClock.Tick(sample, AnimationClock.Now + 90);
            double during = indicator.ComputedStyle.Top ?? 0;
            Assert.True(during > before && during < target, $"Indicator position {during} must be between {before} and {target}.");
            AnimationClock.Tick(sample, AnimationClock.Now + 500);
            Assert.Equal(target, indicator.ComputedStyle.Top);
            Assert.False(indicator.HasRunningAnimations);
            Assert.True(first.Focus(FocusState.Keyboard));
            first.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
            Assert.True(second.IsFocused);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally
        {
            AnimationClock.DetachDriver();
        }
    }

    public static void VerifyIndicatorAndPlacement()
    {
        using TabControl tabs = new();
        TabItem first = new() { Header = "Short" };
        TabItem second = new() { Header = "A longer header" };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        StackPanel headers = tabs.Strip!.Children.OfType<StackPanel>().Single();
        Assert.Equal(Position.Relative, headers.ComputedStyle.Position);
        Assert.Equal(1, headers.ComputedStyle.ZIndex);
        tabs.Strip!.UpdateRenderedBounds(new Rect(10, 20, 400, 40));
        first.UpdateRenderedBounds(new Rect(10, 20, 80, 40));
        second.UpdateRenderedBounds(new Rect(90, 20, 160, 40));
        tabs.SelectedIndex = 1;
        Assert.Equal(80d, tabs.Indicator!.Left);
        Assert.Equal(WidthValue.Pixels(160), tabs.Indicator.Width);
        second.UpdateRenderedBounds(new Rect(110, 20, 180, 40));
        Assert.Equal(100d, tabs.Indicator.Left);
        Assert.Equal(WidthValue.Pixels(180), tabs.Indicator.Width);
        tabs.TabStripPlacement = Dock.Right;
        second.UpdateRenderedBounds(new Rect(10, 70, 180, 40));
        Assert.Equal(50d, tabs.Indicator.Top);
        Assert.Equal(40d, tabs.Indicator.Height);
        Assert.True(tabs.Indicator.Left is null);
        second.IsEnabled = false;
        Assert.Equal(0, tabs.SelectedIndex);
        tabs.Items.Clear();
        Assert.False(tabs.Indicator.IsVisible);
        Assert.True(second.Page.Parent is null);
        second.Width = WidthValue.Pixels(200);
        Assert.Equal(-1, tabs.SelectedIndex);
    }
}
