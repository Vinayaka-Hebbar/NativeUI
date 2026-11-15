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
        DefinePart(MenuPartName, _menuView);
        KeyboardNavigation.SetTabNavigation(_menuView, KeyboardNavigationMode.None);
        Content = _menuView;
    }

    /// <inheritdoc />
    public override string TagName => "dropdownmenu";

    /// <summary>
    /// Gets the collection of elements displayed inside the menu, such as <see cref="MenuItem"/> and <see cref="Separator"/>.
    /// </summary>
    public IList<UIElement> Items => _menuView.Children;

    /// <summary>
    /// Gets or sets the item that owns this submenu.
    /// </summary>
    internal MenuItem? OwnerItem { get; init; }

    /// <summary>
    /// Closes this menu and every ancestor menu.
    /// </summary>
    internal void CloseMenu()
    {
        DropdownMenu root = this;
        while (root.OwnerItem?.ParentMenu is DropdownMenu parent)
        {
            root = parent;
        }

        root.IsOpen = false;
    }

    /// <summary>
    /// Closes sibling submenus before another item opens its submenu.
    /// </summary>
    internal void CloseOtherSubmenus(MenuItem item)
    {
        foreach (MenuItem sibling in GetMenuItems())
        {
            if (!ReferenceEquals(sibling, item))
            {
                sibling.CloseSubmenu();
            }
        }
    }

    /// <inheritdoc />
    protected override void AttachTargetInput(UIElement target)
    {
        if (OwnerItem is null)
        {
            base.AttachTargetInput(target);
            target.KeyDown += OnTargetNavigationKeyDown;
        }
    }

    /// <inheritdoc />
    protected override void DetachTargetInput(UIElement target)
    {
        if (OwnerItem is null)
        {
            target.KeyDown -= OnTargetNavigationKeyDown;
            base.DetachTargetInput(target);
        }
    }

    /// <inheritdoc />
    protected override void OnPortalClosing(object? sender, EventArgs args)
    {
        foreach (MenuItem item in GetMenuItems())
        {
            item.CloseSubmenu();
        }

        base.OnPortalClosing(sender, args);
    }

    /// <inheritdoc />
    private protected override void FocusContent()
    {
        if (InputManager.Current.PrimaryKeyboardDevice.MostRecentInput == FocusState.Keyboard)
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
            CloseMenu();
            return;
        }

        if (args.Key == Key.ArrowLeft && OwnerItem is MenuItem owner)
        {
            args.Handled = true;
            IsOpen = false;
            owner.Focus(FocusState.Keyboard);
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

    /// <summary>
    /// Moves keyboard focus to the first available item or the empty menu.
    /// </summary>
    internal void FocusFirstItem()
    {
        foreach (MenuItem item in GetEnabledItems())
        {
            CloseOtherSubmenus(item);
            item.Focus(FocusState.Keyboard);
            return;
        }

        _menuView.Focus(FocusState.Keyboard);
    }

    /// <summary>
    /// Enumerates items through groups without descending into child menus.
    /// </summary>
    internal IEnumerable<MenuItem> GetMenuItems()
    {
        return EnumerateItems(Items);
    }

    private static IEnumerable<MenuItem> EnumerateItems(IEnumerable<UIElement> items)
    {
        foreach (UIElement child in items)
        {
            if (child is MenuItem item)
            {
                yield return item;
            }
            else if (child is MenuGroup group)
            {
                foreach (MenuItem nested in EnumerateItems(group.Items))
                {
                    yield return nested;
                }
            }
        }
    }

    private List<MenuItem> GetEnabledItems()
    {
        List<MenuItem> items = [];
        foreach (MenuItem item in GetMenuItems())
        {
            if (item.IsEffectivelyEnabled && item.IsVisibleInTree)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private void OnTargetNavigationKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || !ReferenceEquals(args.Source, Target) || Target is not { IsEffectivelyEnabled: true } ||
            args.Key is not (Key.ArrowDown or Key.ArrowUp))
        {
            return;
        }

        args.Handled = true;
        IsOpen = true;
        FocusBoundaryItem(args.Key == Key.ArrowDown);
    }

    private void FocusBoundaryItem(bool first)
    {
        List<MenuItem> items = GetEnabledItems();
        if (items.Count == 0)
        {
            _menuView.Focus(FocusState.Keyboard);
            return;
        }

        MenuItem item = items[first ? 0 : items.Count - 1];
        CloseOtherSubmenus(item);
        item.Focus(FocusState.Keyboard);
    }

    private void MoveSelection(bool forward)
    {
        List<MenuItem> enabledItems = GetEnabledItems();

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

        CloseOtherSubmenus(enabledItems[nextIndex]);
        enabledItems[nextIndex].Focus(FocusState.Keyboard);
    }

    private bool TrySearchItem(string query)
    {
        List<MenuItem> enabledItems = GetEnabledItems();

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
                CloseOtherSubmenus(item);
                item.Focus(FocusState.Keyboard);
                return true;
            }
        }

        return false;
    }
}
