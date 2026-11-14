using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Shows <see cref="Content"/> next to <see cref="Target"/> above all other content while <see cref="IsOpen"/> is
/// <see langword="true"/>. A click on the target toggles the popover, and Escape or a press outside the content and the
/// target closes it. Opening moves focus to the first focusable element of the content, and closing returns focus to the
/// target when focus is inside the content.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Target"/> without a parent, such as one declared in <c>&lt;Popover.Target&gt;</c>, becomes a child of the
/// popover. A <see cref="Target"/> that already has a parent, such as one from <c>{x:Reference}</c>, stays where it is, and
/// the popover takes no space.
/// </para>
/// <para>
/// The popover has the <c>:popover-open</c> pseudo-class and <c>data-open</c> while open, and <c>data-closed</c> while
/// closed. The content part has <c>data-side</c> with <see cref="Side"/> and <c>data-align</c> with <see cref="Align"/>.
/// The content opens on the opposite side when it does not fit on <see cref="Side"/> and that side has more room. The
/// target has <c>data-popup-open</c> while the popover is open.
/// </para>
/// <para>
/// The popover and its content part have <c>data-starting-style</c> in the first frame after opening and
/// <c>data-ending-style</c> while the content stays drawn during its exit. Transitions on the content part run from and to
/// the styles that <c>Popover[data-starting-style]::part(content)</c> and <c>Popover[data-ending-style]::part(content)</c> set.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public class Popover : Control
{
    private const string ContentPartName = "content";

    static Popover()
    {
        WidthProperty.OverrideMetadata<Popover>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <see cref="Target"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> TargetProperty =
        BindableProperty.Create<Popover, UIElement?>(
            nameof(Target),
            defaultValue: null,
            propertyChanged: static (owner, previous, target) => owner.OnTargetChanged(previous, target));

    /// <summary>
    /// Identifies the <see cref="Content"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Create<Popover, UIElement?>(
            nameof(Content),
            defaultValue: null,
            propertyChanged: static (owner, _, content) => owner.OnContentChanged(content));

    /// <summary>
    /// Identifies the <see cref="IsOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Create<Popover, bool>(
            nameof(IsOpen),
            defaultValue: false,
            propertyChanged: static (owner, _, isOpen) => owner.OnIsOpenChanged(isOpen));

    /// <summary>
    /// Identifies the <see cref="Side"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<PortalSide> SideProperty =
        BindableProperty.Create<Popover, PortalSide>(
            nameof(Side),
            defaultValue: PortalSide.Bottom,
            propertyChanged: static (owner, _, side) => owner._portal.Side = side);

    /// <summary>
    /// Identifies the <see cref="Align"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Alignment> AlignProperty =
        BindableProperty.Create<Popover, Alignment>(
            nameof(Align),
            defaultValue: Alignment.Center,
            propertyChanged: static (owner, _, align) => owner._portal.Align = align);

    /// <summary>
    /// Identifies the <see cref="SideOffset"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SideOffsetProperty =
        BindableProperty.Create<Popover, double>(
            nameof(SideOffset),
            defaultValue: 0,
            propertyChanged: static (owner, _, offset) => owner._portal.SideOffset = offset);

    /// <summary>
    /// Identifies the <see cref="AlignOffset"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AlignOffsetProperty =
        BindableProperty.Create<Popover, double>(
            nameof(AlignOffset),
            defaultValue: 0,
            propertyChanged: static (owner, _, offset) => owner._portal.AlignOffset = offset);

    private Portal _portal = null!;
    private IDisposable? _targetGesture;
    private bool _isTargetHosted;

    /// <summary>
    /// Initializes a new instance of the <see cref="Popover"/> class.
    /// </summary>
    public Popover()
    {
        _portal.Align = Alignment.Center;
        _portal.Opened += (_, _) => Opened?.Invoke(this, EventArgs.Empty);
        _portal.Closing += OnPortalClosing;
        _portal.Closed += OnPortalClosed;
        _portal.MotionChanged += (_, _) => InvalidateStyle();
        KeyDown += OnKeyDown;
        UpdateOpenState(false);
    }

    /// <summary>
    /// Occurs after the popover opens.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Occurs after the popover closes, whatever closed it.
    /// </summary>
    public event EventHandler? Closed;

    /// <inheritdoc />
    public override string TagName => "popover";

    /// <summary>
    /// Gets or sets the element that opens the popover and that the content is placed against.
    /// </summary>
    public UIElement? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the element shown while the popover is open.
    /// </summary>
    public UIElement? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the content is shown.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the side of <see cref="Target"/> to open on. Defaults to <see cref="PortalSide.Bottom"/>.
    /// </summary>
    public PortalSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>
    /// Gets or sets how the content lines up along the chosen side of <see cref="Target"/>. Defaults to
    /// <see cref="Alignment.Center"/>.
    /// </summary>
    public Alignment Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    /// <summary>
    /// Gets or sets the gap between <see cref="Target"/> and the content in logical pixels.
    /// </summary>
    public double SideOffset
    {
        get => GetValue(SideOffsetProperty);
        set => SetValue(SideOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the shift along the target's edge in logical pixels, measured away from the edge that
    /// <see cref="Align"/> selects. With <see cref="Alignment.Center"/>, a positive value moves right or down.
    /// </summary>
    public double AlignOffset
    {
        get => GetValue(AlignOffsetProperty);
        set => SetValue(AlignOffsetProperty, value);
    }

    /// <summary>
    /// Gets the portal that shows the content, the <c>content</c> part.
    /// </summary>
    internal Portal ContentPart => _portal;

    /// <summary>
    /// Returns an empty value for <c>data-open</c> while open and for <c>data-closed</c> while closed, and for
    /// <c>data-starting-style</c> and <c>data-ending-style</c> while the content part has them. A selector such as
    /// <c>Popover[data-starting-style]::part(content)</c> styles the content during its motion.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (name is "data-starting-style" or "data-ending-style")
        {
            return _portal.GetAttribute(name);
        }

        if (string.Equals(name, "data-open", StringComparison.Ordinal))
        {
            return IsOpen ? string.Empty : null;
        }

        if (string.Equals(name, "data-closed", StringComparison.Ordinal))
        {
            return IsOpen ? null : string.Empty;
        }

        return base.GetAttribute(name);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _portal = DefinePart(new Portal { Focusable = true, TabIndex = -1 }, ContentPartName);
        Children.Add(_portal);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Target is UIElement target)
            {
                DetachTarget(target);
            }

            _portal.Close();
        }

        base.Dispose(disposing);
    }

    private void OnTargetChanged(UIElement? previous, UIElement? target)
    {
        if (previous is not null)
        {
            DetachTarget(previous);
        }

        if (target is not null)
        {
            AttachTarget(target);
        }

        if (IsOpen)
        {
            _portal.Show(target);
        }
    }

    private void AttachTarget(UIElement target)
    {
        if (target.Parent is null)
        {
            _isTargetHosted = true;
            Children.Insert(0, target);
        }

        // A button handles Enter and Space itself and marks them handled, so its Click event reports activation.
        if (target is Button button)
        {
            button.Click += OnTargetClick;
        }
        else
        {
            _targetGesture = ClickGesture.Attach(target, Toggle);
        }

        if (IsOpen)
        {
            target.SetPopupOpen(true);
        }
    }

    private void DetachTarget(UIElement target)
    {
        if (target is Button button)
        {
            button.Click -= OnTargetClick;
        }

        _targetGesture?.Dispose();
        _targetGesture = null;
        if (IsOpen)
        {
            target.SetPopupOpen(false);
        }

        if (_isTargetHosted)
        {
            _isTargetHosted = false;
            Children.Remove(target);
        }
    }

    private void OnContentChanged(UIElement? content)
    {
        _portal.Children.Clear();
        if (content is not null)
        {
            _portal.Children.Add(content);
        }
    }

    private void OnTargetClick(object? sender, RoutedEventArgs args)
    {
        Toggle();
    }

    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    private void OnIsOpenChanged(bool isOpen)
    {
        UpdateOpenState(isOpen);
        Target?.SetPopupOpen(isOpen);
        if (!isOpen)
        {
            _portal.Close();
            return;
        }

        _portal.Show(Target);
        if (FindFocusManager() is FocusManager focusManager && !focusManager.FocusFirst(_portal))
        {
            _portal.Focus();
        }
    }

    private void UpdateOpenState(bool isOpen)
    {
        SetState(ElementStates.PopoverOpen, isOpen);
        InvalidateStyle();
    }

    /// <summary>
    /// Returns focus to the target when focus is inside the content that is closing.
    /// </summary>
    private void OnPortalClosing(object? sender, EventArgs args)
    {
        if (_portal.States.HasFlag(ElementStates.FocusWithin))
        {
            Target?.Focus();
        }
    }

    private void OnPortalClosed(object? sender, EventArgs args)
    {
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && IsOpen && !args.Handled)
        {
            args.Handled = true;
            IsOpen = false;
        }
    }
}
