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
/// child of its parent for <see cref="BindableObject.DataContext"/>. The desktop renderer draws it after the visual tree;
/// on Web it is mounted at the end of the <c>native-ui</c> element, so selectors through its parent do not match its content there.
/// </summary>
public class Portal : View
{
    private static readonly List<Portal> s_openPortals = [];

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
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="Side"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<PortalSide> SideProperty =
        BindableProperty.Create<Portal, PortalSide>(
            nameof(Side),
            defaultValue: PortalSide.Bottom,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="Align"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Alignment> AlignProperty =
        BindableProperty.Create<Portal, Alignment>(
            nameof(Align),
            defaultValue: Alignment.Start,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

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

    /// <inheritdoc />
    public override string TagName => "portal";

    /// <inheritdoc />
    public override bool IsVisible => base.IsVisible && IsOpen;

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
    /// Gets a value indicating whether a press outside this portal and its anchor closes it.
    /// </summary>
    internal virtual bool ClosesOnOutsidePress => true;

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
        IsOpen = true;
        Opened?.Invoke(this, EventArgs.Empty);
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

        s_openPortals.Remove(this);
        Anchor = null;
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
        OnClosed();
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
        }

        base.Dispose(disposing);
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
}
