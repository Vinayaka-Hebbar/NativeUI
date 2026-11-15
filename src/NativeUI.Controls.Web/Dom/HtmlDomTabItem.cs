using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a tab header's selection, roving tab stop, and associated panel.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomTabItem : HtmlDomControl
{
    private readonly TabItem _tab;

    internal HtmlDomTabItem(TabItem tab, JSObject? element, string tagName)
        : base(tab, element, tagName)
    {
        _tab = tab;
    }

    /// <inheritdoc />
    protected override string Role => "tab";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateRelationships();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, TabItem.IsSelectedProperty) ||
            ReferenceEquals(args.Property, TabItem.IsTabStopProperty) ||
            ReferenceEquals(args.Property, UIElement.IdProperty))
        {
            UpdateRelationships();
        }
    }

    internal void UpdateRelationships()
    {
        string id = _tab.Id ?? $"nui-tab-{NodeId}";
        SetAttribute("id", id);
        SetAttribute("aria-selected", _tab.IsSelected ? "true" : "false");
        UpdateTabIndex();
        if (_tab.Page.NativeHandle is HtmlDomElement panel)
        {
            string panelId = _tab.Page.Id ?? $"nui-tabpanel-{panel.NodeId}";
            SetAttribute("aria-controls", panelId);
            panel.SetAttribute("id", panelId);
            panel.SetAttribute("role", "tabpanel");
            panel.SetAttribute("aria-labelledby", id);
            panel.SetAttribute("aria-hidden", _tab.IsSelected ? null : "true");
        }
    }
}
