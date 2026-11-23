using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
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

    /// <summary>Verifies that the compiled sample vertically centers the value and chevron inside the trigger.</summary>
    public static void VerifyTriggerAlignment()
    {
        using SelectSampleView page = new();
        Select select = page.Children.OfType<View>().SelectMany(static view => view.Children).OfType<Select>().First();
        StackPanel trigger = (StackPanel)select.Children[0];
        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(page, new Size(700, 1200));
        LayoutBox box = FindBox(layout, trigger) ?? throw new InvalidOperationException("Select trigger was not laid out.");
        double center = box.ContentBox.Y + box.ContentBox.Height / 2;
        Assert.Equal(Alignment.Center, trigger.ComputedStyle.AlignItems);
        foreach (LayoutBox child in box.Children)
        {
            Assert.Equal(center, child.BorderBox.Y + child.BorderBox.Height / 2,
                $"Select part {((UIElement)child.Element).PartName} is not centered: trigger {box.BorderBox}, child {child.BorderBox}.");
        }
    }

    private static LayoutBox? FindBox(LayoutBox box, UIElement element)
    {
        if (ReferenceEquals(box.Element, element))
        {
            return box;
        }

        foreach (LayoutBox child in box.Children)
        {
            if (FindBox(child, element) is LayoutBox found)
            {
                return found;
            }
        }

        return null;
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
