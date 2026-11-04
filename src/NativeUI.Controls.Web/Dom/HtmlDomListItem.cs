using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ListItem"/> with a custom element that has the ARIA <c>option</c> role. <c>aria-selected</c>
/// carries <see cref="ListItem.IsSelected"/>, which the browser CSS for <c>:selected</c> selects.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomListItem : HtmlDomView
{
    private readonly ListItem _item;

    internal HtmlDomListItem(ListItem item, JSObject? element, string tagName)
        : base(item, element, tagName)
    {
        _item = item;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("role", "option");
        UpdateSelected();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, ListItem.IsSelectedProperty))
        {
            UpdateSelected();
        }
    }

    private void UpdateSelected()
    {
        SetAttribute("aria-selected", _item.IsSelected ? "true" : "false");
    }
}
