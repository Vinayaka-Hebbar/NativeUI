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
        else if (ReferenceEquals(args.Property, MenuItem.IsCheckedProperty))
        {
            UpdateCheckedState();
        }
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
