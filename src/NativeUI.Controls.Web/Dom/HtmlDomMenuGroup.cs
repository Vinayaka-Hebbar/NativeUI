using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a menu group with its ARIA role and optional header label.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomMenuGroup : HtmlDomControl
{
    private readonly MenuGroup _group;

    internal HtmlDomMenuGroup(MenuGroup group, JSObject? element, string tagName)
        : base(group, element, tagName)
    {
        _group = group;
    }

    /// <inheritdoc />
    protected override string Role => "group";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateHeader();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, MenuGroup.HeaderProperty))
        {
            UpdateHeader();
        }
    }

    private void UpdateHeader()
    {
        if (_group.HeaderPart is View header && _group.Header is not null)
        {
            if (string.IsNullOrEmpty(header.Id))
            {
                header.Id = "nui-menu-group-header-" + NodeId.ToString(CultureInfo.InvariantCulture);
            }

            SetAttribute("aria-labelledby", header.Id);
        }
        else
        {
            SetAttribute("aria-labelledby", null);
        }
    }
}
