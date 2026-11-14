using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a <see cref="Portal"/> while it is open, and removes it from the DOM while it is closed. A portal is appended to
/// its parent's element and shown as a manual HTML popover in the browser's top layer, so selectors through its ancestors
/// match it, as they do on desktop. A <see cref="Dialog"/> is mounted at the end of the <c>native-ui</c> element as a
/// native <c>&lt;dialog&gt;</c> element, so the browser provides the top layer, focus containment, the inert page behind
/// a modal dialog, and closing on Escape. The position is calculated when the portal opens or its placement changes. It
/// does not follow a later scroll or resize.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomPortal : HtmlDomView, IPortalPeer
{
    private static int s_lastGeneratedId;

    private readonly Portal _portal;
    private Action? _startingStyleCompleted;
    private readonly string _generatedId = "nui-portal-" + Interlocked.Increment(ref s_lastGeneratedId).ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Gets the id of the element: <see cref="UIElement.Id"/>, or a generated id when it is <see langword="null"/>, so
    /// <c>aria-controls</c> on the anchor can refer to the element.
    /// </summary>
    internal string ElementId => Source.Id ?? _generatedId;

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("id", ElementId);
        SetAttribute(StyleSheetCss.PortalAttribute, string.Empty);
        UpdateMotionAttributes();
        UpdateMounted();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, UIElement.IdProperty))
        {
            SetAttribute("id", ElementId);
        }
        else if (ReferenceEquals(args.Property, Portal.IsOpenProperty))
        {
            UpdateMotionAttributes();
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

        if (!_portal.IsVisible)
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

        // The parent element is not in the document until the root finishes mounting.
        bool isShown = ParentElement is JSObject parent && (_portal is Dialog
            ? DomInterop.MountPortal(parent, element)
            : DomInterop.ShowPortal(parent, element));
        if (!isShown)
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
        if (_startingStyleCompleted is not null)
        {
            EndStartingStyleAfterFrames();
        }
    }

    /// <inheritdoc />
    void IPortalPeer.WaitForStartingStyle(Action completed)
    {
        _startingStyleCompleted = completed;
        if (_isMounted)
        {
            EndStartingStyleAfterFrames();
        }
    }

    /// <inheritdoc />
    void IPortalPeer.WaitForEndingMotion(Action completed)
    {
        UpdateMotionAttributes();
        if (!_isMounted || Element is not JSObject element)
        {
            completed();
            UpdateMounted();
            return;
        }

        DomInterop.WaitForMotion(element, () =>
        {
            if (IsDisposed)
            {
                return;
            }

            completed();
            UpdateMotionAttributes();
            UpdateVisibility();
            UpdateMounted();
        });
    }

    /// <summary>
    /// Removes <c>data-starting-style</c> in the second animation frame after mounting. The browser has then computed
    /// the starting style, so removing the attribute starts the transitions.
    /// </summary>
    private void EndStartingStyleAfterFrames()
    {
        DomInterop.RequestAnimationFrame(() => DomInterop.RequestAnimationFrame(() =>
        {
            Action? completed = _startingStyleCompleted;
            _startingStyleCompleted = null;
            if (IsDisposed || completed is null)
            {
                return;
            }

            completed();
            UpdateMotionAttributes();
        }));
    }

    /// <summary>
    /// Writes the open state and the motion attributes of the portal. A portal in its exit is inert, which keeps it from
    /// receiving input and focus.
    /// </summary>
    private void UpdateMotionAttributes()
    {
        SetAttribute("data-open", _portal.GetAttribute("data-open"));
        SetAttribute("data-closed", _portal.GetAttribute("data-closed"));
        SetAttribute("data-starting-style", _portal.GetAttribute("data-starting-style"));
        SetAttribute("data-ending-style", _portal.GetAttribute("data-ending-style"));
        SetAttribute("inert", _portal.IsEnding ? string.Empty : null);
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
        SetAttribute("data-side", _portal.GetAttribute("data-side"));
        SetAttribute("data-align", _portal.GetAttribute("data-align"));
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
