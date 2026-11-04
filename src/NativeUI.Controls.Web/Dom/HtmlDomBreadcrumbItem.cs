using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="BreadcrumbItem"/> with a custom element. The current page has <c>data-current</c> for
/// stylesheets and <c>aria-current="page"</c> for assistive technologies.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomBreadcrumbItem : HtmlDomView
{
    private readonly BreadcrumbItem _item;

    internal HtmlDomBreadcrumbItem(BreadcrumbItem item, JSObject? element, string tagName)
        : base(item, element, tagName)
    {
        _item = item;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateCurrent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, BreadcrumbItem.IsCurrentProperty))
        {
            UpdateCurrent();
        }
    }

    private void UpdateCurrent()
    {
        SetAttribute("data-current", _item.GetAttribute("data-current"));
        SetAttribute("aria-current", _item.IsCurrent ? "page" : null);
    }
}
