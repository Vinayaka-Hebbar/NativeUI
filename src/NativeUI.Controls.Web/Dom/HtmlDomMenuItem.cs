using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="MenuItem"/> with a custom element. The element has <c>role="menuitem"</c>
/// (or <c>role="menuitemcheckbox"</c> when checkable, with <c>aria-checked</c>) and reflects disabled state.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomMenuItem : HtmlDomControl
{
    private readonly MenuItem _menuItem;

    internal HtmlDomMenuItem(MenuItem menuItem, JSObject? element, string tagName)
        : base(menuItem, element, tagName)
    {
        _menuItem = menuItem;
    }

    /// <inheritdoc />
    protected override string Role => _menuItem.IsCheckable ? "menuitemcheckbox" : "menuitem";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateCheckedState();
        UpdateSubmenuState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, MenuItem.IsCheckableProperty))
        {
            SetAttribute("role", Role);
            UpdateCheckedState();
        }
        else if (ReferenceEquals(args.Property, MenuItem.HasItemsProperty) ||
            ReferenceEquals(args.Property, MenuItem.IsSubmenuOpenProperty))
        {
            UpdateSubmenuState();
        }
        else if (ReferenceEquals(args.Property, MenuItem.IsCheckedProperty))
        {
            UpdateCheckedState();
        }
    }

    private void UpdateSubmenuState()
    {
        SetAttribute("data-has-items", _menuItem.HasItems ? string.Empty : null);
        SetAttribute("data-open", _menuItem.GetAttribute("data-open"));
        SetAttribute("data-closed", _menuItem.GetAttribute("data-closed"));
        SetAttribute("aria-haspopup", _menuItem.HasItems ? "menu" : null);
        SetAttribute("aria-expanded", _menuItem.HasItems ? (_menuItem.IsSubmenuOpen ? "true" : "false") : null);
        SetAttribute("data-popup-open", _menuItem.IsSubmenuOpen ? string.Empty : null);
    }

    private void UpdateCheckedState()
    {
        if (_menuItem.IsCheckable)
        {
            SetAttribute("aria-checked", _menuItem.IsChecked ? "true" : "false");
        }
        else
        {
            SetAttribute("aria-checked", null);
        }
    }
}
