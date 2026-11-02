using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a <see cref="Portal"/> at the end of the <c>native-ui</c> element while it is open, and removes it from the DOM
/// while it is closed. A <see cref="Dialog"/> uses a native <c>&lt;dialog&gt;</c> element, so the browser provides the top
/// layer, focus containment, the inert page behind a modal dialog, and closing on Escape. The position is calculated when the
/// portal opens or its placement changes; it does not follow a later scroll or resize.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomPortal : HtmlDomView
{
    private readonly Portal _portal;
    private bool _isMounted;

    internal HtmlDomPortal(Portal portal, JSObject? element, string tagName)
        : base(portal, element, tagName)
    {
        _portal = portal;
    }

    /// <summary>
    /// Gets the underlying <see cref="Portal"/> element.
    /// </summary>
    internal Portal Portal => _portal;

    /// <summary>
    /// Applies a close that the browser performed, such as on Escape.
    /// </summary>
    internal void OnBrowserClosed()
    {
        _portal.Close();
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute(StyleSheetCss.PortalAttribute, string.Empty);
        UpdateMounted();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Portal.IsOpenProperty))
        {
            UpdateVisibility();
            UpdateMounted();
        }
        else if (ReferenceEquals(args.Property, Portal.SideProperty) ||
            ReferenceEquals(args.Property, Portal.AlignProperty) ||
            ReferenceEquals(args.Property, Portal.SideOffsetProperty) ||
            ReferenceEquals(args.Property, Portal.AlignOffsetProperty))
        {
            UpdatePosition();
        }
    }

    private void UpdateMounted()
    {
        if (IsDisposed || Element is not JSObject element)
        {
            return;
        }

        if (!_portal.IsOpen)
        {
            if (_isMounted)
            {
                _isMounted = false;
                DomInterop.RemoveElement(element);
            }

            return;
        }

        if (_isMounted)
        {
            return;
        }

        // The native-ui element does not contain the tree until the root finishes mounting.
        if (ParentElement is not JSObject parent || !DomInterop.MountPortal(parent, element))
        {
            DomInterop.RequestAnimationFrame(UpdateMounted);
            return;
        }

        _isMounted = true;
        if (_portal is Dialog dialog)
        {
            DomInterop.ShowDialog(element, dialog.IsModal);
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (!_isMounted || Element is not JSObject element)
        {
            return;
        }

        Rect bounds = ReadBounds(element);
        Rect? anchor = (_portal.Anchor?.NativeHandle as HtmlDomElement)?.Element is JSObject anchorElement
            ? ReadBounds(anchorElement)
            : null;

        using JSObject viewportSize = DomInterop.GetViewportSize();
        Rect viewport = new(0, 0, viewportSize.GetPropertyAsDouble("width"), viewportSize.GetPropertyAsDouble("height"));

        Point position = PortalPlacement.Compute(_portal, anchor, bounds.Size, viewport, viewport);
        SetStyleProperty("left", position.X.ToString("R", CultureInfo.InvariantCulture) + "px");
        SetStyleProperty("top", position.Y.ToString("R", CultureInfo.InvariantCulture) + "px");
    }

    private static Rect ReadBounds(JSObject element)
    {
        using JSObject bounds = DomInterop.GetBounds(element);
        return new Rect(
            bounds.GetPropertyAsDouble("x"),
            bounds.GetPropertyAsDouble("y"),
            bounds.GetPropertyAsDouble("width"),
            bounds.GetPropertyAsDouble("height"));
    }
}
