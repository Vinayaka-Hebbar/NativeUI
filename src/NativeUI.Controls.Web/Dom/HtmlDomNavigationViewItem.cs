using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="NavigationViewItem"/> with its DOM element, reflecting <c>aria-current="page"</c>,
/// <c>aria-expanded</c>, and navigation state attributes without using <c>aria-selected</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomNavigationViewItem : HtmlDomView
{
    private readonly NavigationViewItem _item;

    internal HtmlDomNavigationViewItem(NavigationViewItem item, JSObject? element, string tagName)
        : base(item, element, tagName)
    {
        _item = item;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateAccessibilityAndAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, NavigationViewItem.IsSelectedProperty) ||
            ReferenceEquals(args.Property, NavigationViewItem.IsExpandedProperty))
        {
            UpdateAccessibilityAndAttributes();
        }
    }

    private void UpdateAccessibilityAndAttributes()
    {
        SetAttribute("aria-current", _item.IsSelected ? "page" : null);

        if (_item.HasItems)
        {
            SetAttribute("aria-expanded", _item.IsExpanded ? "true" : "false");
        }
        else
        {
            SetAttribute("aria-expanded", null);
        }

        SetAttribute("data-selected", _item.GetAttribute("data-selected"));
        SetAttribute("data-expanded", _item.GetAttribute("data-expanded"));
        SetAttribute("data-collapsed", _item.GetAttribute("data-collapsed"));
        SetAttribute("data-has-items", _item.GetAttribute("data-has-items"));

        if (_item.Content is string contentStr)
        {
            SetAttribute("title", contentStr);
        }
    }
}
