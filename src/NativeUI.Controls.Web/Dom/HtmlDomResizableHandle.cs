using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ResizableHandle"/> with its custom element state attributes and accessibility roles.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomResizableHandle : HtmlDomElement
{
    private readonly ResizableHandle _handle;

    internal HtmlDomResizableHandle(ResizableHandle handle, JSObject? element, string tagName)
        : base(handle, element, tagName)
    {
        _handle = handle;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (Element is not null)
        {
            DomInterop.SetAttribute(Element, "role", "separator");
        }

        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, ResizableHandle.DisabledProperty) ||
            ReferenceEquals(args.Property, ResizableHandle.OrientationProperty) ||
            ReferenceEquals(args.Property, ResizableHandle.IsDraggingProperty) ||
            ReferenceEquals(args.Property, UIElement.CursorProperty))
        {
            UpdateAttributes();
        }
    }

    private void UpdateAttributes()
    {
        if (Element is null)
        {
            return;
        }

        string orientation = _handle.EffectiveOrientation == Orientation.Vertical ? "vertical" : "horizontal";
        DomInterop.SetAttribute(Element, "data-orientation", orientation);
        DomInterop.SetAttribute(Element, "aria-orientation", orientation);
        DomInterop.SetAttribute(Element, "data-dragging", _handle.IsDragging ? "true" : "false");
        DomInterop.SetAttribute(Element, "data-disabled", _handle.Disabled ? "true" : "false");
        DomInterop.SetAttribute(Element, "tabindex", _handle.Disabled ? "-1" : "0");
    }
}
