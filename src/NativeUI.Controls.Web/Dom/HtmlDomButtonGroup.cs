using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ButtonGroup"/> with a flex container that has the ARIA <c>group</c> role. The flex direction
/// follows <see cref="ButtonGroup.Orientation"/>, which <c>data-orientation</c> carries for stylesheets.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomButtonGroup : HtmlDomControl
{
    private readonly ButtonGroup _group;

    internal HtmlDomButtonGroup(ButtonGroup group, JSObject? element, string tagName)
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
        UpdateOrientation();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, ButtonGroup.OrientationProperty))
        {
            UpdateOrientation();
        }
    }

    private void UpdateOrientation()
    {
        bool isHorizontal = _group.Orientation == Orientation.Horizontal;
        SetAttribute("data-orientation", _group.GetAttribute("data-orientation"));
        SetStyleProperty("flex-direction", isHorizontal ? "row" : "column");
        SetStyleProperty("align-items", isHorizontal ? "flex-start" : "stretch");
    }
}
