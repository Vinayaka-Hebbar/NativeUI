using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class MenuItemTests
{
    public static void VerifyRowLayout()
    {
        using StackPanel menu = new() { Width = 220 };
        MenuItem plain = new() { Content = "New File", InputGestureText = "⌘N", Style = new Style { Padding = new Thickness(6, 8) } };
        MenuItem checkable = new() { Content = "Auto Save", IsCheckable = true, IsChecked = true, Style = new Style { Padding = new Thickness(6, 8) } };
        plain.Children[0].Style = new Style { MinWidth = 24, Margin = new Thickness(0, 8, 0, 0) };
        checkable.Children[0].Style = new Style { MinWidth = 24, Margin = new Thickness(0, 8, 0, 0) };
        menu.Children.Add(plain);
        menu.Children.Add(checkable);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(menu, new Size(220, 300));
        LayoutBox row = layout.Children[0];
        LayoutBox checkedRow = layout.Children[1];
        LayoutBox icon = row.Children[0];
        LayoutBox label = row.Children[1];
        LayoutBox shortcut = row.Children[2];

        Assert.Equal(label.BorderBox.Y, shortcut.BorderBox.Y, "The shortcut must share the label's top edge, without a vertical margin.");
        Assert.Equal(label.BorderBox.X, checkedRow.Children[1].BorderBox.X, "Labels must align across checked and unchecked items.");
        Assert.True(label.BorderBox.X >= icon.BorderBox.Right + 8, "The icon gutter must include horizontal spacing.");
        Assert.True(shortcut.BorderBox.X >= label.BorderBox.Right + 16, "The shortcut must have horizontal spacing after the label.");
        Assert.Equal(row.BorderBox.Height, checkedRow.BorderBox.Height, "Shortcuts must not inflate the menu row height.");
        Assert.True(row.BorderBox.Height < 36, "A single-line menu item must remain compact.");
    }

    public static void VerifyPartUpdates()
    {
        using MenuItem item = new() { Content = "Auto Save", IsCheckable = true };
        item.Items.Add(new MenuItem { Content = "Nested" });
        Assert.True(item.HasItems);
        Assert.Null(item.Parent);
        UIElement label = item.Children[1];
        UIElement icon = item.Children[0];
        View iconHost = (View)icon;
        UIElement checkmark = iconHost.Children[0];
        UIElement shortcut = item.Children[2];

        item.IsChecked = true;
        Assert.True(checkmark.IsVisible);
        item.IsChecked = false;
        Assert.False(checkmark.IsVisible);
        item.IsCheckable = false;
        Assert.True(icon.IsVisible, "The empty icon gutter must remain reserved.");
        item.InputGestureText = "⌘S";
        Assert.True(shortcut.IsVisible);
        item.InputGestureText = null;
        Assert.False(shortcut.IsVisible);
        Assert.Same(label, item.Children[1]);
        Assert.True(icon.ComputedStyle.Width.IsAuto, "The icon host must size to content rather than enforce a fixed width.");
        Assert.Equal(new Thickness(0, 8, 0, 0), icon.ComputedStyle.Margin);
        Assert.Equal(new Thickness(0, 0, 0, 16), shortcut.ComputedStyle.Margin);

        using View customIcon = new() { Width = 40, Height = 20 };
        item.Icon = customIcon;
        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(item, new Size(220, 100));
        Assert.Equal(40.0, layout.Children[0].BorderBox.Width, "The host must accommodate a wider custom icon.");
        item.IsCheckable = true;
        item.IsChecked = true;
        Assert.True(icon.ComputedStyle.Width.IsAuto, "Check state updates must not overwrite icon sizing.");
    }
}
