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
/// the first frame after it opens, so its transitions run from the styles that select that attribute. When it closes, it
/// has <c>data-ending-style</c> and stays drawn until its own transitions and its animations with a finite number of
/// iterations end. It takes no input during that time.
/// </remarks>
public class Portal : View, IPortalLayoutNode
{
    private static readonly List<Portal> s_openPortals = [];

    private readonly MotionFrameTarget _motionFrameTarget;
    private bool _isStarting;
    private bool _isEnding;
    private bool _hasRenderedSinceMotion;

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
    /// Occurs after <see cref="Show"/> opens this portal.
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
    /// Occurs when <see cref="IsStarting"/> or <see cref="IsEnding"/> changes.
    /// </summary>
    internal event EventHandler? MotionChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Portal"/> class.
    /// </summary>
    public Portal()
    {
        _motionFrameTarget = new MotionFrameTarget(this);
    }

    /// <inheritdoc />
    public override string TagName => "portal";

    /// <inheritdoc />
    public override bool IsVisible => base.IsVisible && (IsOpen || _isEnding);

    /// <summary>
    /// Gets a value indicating whether the portal shows its first frame after opening, with <c>data-starting-style</c>.
    /// </summary>
    internal bool IsStarting => _isStarting;

    /// <summary>
    /// Gets a value indicating whether the portal is closed and stays drawn, with <c>data-ending-style</c>, until its
    /// exit transitions and animations end.
    /// </summary>
    internal bool IsEnding => _isEnding;

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
                return _isStarting ? string.Empty : null;
            case "data-ending-style":
                return _isEnding ? string.Empty : null;
        }

        if (string.Equals(name, "data-side", StringComparison.Ordinal))
        {
            return Anchor is null ? null : GetSideName(Side);
        }

        if (string.Equals(name, "data-align", StringComparison.Ordinal))
        {
            return Anchor is null ? null : GetAlignName(Align);
        }

        return base.GetAttribute(name);
    }

    /// <summary>
    /// Gets a value indicating whether a press outside this portal and its anchor closes it.
    /// </summary>
    internal virtual bool ClosesOnOutsidePress => true;

    /// <summary>
    /// Gets a value indicating whether this portal shows <c>data-starting-style</c> when it opens and waits for its exit
    /// motion when it closes.
    /// </summary>
    private protected virtual bool HasMotion => true;

    /// <summary>
    /// Opens this portal next to <paramref name="anchor"/>, or centered when it is <see langword="null"/>. Calling this
    /// while open moves the portal to the new anchor.
    /// </summary>
    public void Show(UIElement? anchor = null)
    {
        if (IsOpen)
        {
            Anchor = anchor;
            InvalidateVisual();
            return;
        }

        OnOpening();
        Anchor = anchor;
        s_openPortals.Add(this);

        // A portal that opens again during its exit transitions back from its current style.
        bool wasEnding = _isEnding;
        _isEnding = false;
        _isStarting = HasMotion && !wasEnding;
        IsOpen = true;
        Opened?.Invoke(this, EventArgs.Empty);
        StartMotion();
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

        Closing?.Invoke(this, EventArgs.Empty);
        s_openPortals.Remove(this);
        if (States.HasFlag(ElementStates.FocusWithin))
        {
            FindFocusManager()?.ReleaseFocus(this);
        }

        _isStarting = false;
        _isEnding = HasMotion && IsVisibleInTree;
        if (!_isEnding)
        {
            Anchor = null;
        }

        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
        OnClosed();
        StartMotion();
    }

    /// <inheritdoc />
    internal override void MarkRendered()
    {
        base.MarkRendered();
        _hasRenderedSinceMotion = true;
    }

    /// <summary>
    /// Runs before this portal opens. An exception leaves it closed.
    /// </summary>
    private protected virtual void OnOpening()
    {
    }

    /// <summary>
    /// Runs after this portal closes and the <see cref="Closed"/> handlers have run.
    /// </summary>
    private protected virtual void OnClosed()
    {
    }

    /// <summary>
    /// Closes the open portals that a pointer press on <paramref name="target"/> falls outside of.
    /// </summary>
    internal static void DismissForPress(UIElement? target)
    {
        foreach (Portal portal in s_openPortals.ToArray())
        {
            if (portal.ClosesOnOutsidePress && !IsWithin(target, portal) && !IsWithin(target, portal.Anchor))
            {
                portal.Close();
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            s_openPortals.Remove(this);
            if (_isStarting || _isEnding)
            {
                AnimationClock.Remove(_motionFrameTarget);
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Ends the starting style or the exit after the platform draws them: through <see cref="IPortalPeer"/> on a platform
    /// that runs transitions itself, and otherwise on the frames of <see cref="AnimationClock"/>. Without either, both
    /// end at once.
    /// </summary>
    private void StartMotion()
    {
        InvalidateStyle();
        MotionChanged?.Invoke(this, EventArgs.Empty);
        if (!_isStarting && !_isEnding)
        {
            return;
        }

        _hasRenderedSinceMotion = false;
        if (NativeHandle is IPortalPeer peer)
        {
            if (_isStarting)
            {
                peer.WaitForStartingStyle(EndStartingStyle);
            }
            else
            {
                peer.WaitForEndingMotion(EndExit);
            }
        }
        else if (AnimationClock.IsEnabled)
        {
            AnimationClock.Add(_motionFrameTarget);
        }
        else
        {
            EndStartingStyle();
            EndExit();
        }
    }

    /// <summary>
    /// Advances the motion on a frame of <see cref="AnimationClock"/> after the portal has been drawn in its current state.
    /// </summary>
    private void OnMotionFrame()
    {
        if (_hasRenderedSinceMotion)
        {
            if (_isStarting)
            {
                EndStartingStyle();
            }
            else if (_isEnding && !HasFiniteMotion)
            {
                EndExit();
            }
        }

        if (!_isStarting && !_isEnding)
        {
            AnimationClock.Remove(_motionFrameTarget);
        }
    }

    private void EndStartingStyle()
    {
        if (_isStarting)
        {
            _isStarting = false;
            InvalidateStyle();
            MotionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EndExit()
    {
        if (_isEnding)
        {
            _isEnding = false;
            Anchor = null;
            OnIsVisibleChanged();
            MotionChanged?.Invoke(this, EventArgs.Empty);
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

    private static bool IsWithin(UIElement? element, UIElement? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Receives the frames of <see cref="AnimationClock"/> while the portal waits for its starting style to be drawn or
    /// for its exit motion to end.
    /// </summary>
    private sealed class MotionFrameTarget(Portal portal) : IFrameTarget
    {
        public UIElement Target => portal;

        public void Tick(double now)
        {
            portal.OnMotionFrame();
        }
    }
}
