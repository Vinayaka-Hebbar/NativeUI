using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Displays a menu of actionable items in a popup anchored to a target element.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DropdownMenu"/> extends <see cref="Popover"/> by hosting a collection of <see cref="Items"/> in a vertical
/// stack (<c>menu</c> part). It sets <see cref="KeyboardNavigation.TabNavigationProperty"/> to <see cref="KeyboardNavigationMode.None"/>
/// so Tab closes the menu and returns focus to the target instead of tabbing away into other controls.
/// </para>
/// <para>
/// Keyboard interactions within an open menu:
/// <list type="bullet">
/// <item><description>Keyboard opening focuses the first enabled item; pointer opening focuses the menu.</description></item>
/// <item><description>Up and Down arrow keys move focus between enabled items, wrapping at either end.</description></item>
/// <item><description>Home and End keys move focus to the first or last enabled item.</description></item>
/// <item><description>Type-ahead typing cycles through items starting with that character.</description></item>
/// <item><description>Escape and Tab close the menu.</description></item>
/// </list>
/// </para>
/// </remarks>
[ContentProperty(nameof(Items))]
public class DropdownMenu : Popover
{
    private const string MenuPartName = "menu";

    private readonly StackPanel _menuView;

    /// <summary>
    /// Initializes a new instance of the <see cref="DropdownMenu"/> class.
    /// </summary>
    public DropdownMenu()
    {
        _menuView = new StackPanel { Orientation = Orientation.Vertical, Focusable = true, TabIndex = -1 };
        _menuView.ClearTemplatePart();
        DefinePart(_menuView, MenuPartName);
        KeyboardNavigation.SetTabNavigation(_menuView, KeyboardNavigationMode.None);
        Content = _menuView;
    }

    /// <inheritdoc />
    public override string TagName => "dropdownmenu";

    /// <summary>
    /// Gets the collection of elements displayed inside the menu, such as <see cref="MenuItem"/> and <see cref="Separator"/>.
    /// </summary>
    public IList<UIElement> Items => _menuView.Children;

    /// <inheritdoc />
    private protected override void FocusContent()
    {
        if (FindFocusManager()?.MostRecentInput == FocusState.Keyboard)
        {
            FocusFirstItem();
        }
        else
        {
            _menuView.Focus(FocusState.Pointer);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (!IsOpen || args.Handled)
        {
            base.OnKeyDown(sender, args);
            return;
        }

        if (args.Key == Key.Tab)
        {
            args.Handled = true;
            IsOpen = false;
            return;
        }

        if (args.Key == Key.ArrowDown)
        {
            args.Handled = true;
            MoveSelection(forward: true);
            return;
        }

        if (args.Key == Key.ArrowUp)
        {
            args.Handled = true;
            MoveSelection(forward: false);
            return;
        }

        if (args.Key == Key.Home)
        {
            args.Handled = true;
            FocusBoundaryItem(first: true);
            return;
        }

        if (args.Key == Key.End)
        {
            args.Handled = true;
            FocusBoundaryItem(first: false);
            return;
        }

        if (args.Key == Key.Character && !string.IsNullOrEmpty(args.Text))
        {
            if (TrySearchItem(args.Text))
            {
                args.Handled = true;
                return;
            }
        }

        base.OnKeyDown(sender, args);
    }

    private void FocusFirstItem()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] is MenuItem { IsEnabled: true } item)
            {
                item.Focus(FocusState.Keyboard);
                return;
            }
        }
    }

    private void FocusBoundaryItem(bool first)
    {
        int start = first ? 0 : Items.Count - 1;
        int step = first ? 1 : -1;
        for (int i = start; i >= 0 && i < Items.Count; i += step)
        {
            if (Items[i] is MenuItem { IsEnabled: true } item)
            {
                item.Focus(FocusState.Keyboard);
                return;
            }
        }
    }

    private void MoveSelection(bool forward)
    {
        List<MenuItem> enabledItems = [];
        foreach (UIElement child in Items)
        {
            if (child is MenuItem { IsEnabled: true } item)
            {
                enabledItems.Add(item);
            }
        }

        if (enabledItems.Count == 0)
        {
            return;
        }

        int currentIndex = -1;
        for (int i = 0; i < enabledItems.Count; i++)
        {
            if (enabledItems[i].IsFocused)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = forward ? 0 : enabledItems.Count - 1;
        }
        else if (forward)
        {
            nextIndex = (currentIndex + 1) % enabledItems.Count;
        }
        else
        {
            nextIndex = (currentIndex - 1 + enabledItems.Count) % enabledItems.Count;
        }

        enabledItems[nextIndex].Focus(FocusState.Keyboard);
    }

    private bool TrySearchItem(string query)
    {
        List<MenuItem> enabledItems = [];
        foreach (UIElement child in Items)
        {
            if (child is MenuItem { IsEnabled: true } item)
            {
                enabledItems.Add(item);
            }
        }

        if (enabledItems.Count == 0)
        {
            return false;
        }

        int currentIndex = -1;
        for (int i = 0; i < enabledItems.Count; i++)
        {
            if (enabledItems[i].IsFocused)
            {
                currentIndex = i;
                break;
            }
        }

        for (int offset = 1; offset <= enabledItems.Count; offset++)
        {
            int index = (currentIndex + offset) % enabledItems.Count;
            MenuItem item = enabledItems[index];
            string? text = item.GetSearchText();
            if (!string.IsNullOrEmpty(text) && text.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            {
                item.Focus(FocusState.Keyboard);
                return true;
            }
        }

        return false;
    }
}
