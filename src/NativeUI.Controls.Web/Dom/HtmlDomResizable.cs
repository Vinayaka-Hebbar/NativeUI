using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Resizable"/> container with its custom element state attributes.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomResizable : HtmlDomView
{
    private readonly Resizable _resizable;

    internal HtmlDomResizable(Resizable resizable, JSObject? element, string tagName)
        : base(resizable, element, tagName)
    {
        _resizable = resizable;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Resizable.OrientationProperty))
        {
            UpdateAttributes();
        }
    }

    private void UpdateAttributes()
    {
        SetAttribute("data-orientation", _resizable.GetAttribute("data-orientation"));
    }
}
