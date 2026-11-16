using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

internal static class SelectTests
{
    public static void VerifyValueShowsPlaceholderAndSelectedItem()
    {
        using Select select = new() { Placeholder = "Choose" };
        select.Items.Add("Apple");
        select.Items.Add("Pear");
        Assert.Equal("Choose", GetValueText(select));
        Assert.True(select.GetAttribute("data-placeholder") is not null);

        select.SelectedIndex = 1;
        Assert.Equal("Pear", GetValueText(select));
        Assert.True(select.GetAttribute("data-placeholder") is null);

        select.SelectedItem = null;
        Assert.Equal("Choose", GetValueText(select));
    }

    public static void VerifyDropDownOpensAndClosesFromKeyboard()
    {
        using View root = new();
        using Window host = new() { Content = root };
        Select select = new();
        select.Items.Add("Apple");
        root.Children.Add(select);
        int opened = 0;
        int closed = 0;
        select.DropDownOpened += (_, _) => opened++;
        select.DropDownClosed += (_, _) => closed++;

        select.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Enter));
        Assert.True(select.IsDropDownOpen);
        Assert.True(GetPopover(select).IsOpen);
        Assert.Equal(1, opened);
        Assert.True(select.GetAttribute("data-open") is not null);

        select.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Escape));
        Assert.False(select.IsDropDownOpen);
        Assert.False(GetPopover(select).IsOpen);
        Assert.Equal(1, closed);

        select.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown, null, InputModifiers.Alt));
        Assert.True(select.IsDropDownOpen);
        GetPopover(select).IsOpen = false;
        Assert.False(select.IsDropDownOpen);
    }

    public static void VerifyArrowKeysSelectWhileClosed()
    {
        using Select select = new();
        select.Items.Add("Apple");
        select.Items.Add("Pear");
        select.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.Equal(0, select.SelectedIndex);
        select.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.ArrowDown));
        Assert.Equal(1, select.SelectedIndex);
        Assert.False(select.IsDropDownOpen);
    }

    public static void VerifyRequiredValidityAndReset()
    {
        using Select select = new() { Required = true };
        select.Items.Add("Apple");
        select.CaptureInitialValue();
        Assert.False(select.CheckValidity());
        Assert.True(select.Validity.ValueMissing);

        select.SelectedIndex = 0;
        Assert.True(select.CheckValidity());

        select.Reset();
        Assert.Equal(-1, select.SelectedIndex);
    }

    private static string GetValueText(Select select)
    {
        StackPanel trigger = (StackPanel)select.Children[0];
        View value = (View)trigger.Children[0];
        return ((Text)value.Children[0]).Value;
    }

    private static Popover GetPopover(Select select)
    {
        return (Popover)select.Children[1];
    }
}
