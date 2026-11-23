using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a <see cref="Portal"/> while it is open, and removes it from the DOM while it is closed. A portal is appended to
/// its parent's element and shown as a manual HTML popover in the browser's top layer, so selectors through its ancestors
/// match it, as they do on desktop. A <see cref="Dialog"/> keeps its DOM ancestry and is shown as a
/// native <c>&lt;dialog&gt;</c> element, so the browser provides the top layer, focus containment, the inert page behind
/// a modal dialog, and closing on Escape. Element-anchored popovers use native CSS anchor positioning, which follows
/// scrolling, resizing, and subsequent browser layout.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomPortal : HtmlDomView, IPortalPeer
{
    private static int s_lastGeneratedId;

    private readonly Portal _portal;
    private Action? _pendingAnimationFrame;
    private readonly string _generatedId = "nui-portal-" + Interlocked.Increment(ref s_lastGeneratedId).ToString(CultureInfo.InvariantCulture);
    private bool _isMounted;
    private JSObject? _placementObserver;
    private UIElement? _positionedAnchor;

    internal HtmlDomPortal(Portal portal, JSObject? element, string tagName)
        : base(portal, element, tagName)
    {
        _portal = portal;
        _portal.TransitionStatusChanged += OnTransitionStatusChanged;
        _portal.PlacementChanged += OnPlacementChanged;
    }

    /// <summary>
    /// Gets the underlying <see cref="Portal"/> element.
    /// </summary>
    internal Portal Portal => _portal;

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
        if (_portal.TemplatedParent is DropdownMenu)
        {
            SetAttribute("role", "menu");
            SetAttribute("aria-orientation", "vertical");
        }

        SetAttribute(StyleSheetCss.PortalAttribute, string.Empty);
        UpdateDataAttributes();
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
            UpdateDataAttributes();
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
                DisconnectPlacementObserver();
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
            : DomInterop.ShowPortal(parent, element, GetAnchorElement()));
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
        if (_pendingAnimationFrame is Action pending)
        {
            _pendingAnimationFrame = null;
            ((IPortalPeer)this).RequestAnimationFrame(pending);
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        DisconnectPlacementObserver();
        _portal.TransitionStatusChanged -= OnTransitionStatusChanged;
        _portal.PlacementChanged -= OnPlacementChanged;
        if (_isMounted && Element is JSObject element)
        {
            _isMounted = false;
            DomInterop.RemoveElement(element);
        }

        base.OnDisposing();
    }

    /// <summary>
    /// Invokes <paramref name="callback"/> in the second animation frame after the element is mounted. The browser has
    /// then drawn the element with its current attributes, and a style change after that starts transitions.
    /// </summary>
    void IPortalPeer.RequestAnimationFrame(Action callback)
    {
        if (!_isMounted)
        {
            _pendingAnimationFrame = callback;
            return;
        }

        DomInterop.RequestAnimationFrame(() => DomInterop.RequestAnimationFrame(() =>
        {
            if (!IsDisposed)
            {
                callback();
            }
        }));
    }

    /// <inheritdoc />
    void IPortalPeer.WaitForAnimations(Action callback)
    {
        if (!_isMounted || Element is not JSObject element)
        {
            callback();
            return;
        }

        DomInterop.WaitForAnimations(element, () =>
        {
            if (!IsDisposed)
            {
                callback();
            }
        });
    }

    private void OnTransitionStatusChanged(object? sender, EventArgs args)
    {
        UpdateDataAttributes();
        UpdateVisibility();
        UpdateMounted();
    }

    /// <summary>
    /// Writes the open state and the transition attributes of the portal. A portal during its exit is inert, which keeps
    /// it from receiving input and focus.
    /// </summary>
    private void UpdateDataAttributes()
    {
        SetAttribute("data-open", _portal.GetAttribute("data-open"));
        SetAttribute("data-closed", _portal.GetAttribute("data-closed"));
        SetAttribute("data-starting-style", _portal.GetAttribute("data-starting-style"));
        SetAttribute("data-ending-style", _portal.GetAttribute("data-ending-style"));
        SetAttribute("inert", _portal.TransitionStatus == TransitionStatus.Ending ? string.Empty : null);
    }

    private void OnPlacementChanged(object? sender, EventArgs args)
    {
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

        bool isElementAnchored = _portal is not Dialog && _portal.AnchorPoint is null && GetAnchorElement() is not null;
        PortalSide side = _portal.Side;
        Point position;
        if (isElementAnchored)
        {
            ApplyAnchorPosition();
            bounds = ReadBounds(element);
            position = new Point(bounds.X, bounds.Y);
            if (anchor is Rect actualAnchor)
            {
                side = GetPlacedSide(bounds, actualAnchor);
            }
        }
        else
        {
            DisconnectPlacementObserver();
            SetStyleProperty("position-area", null);
            SetStyleProperty("position-anchor", null);
            SetStyleProperty("position-try-fallbacks", null);
            SetStyleProperty("margin", "0px");
            position = PortalPlacement.Compute(_portal, anchor, bounds.Size, viewport, viewport, out side);
        }
        if (_portal.AnchorPoint is Point point && RootElement is JSObject root)
        {
            Rect rootBounds = ReadBounds(root);
            Rect pointerAnchor = new(rootBounds.X + point.X, rootBounds.Y + point.Y, 0, 0);
            position = PortalPlacement.ComputeAtPoint(_portal, new Point(pointerAnchor.X, pointerAnchor.Y), bounds.Size, viewport);
        }
        if (!isElementAnchored)
        {
            SetStyleProperty("left", position.X.ToString("R", CultureInfo.InvariantCulture) + "px");
            SetStyleProperty("top", position.Y.ToString("R", CultureInfo.InvariantCulture) + "px");
        }
        string? dataSide = _portal.GetAttribute("data-side");
        string? dataAlign = _portal.GetAttribute("data-align");
        SetAttribute("data-side", dataSide);
        SetAttribute("data-align", dataAlign);
        if (_portal.TemplatedParent?.NativeHandle is HtmlDomElement ownerDom)
        {
            ownerDom.SetAttribute("data-side", dataSide);
            ownerDom.SetAttribute("data-align", dataAlign);
        }

        (UIElement? arrow, double arrowPadding) = _portal.TemplatedParent switch
        {
            Popover popover => (popover.ArrowPart, popover.ArrowPadding),
            Tooltip tooltip => (tooltip.ArrowPart, tooltip.ArrowPadding),
            _ => (null, 0.0)
        };

        if (arrow?.NativeHandle is HtmlDomElement arrowDom && arrowDom.Element is JSObject arrowElement)
        {
            arrowDom.SetAttribute("data-side", dataSide);
            arrowDom.SetAttribute("data-align", dataAlign);
            if (anchor is Rect anchorBounds)
            {
                double arrowWidth = arrowElement.GetPropertyAsDouble("offsetWidth");
                double arrowHeight = arrowElement.GetPropertyAsDouble("offsetHeight");
                Size arrowSize = arrowWidth > 0 && arrowHeight > 0
                    ? new Size(arrowWidth, arrowHeight)
                    : ReadBounds(arrowElement).Size;
                Rect portalBounds = new(position.X, position.Y, bounds.Width, bounds.Height);
                Point arrowOffset = PortalPlacement.ComputeArrowPosition(side, portalBounds, anchorBounds, arrowSize, arrowPadding);
                arrowDom.SetStyleProperty("left", arrowOffset.X.ToString("R", CultureInfo.InvariantCulture) + "px");
                arrowDom.SetStyleProperty("top", arrowOffset.Y.ToString("R", CultureInfo.InvariantCulture) + "px");
                ApplyArrowBorderSides(arrowDom, side);
            }
        }
    }

    private JSObject? GetAnchorElement()
    {
        return _portal.AnchorPoint is null ? (_portal.Anchor?.NativeHandle as HtmlDomElement)?.Element : null;
    }

    private void ApplyAnchorPosition()
    {
        if (Element is not JSObject element || GetAnchorElement() is not JSObject anchor)
        {
            return;
        }

        if (!ReferenceEquals(_positionedAnchor, _portal.Anchor))
        {
            DisconnectPlacementObserver();
            DomInterop.SetPortalSource(element, anchor);
            _positionedAnchor = _portal.Anchor;
            _placementObserver = DomInterop.ObservePortal(element, anchor, OnPlacementObserved);
        }

        SetStyleProperty("inset", "auto");
        SetStyleProperty("position-anchor", _portal.ComputedStyle.PositionAnchor ?? "auto");
        SetStyleProperty("position-area", StyleCssDeclarations.FormatPositionArea(_portal.GetPositionArea()));
        SetStyleProperty("position-try-fallbacks", StyleCssDeclarations.FormatPositionTryFallbacks(_portal.GetPositionTryFallbacks()));
        SetStyleProperty("margin", StyleCssDeclarations.FormatThickness(_portal.GetPositionMargin()));
    }

    private void OnPlacementObserved()
    {
        if (!IsDisposed && _isMounted && _portal.IsVisible)
        {
            UpdatePosition();
        }
    }

    private void DisconnectPlacementObserver()
    {
        if (_placementObserver is JSObject observer)
        {
            _placementObserver = null;
            DomInterop.DisconnectPortalObserver(observer);
            observer.Dispose();
        }

        _positionedAnchor = null;
    }

    private PortalSide GetPlacedSide(Rect portal, Rect anchor)
    {
        return _portal.Side is PortalSide.Top or PortalSide.Bottom
            ? portal.Y + portal.Height / 2 < anchor.Y + anchor.Height / 2 ? PortalSide.Top : PortalSide.Bottom
            : portal.X + portal.Width / 2 < anchor.X + anchor.Width / 2 ? PortalSide.Left : PortalSide.Right;
    }

    private static void ApplyArrowBorderSides(HtmlDomElement arrowDom, PortalSide side)
    {
        arrowDom.SetStyleProperty("border-top-width", side is PortalSide.Top or PortalSide.Right ? "0px" : null);
        arrowDom.SetStyleProperty("border-right-width", side is PortalSide.Bottom or PortalSide.Right ? "0px" : null);
        arrowDom.SetStyleProperty("border-bottom-width", side is PortalSide.Bottom or PortalSide.Left ? "0px" : null);
        arrowDom.SetStyleProperty("border-left-width", side is PortalSide.Top or PortalSide.Left ? "0px" : null);
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
