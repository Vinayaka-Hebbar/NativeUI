using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Exposes tab-list and tab-panel relationships through real DOM elements.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomTabControl : HtmlDomView
{
    private readonly TabControl _tabs;

    internal HtmlDomTabControl(TabControl tabs, JSObject? element, string tagName)
        : base(tabs, element, tagName)
    {
        _tabs = tabs;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateTabs();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, TabControl.TabStripPlacementProperty) ||
            ReferenceEquals(args.Property, TabControl.SelectedContentProperty) ||
            ReferenceEquals(args.Property, Selector.SelectedIndexProperty))
        {
            UpdateTabs();
        }
    }

    private void UpdateTabs()
    {
        if (_tabs.Strip?.NativeHandle is HtmlDomElement strip)
        {
            strip.SetAttribute("role", "tablist");
            strip.SetAttribute("aria-orientation", _tabs.TabStripPlacement is Dock.Left or Dock.Right ? "vertical" : "horizontal");
        }

        for (int i = 0; i < _tabs.Items.Count; i++)
        {
            if (_tabs.GetItemElement(i)?.NativeHandle is HtmlDomTabItem item)
            {
                item.UpdateRelationships();
            }
        }
    }
}
