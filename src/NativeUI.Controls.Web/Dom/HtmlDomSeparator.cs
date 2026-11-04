using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Separator"/> with a custom element that has the ARIA <c>separator</c> role.
/// <c>data-orientation</c> carries <see cref="Separator.Orientation"/> for stylesheets and <c>aria-orientation</c> carries it
/// for assistive technologies.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSeparator : HtmlDomElement
{
    private readonly Separator _separator;

    internal HtmlDomSeparator(Separator separator, JSObject? element, string tagName)
        : base(separator, element, tagName)
    {
        _separator = separator;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("role", "separator");
        UpdateOrientation();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Separator.OrientationProperty))
        {
            UpdateOrientation();
        }
    }

    private void UpdateOrientation()
    {
        string? orientation = _separator.GetAttribute("data-orientation");
        SetAttribute("data-orientation", orientation);
        SetAttribute("aria-orientation", orientation);
    }
}
