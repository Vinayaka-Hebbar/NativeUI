using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Identifies the side of an anchor element that a <see cref="Portal"/> opens on.
/// </summary>
public enum PortalSide
{
    /// <summary>
    /// Above the anchor.
    /// </summary>
    Top,

    /// <summary>
    /// To the right of the anchor.
    /// </summary>
    Right,

    /// <summary>
    /// Below the anchor.
    /// </summary>
    Bottom,

    /// <summary>
    /// To the left of the anchor.
    /// </summary>
    Left
}

/// <summary>
/// Shows its children above all other content while open, and takes no space in its parent's layout. It stays a logical
/// child of its parent for <see cref="BindableObject.DataContext"/> and for selectors through its ancestors. The desktop
/// renderer draws it after the visual tree. On Web it is shown in the browser's top layer from its parent's element.
/// </summary>
/// <remarks>
/// The portal has <c>data-open</c> while open and <c>data-closed</c> while closed. It has <c>data-starting-style</c> in
/// the first frame after it opens. Its transitions run from the styles that select that attribute. When it closes, it
/// has <c>data-ending-style</c> and stays drawn until its own transitions and its animations with a finite number of
/// iterations finish. It takes no input during that time. The attribute names come from Base UI.
/// </remarks>
public class Portal : View, IPortalLayoutNode, IFrameTarget
{
    private OverlayManager? _overlayManager;

    private Point? _anchorPoint;
    private TransitionStatus _transitionStatus;

    // The transition status of the most recent frame that drew this portal.
    private TransitionStatus? _renderedTransitionStatus;

    static Portal()
    {
        WidthProperty.OverrideMetadata<Portal>(WidthValue.FitContent);
        PositionProperty.OverrideMetadata<Portal>(NativeUI.Layout.Position.Fixed);
    }

    /// <summary>
    /// Identifies the <see cref="IsOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Create<Portal, bool>(
            nameof(IsOpen),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.OnIsVisibleChanged());

    /// <summary>
    /// Identifies the <see cref="Side"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<PortalSide> SideProperty =
        BindableProperty.Create<Portal, PortalSide>(
            nameof(Side),
            defaultValue: PortalSide.Bottom,
            propertyChanged: static (owner, _, _) => owner.OnPlacementChanged());

    /// <summary>
    /// Identifies the <see cref="Align"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Alignment> AlignProperty =
        BindableProperty.Create<Portal, Alignment>(
            nameof(Align),
            defaultValue: Alignment.Start,
            propertyChanged: static (owner, _, _) => owner.OnPlacementChanged());

    /// <summary>
    /// Identifies the <see cref="SideOffset"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SideOffsetProperty =
        BindableProperty.Create<Portal, double>(
            nameof(SideOffset),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="AlignOffset"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AlignOffsetProperty =
        BindableProperty.Create<Portal, double>(
            nameof(AlignOffset),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Occurs after <see cref="Show(UIElement?)"/> opens this portal.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Occurs after this portal closes, whatever closed it.
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Occurs when this portal starts to close, while its content is still visible and keeps focus.
    /// </summary>
    internal event EventHandler? Closing;

    /// <summary>
    /// Occurs when <see cref="TransitionStatus"/> changes.
    /// </summary>
    internal event EventHandler? TransitionStatusChanged;

    /// <summary>
    /// Occurs when an open portal moves to a different element or pointer anchor.
    /// </summary>
    internal event EventHandler? PlacementChanged;

    /// <summary>
    /// Occurs after an open or a close finishes.
    /// </summary>
    internal event EventHandler? OpenChangeCompleted;

    /// <inheritdoc />
    public override string TagName => "portal";

    /// <inheritdoc />
    public override bool IsVisible => base.IsVisible && (IsOpen || _transitionStatus == TransitionStatus.Ending);

    /// <inheritdoc />
    internal override bool IsHitTestVisible => IsOpen && base.IsHitTestVisible;

    /// <summary>
    /// Gets the phase of the open and close transitions.
    /// </summary>
    internal TransitionStatus TransitionStatus => _transitionStatus;

    /// <inheritdoc />
    UIElement IFrameTarget.Target => this;

    /// <summary>
    /// Gets a value indicating whether this portal is open.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        private set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets the element this portal is positioned against while open, or <see langword="null"/> when it is centered.
    /// </summary>
    public UIElement? Anchor { get; private set; }

    /// <summary>
    /// Gets the pointer anchor in root coordinates, or null when anchored to an element.
    /// </summary>
    internal Point? AnchorPoint => _anchorPoint;

    /// <summary>
    /// Gets or sets the side of <see cref="Anchor"/> to open on. Defaults to <see cref="PortalSide.Bottom"/>.
    /// </summary>
    public PortalSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>
    /// Gets or sets how this portal lines up along the chosen side of <see cref="Anchor"/>. Defaults to <see cref="Alignment.Start"/>.
    /// </summary>
    public Alignment Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    /// <summary>
    /// Gets or sets the gap between <see cref="Anchor"/> and this portal in logical pixels.
    /// </summary>
    public double SideOffset
    {
        get => GetValue(SideOffsetProperty);
        set => SetValue(SideOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the shift along the anchor's edge in logical pixels, measured away from the edge that <see cref="Align"/> selects.
    /// With <see cref="Alignment.Center"/>, a positive value moves right or down.
    /// </summary>
    public double AlignOffset
    {
        get => GetValue(AlignOffsetProperty);
        set => SetValue(AlignOffsetProperty, value);
    }

    /// <summary>
    /// Returns <see cref="Side"/> for <c>data-side</c> and <see cref="Align"/> for <c>data-align</c> while this portal is open next
    /// to an anchor, as <c>top</c>, <c>right</c>, <c>bottom</c>, or <c>left</c> and as <c>start</c>, <c>center</c>, or <c>end</c>.
    /// Returns an empty value for <c>data-open</c>, <c>data-closed</c>, <c>data-starting-style</c>, and
    /// <c>data-ending-style</c> while each applies.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        switch (name)
        {
            case "data-open":
                return IsOpen ? string.Empty : null;
            case "data-closed":
                return IsOpen ? null : string.Empty;
            case "data-starting-style":
                return _transitionStatus == TransitionStatus.Starting ? string.Empty : null;
            case "data-ending-style":
                return _transitionStatus == TransitionStatus.Ending ? string.Empty : null;
        }

        if (string.Equals(name, "data-side", StringComparison.Ordinal))
        {
            return Anchor is null && _anchorPoint is null ? null : GetSideName(Side);
        }

        if (string.Equals(name, "data-align", StringComparison.Ordinal))
        {
            return Anchor is null && _anchorPoint is null ? null : GetAlignName(Align);
        }

        return base.GetAttribute(name);
    }

    /// <summary>
    /// Gets a value indicating whether a press outside this portal and its anchor closes it.
    /// </summary>
    internal virtual bool ClosesOnOutsidePress => true;

    /// <summary>
    /// Opens this portal next to <paramref name="anchor"/>, or centered when it is <see langword="null"/>. Calling this
    /// while open moves the portal to the new anchor.
    /// </summary>
    public void Show(UIElement? anchor = null)
    {
        _anchorPoint = null;
        ShowCore(anchor);
    }

    /// <summary>
    /// Opens this portal at a point in root coordinates measured in logical pixels.
    /// </summary>
    /// <param name="position">The pointer position relative to the root element.</param>
    public void Show(Point position)
    {
        _anchorPoint = position;
        ShowCore(null);
    }

    private void ShowCore(UIElement? anchor)
    {
        if (IsOpen)
        {
            Anchor = anchor;
            _overlayManager?.UpdateRelationships();
            PlacementChanged?.Invoke(this, EventArgs.Empty);
            InvalidateStyle();
            InvalidateVisual();
            return;
        }

        OnOpening();
        Anchor = anchor;
        UpdateOverlayManager();
        _overlayManager?.Register(this);

        // A portal that opens again during its exit transitions back from its current style.
        TransitionStatus status = _transitionStatus == TransitionStatus.Ending ? TransitionStatus.Idle : TransitionStatus.Starting;
        _transitionStatus = TransitionStatus.Idle;
        IsOpen = true;
        if (!IsOpen)
        {
            return;
        }

        Opened?.Invoke(this, EventArgs.Empty);
        if (IsOpen)
        {
            SetTransitionStatus(status);
        }
    }

    /// <summary>
    /// Closes this portal. Does nothing when it is already closed.
    /// </summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _overlayManager?.CloseChildren(this);

        Closing?.Invoke(this, EventArgs.Empty);
        if (States.HasFlag(ElementStates.FocusWithin))
        {
            InputManager.Current.PrimaryKeyboardDevice.ClearFocus(this);
        }

        // A drawn portal stays visible during its exit. Its status changes before IsOpen does.
        _transitionStatus = IsVisibleInTree ? TransitionStatus.Ending : TransitionStatus.Idle;
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
        OnClosed();
        SetTransitionStatus(_transitionStatus);
    }

    /// <inheritdoc />
    internal override void MarkRendered()
    {
        base.MarkRendered();
        _renderedTransitionStatus = _transitionStatus;
    }

    /// <inheritdoc />
    void IFrameTarget.Tick(double now)
    {
        if (_renderedTransitionStatus == _transitionStatus)
        {
            if (_transitionStatus == TransitionStatus.Starting)
            {
                SetTransitionStatus(TransitionStatus.Idle);
            }
            else if (_transitionStatus == TransitionStatus.Ending && !HasRunningAnimations)
            {
                SetTransitionStatus(TransitionStatus.Idle);
            }
        }

        if (_transitionStatus == TransitionStatus.Idle)
        {
            AnimationClock.Remove(this);
        }
    }

    /// <summary>
    /// Runs before this portal opens. An exception leaves it closed.
    /// </summary>
    private protected virtual void OnOpening()
    {
    }

    /// <summary>
    /// Runs after this portal closes and the <see cref="Closed"/> handlers have run, before its exit animations finish.
    /// </summary>
    private protected virtual void OnClosed()
    {
    }

    /// <summary>
    /// Runs after an open or a close finishes: after the platform draws the starting style, or after the exit animations
    /// finish and the portal is hidden. <see cref="IsOpen"/> tells which. A portal that opens again during its exit does
    /// not run it for that exit.
    /// </summary>
    private protected virtual void OnOpenChangeComplete()
    {
    }

    /// <summary>
    /// Resolves the overlay manager belonging to this portal's hosted root.
    /// </summary>
    internal void UpdateOverlayManager()
    {
        OverlayManager? manager = OverlayManager.FindFor(this);
        if (ReferenceEquals(_overlayManager, manager))
        {
            return;
        }

        _overlayManager?.Unregister(this);
        _overlayManager = manager;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        UpdateOverlayManager();
        if (IsVisible)
        {
            _overlayManager?.Register(this);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        _overlayManager?.Unregister(this);
        _overlayManager = null;
        base.OnDetachedFromVisualTree();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _overlayManager?.CloseChildren(this);
            _overlayManager?.Unregister(this);
            _overlayManager = null;
            if (_transitionStatus != TransitionStatus.Idle)
            {
                AnimationClock.Remove(this);
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Sets the transition phase and waits for it to end. <see cref="TransitionStatus.Starting"/> ends after the platform
    /// draws the starting style. <see cref="TransitionStatus.Ending"/> ends after the exit animations finish. A platform
    /// that runs transitions itself reports both through <see cref="IPortalPeer"/>. Otherwise the frames of
    /// <see cref="AnimationClock"/> report them, and without a running clock both end at once.
    /// </summary>
    private void SetTransitionStatus(TransitionStatus status)
    {
        TransitionStatus previous = _transitionStatus;
        _transitionStatus = status;
        InvalidateStyle();
        TransitionStatusChanged?.Invoke(this, EventArgs.Empty);
        if (status == TransitionStatus.Idle)
        {
            if (!IsOpen)
            {
                _overlayManager?.Unregister(this);
                _overlayManager = null;
                Anchor = null;
                _anchorPoint = null;
                if (previous == TransitionStatus.Ending)
                {
                    OnIsVisibleChanged();
                }
            }

            if (previous != TransitionStatus.Idle || !IsOpen)
            {
                OnOpenChangeComplete();
                OpenChangeCompleted?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        _renderedTransitionStatus = null;
        if (NativeHandle is IPortalPeer peer)
        {
            if (status == TransitionStatus.Starting)
            {
                peer.RequestAnimationFrame(() =>
                {
                    if (_transitionStatus == TransitionStatus.Starting)
                    {
                        SetTransitionStatus(TransitionStatus.Idle);
                    }
                });
            }
            else
            {
                peer.WaitForAnimations(() =>
                {
                    if (_transitionStatus == TransitionStatus.Ending)
                    {
                        SetTransitionStatus(TransitionStatus.Idle);
                    }
                });
            }
        }
        else if (AnimationClock.IsEnabled)
        {
            AnimationClock.Add(this);
        }
        else
        {
            SetTransitionStatus(TransitionStatus.Idle);
        }
    }

    private void OnPlacementChanged()
    {
        InvalidateStyle();
        InvalidateVisual();
    }

    private static string GetSideName(PortalSide side)
    {
        return side switch
        {
            PortalSide.Top => "top",
            PortalSide.Right => "right",
            PortalSide.Left => "left",
            _ => "bottom"
        };
    }

    private static string GetAlignName(Alignment align)
    {
        return align switch
        {
            Alignment.Center => "center",
            Alignment.End => "end",
            _ => "start"
        };
    }

}
