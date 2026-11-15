using System.Globalization;
using System.Runtime.CompilerServices;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Shows <see cref="Content"/> next to <see cref="Target"/> above all other content while <see cref="IsOpen"/> is
/// <see langword="true"/>. Entering the target with a pointer starts <see cref="ShowDelay"/> and then opens the tooltip.
/// Leaving the target starts <see cref="HideDelay"/> and then closes it. Keyboard focus on the target opens the tooltip at
/// once, and losing focus, pressing the target, or pressing Escape closes it at once. A tooltip takes no focus.
/// </summary>
/// <remarks>
/// <para>
/// Setting <see cref="SetText"/> on an element creates a <see cref="Tooltip"/> for that element with the text as its
/// content, and clearing the text removes it. When an element has both <c>Tooltip.Text</c> and a <see cref="Tooltip"/>
/// element whose <see cref="Target"/> is that element, the <see cref="Tooltip"/> element is shown.
/// </para>
/// <para>
/// A <see cref="Target"/> without a parent, such as one declared in <c>&lt;Tooltip.Target&gt;</c>, becomes a child of the
/// tooltip. A <see cref="Target"/> that already has a parent, such as one from <c>{x:Reference}</c>, stays where it is, and
/// the tooltip takes no space.
/// </para>
/// <para>
/// The tooltip has the <c>:popover-open</c> pseudo-class and <c>data-open</c> while open, and <c>data-closed</c> while
/// closed. The tooltip and its content part have <c>data-side</c> with <see cref="Side"/>, <c>data-align</c> with
/// <see cref="Align"/>, <c>data-starting-style</c> in the first frame after opening, and <c>data-ending-style</c> while the
/// content stays drawn during its exit. The target has <c>data-popup-open</c> while the tooltip is open.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public class Tooltip : Control
{
    private const string ContentPartName = "content";
    private const string ArrowPartName = "arrow";
    private static readonly TimeSpan s_defaultShowDelay = TimeSpan.FromMilliseconds(700);
    private static readonly ConditionalWeakTable<UIElement, Tooltip> s_attachedTextTooltips = new();
    private static readonly ConditionalWeakTable<UIElement, Tooltip> s_elementTooltips = new();

    static Tooltip()
    {
        WidthProperty.OverrideMetadata<Tooltip>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <c>Tooltip.Text</c> attached property.
    /// </summary>
    public static readonly BindableProperty<string?> TextProperty =
        BindableProperty.CreateAttached<Tooltip, string?>(
            "Text",
            defaultValue: null,
            propertyChanged: static (owner, _, text) => OnAttachedTextChanged(owner, text));

    /// <summary>
    /// Identifies the <see cref="Target"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> TargetProperty =
        BindableProperty.Create<Tooltip, UIElement?>(
            nameof(Target),
            defaultValue: null,
            propertyChanged: static (owner, previous, target) => owner.OnTargetChanged(previous, target));

    /// <summary>
    /// Identifies the <see cref="Content"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Create<Tooltip, object?>(
            nameof(Content),
            defaultValue: null,
            propertyChanged: static (owner, _, content) => owner.OnContentChanged(content));

    /// <summary>
    /// Identifies the <see cref="Side"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<PortalSide> SideProperty =
        BindableProperty.Create<Tooltip, PortalSide>(
            nameof(Side),
            defaultValue: PortalSide.Top,
            propertyChanged: static (owner, _, side) => owner._portal.Side = side);

    /// <summary>
    /// Identifies the <see cref="Align"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Alignment> AlignProperty =
        BindableProperty.Create<Tooltip, Alignment>(
            nameof(Align),
            defaultValue: Alignment.Center,
            propertyChanged: static (owner, _, align) => owner._portal.Align = align);

    /// <summary>
    /// Identifies the <see cref="SideOffset"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SideOffsetProperty =
        BindableProperty.Create<Tooltip, double>(
            nameof(SideOffset),
            defaultValue: 0,
            propertyChanged: static (owner, _, offset) => owner._portal.SideOffset = offset);

    /// <summary>
    /// Identifies the <see cref="ShowDelay"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TimeSpan> ShowDelayProperty =
        BindableProperty.Create<Tooltip, TimeSpan>(
            nameof(ShowDelay),
            defaultValue: s_defaultShowDelay);

    /// <summary>
    /// Identifies the <see cref="HideDelay"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TimeSpan> HideDelayProperty =
        BindableProperty.Create<Tooltip, TimeSpan>(
            nameof(HideDelay),
            defaultValue: TimeSpan.Zero);

    /// <summary>
    /// Identifies the <see cref="IsOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Create<Tooltip, bool>(
            nameof(IsOpen),
            defaultValue: false);

    /// <summary>
    /// Identifies the <see cref="HasArrow"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> HasArrowProperty =
        BindableProperty.Create<Tooltip, bool>(
            nameof(HasArrow),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.SyncArrow());

    /// <summary>
    /// Identifies the <see cref="Arrow"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> ArrowProperty =
        BindableProperty.Create<Tooltip, UIElement?>(
            nameof(Arrow),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.SyncArrow());

    /// <summary>
    /// Identifies the <see cref="ArrowPadding"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ArrowPaddingProperty =
        BindableProperty.Create<Tooltip, double>(
            nameof(ArrowPadding),
            defaultValue: 5,
            propertyChanged: static (owner, _, _) => owner._portal.InvalidateVisual());

    private readonly DispatcherTimer _showTimer = new();
    private readonly DispatcherTimer _hideTimer = new();
    private Portal _portal = null!;
    private View? _defaultArrow;
    private UIElement? _mountedArrow;
    private UIElement? _contentElement;
    private UIElement? _openRoot;
    private bool _isTargetHosted;
    private bool _isAttachedToHostView;
    private bool _isAttachedTextTooltip;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tooltip"/> class.
    /// </summary>
    public Tooltip()
    {
        _portal.Side = PortalSide.Top;
        _portal.Align = Alignment.Center;
        _portal.Closed += OnPortalClosed;
        _portal.TransitionStatusChanged += (_, _) => InvalidateStyle();
        _portal.OpenChangeCompleted += OnPortalOpenChangeCompleted;
        _showTimer.Tick += OnShowTimerTick;
        _hideTimer.Tick += OnHideTimerTick;
        UpdateOpenState(false);
    }

    /// <inheritdoc />
    public override string TagName => "tooltip";

    /// <summary>
    /// Gets or sets the element that triggers the tooltip and that the content is placed against.
    /// </summary>
    public UIElement? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the content shown while the tooltip is open. A <see cref="UIElement"/> is shown directly, and any
    /// other value is shown as a <see cref="Text"/> element.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the side of <see cref="Target"/> to open on. Defaults to <see cref="PortalSide.Top"/>.
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
    /// Gets or sets the delay between the pointer entering <see cref="Target"/> and the tooltip opening. Defaults to 700
    /// milliseconds.
    /// </summary>
    public TimeSpan ShowDelay
    {
        get => GetValue(ShowDelayProperty);
        set => SetValue(ShowDelayProperty, value);
    }

    /// <summary>
    /// Gets or sets the delay between the pointer leaving <see cref="Target"/> and the tooltip closing. Defaults to
    /// <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan HideDelay
    {
        get => GetValue(HideDelayProperty);
        set => SetValue(HideDelayProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the tooltip is open.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        private set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an arrow part is shown on the target-facing edge of the content.
    /// </summary>
    public bool HasArrow
    {
        get => GetValue(HasArrowProperty);
        set => SetValue(HasArrowProperty, value);
    }

    /// <summary>
    /// Gets or sets a custom arrow element shown on the target-facing edge of the content. Setting this shows the arrow
    /// regardless of <see cref="HasArrow"/>.
    /// </summary>
    public UIElement? Arrow
    {
        get => GetValue(ArrowProperty);
        set => SetValue(ArrowProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum distance in logical pixels between the arrow and the content's corners. Defaults to 5.
    /// </summary>
    public double ArrowPadding
    {
        get => GetValue(ArrowPaddingProperty);
        set => SetValue(ArrowPaddingProperty, value);
    }

    /// <summary>
    /// Gets the portal that shows the content, the <c>content</c> part.
    /// </summary>
    internal Portal ContentPart => _portal;

    /// <summary>
    /// Gets the active arrow element mounted in <see cref="ContentPart"/>, or <see langword="null"/> when no arrow is shown.
    /// </summary>
    internal UIElement? ArrowPart => _mountedArrow;

    /// <summary>
    /// Returns the tooltip text attached to <paramref name="element"/>, or <see langword="null"/> when none is set.
    /// </summary>
    public static string? GetText(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(TextProperty);
    }

    /// <summary>
    /// Sets the tooltip text attached to <paramref name="element"/>. Setting a non-empty string creates a
    /// <see cref="Tooltip"/> for <paramref name="element"/>, and clearing it removes that tooltip.
    /// </summary>
    public static void SetText(UIElement element, string? text)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TextProperty, text);
    }

    /// <summary>
    /// Returns an empty value for <c>data-open</c> while open and for <c>data-closed</c> while closed, and forwards
    /// <c>data-side</c>, <c>data-align</c>, <c>data-starting-style</c>, and <c>data-ending-style</c> from the content part.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (name is "data-starting-style" or "data-ending-style" or "data-side" or "data-align")
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
    internal override string? GetTemplateChildAttribute(UIElement child, string name)
    {
        if (ReferenceEquals(child, _mountedArrow) && name is "data-side" or "data-align")
        {
            return _portal.GetAttribute(name);
        }

        return base.GetTemplateChildAttribute(child, name);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _portal = DefinePart(ContentPartName, new Portal());
        Children.Add(_portal);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _showTimer.Stop();
            _hideTimer.Stop();
            DetachOpenRoot();
            if (Target is UIElement target)
            {
                DetachTarget(target);
            }

            _portal.Close();
            if (_isAttachedToHostView)
            {
                _isAttachedToHostView = false;
                (Parent as View)?.Children.Remove(this);
            }
        }

        base.Dispose(disposing);
    }

    private static void OnAttachedTextChanged(BindableObject owner, string? text)
    {
        if (owner is not UIElement element)
        {
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            if (s_attachedTextTooltips.TryGetValue(element, out Tooltip? existing))
            {
                s_attachedTextTooltips.Remove(element);
                existing.Target = null;
                existing.Dispose();
            }

            return;
        }

        if (s_attachedTextTooltips.TryGetValue(element, out Tooltip? tooltip))
        {
            tooltip.Content = text;
            return;
        }

        Tooltip created = CreateAttachedTextTooltip(element, text);
        s_attachedTextTooltips.AddOrUpdate(element, created);
    }

    private static Tooltip CreateAttachedTextTooltip(UIElement target, string text)
    {
        Tooltip tooltip = new()
        {
            _isAttachedTextTooltip = true,
            HasArrow = true,
            SideOffset = 6,
            Content = text,
            Target = target
        };
        return tooltip;
    }

    private void OnTargetChanged(UIElement? previous, UIElement? target)
    {
        _showTimer.Stop();
        _hideTimer.Stop();
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
            if (target is null)
            {
                _portal.Close();
            }
            else
            {
                _portal.Show(target);
            }
        }
    }

    private void AttachTarget(UIElement target)
    {
        if (!_isAttachedTextTooltip)
        {
            s_elementTooltips.AddOrUpdate(target, this);
            if (s_attachedTextTooltips.TryGetValue(target, out Tooltip? textTooltip))
            {
                textTooltip.CancelTimersAndClose();
            }

            if (target.Parent is null && !target.IsAncestorOf(this))
            {
                _isTargetHosted = true;
                Children.Insert(0, target);
            }
        }

        target.PointerEntered += OnTargetPointerEntered;
        target.PointerExited += OnTargetPointerExited;
        target.AddHandler(PointerPressedEvent, OnTargetPointerPressed, handledEventsToo: true);
        target.GotFocus += OnTargetGotFocus;
        target.LostFocus += OnTargetLostFocus;
        target.KeyDown += OnKeyDown;

        if (IsOpen)
        {
            target.SetState(ElementStates.PopoverOpen, true);
            target.InvalidateStyle();
        }
    }

    private void DetachTarget(UIElement target)
    {
        target.PointerEntered -= OnTargetPointerEntered;
        target.PointerExited -= OnTargetPointerExited;
        target.RemoveHandler(PointerPressedEvent, OnTargetPointerPressed);
        target.GotFocus -= OnTargetGotFocus;
        target.LostFocus -= OnTargetLostFocus;
        target.KeyDown -= OnKeyDown;

        if (!_isAttachedTextTooltip &&
            s_elementTooltips.TryGetValue(target, out Tooltip? registered) &&
            ReferenceEquals(registered, this))
        {
            s_elementTooltips.Remove(target);
        }

        if (IsOpen)
        {
            target.SetState(ElementStates.PopoverOpen, false);
            target.InvalidateStyle();
        }

        if (_isTargetHosted)
        {
            _isTargetHosted = false;
            Children.Remove(target);
        }
    }

    private void OnContentChanged(object? content)
    {
        if (_contentElement is not null)
        {
            _portal.Children.Remove(_contentElement);
        }

        UIElement? element = content switch
        {
            null => null,
            UIElement uiElement => uiElement,
            object value => new Text
            {
                Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        _contentElement = element;
        if (element is not null)
        {
            int index = _mountedArrow is not null && _portal.Children.Count > 0 ? _portal.Children.Count - 1 : _portal.Children.Count;
            _portal.Children.Insert(index, element);
        }
        else if (IsOpen)
        {
            CancelTimersAndClose();
        }
    }

    private void SyncArrow()
    {
        UIElement? nextArrow = Arrow ?? (HasArrow ? (_defaultArrow ??= new View()) : null);
        if (ReferenceEquals(_mountedArrow, nextArrow))
        {
            return;
        }

        if (_mountedArrow is UIElement previous)
        {
            previous.ClearTemplatePart();
            _portal.Children.Remove(previous);
        }

        _mountedArrow = nextArrow;
        if (nextArrow is not null)
        {
            nextArrow.Position = NativeUI.Layout.Position.Absolute;
            if (!ReferenceEquals(nextArrow.TemplatedParent, this))
            {
                nextArrow.ClearTemplatePart();
                DefinePart(ArrowPartName, nextArrow);
            }

            _portal.Children.Add(nextArrow);
        }
    }

    private void OnTargetPointerEntered(object? sender, PointerEventArgs args)
    {
        _hideTimer.Stop();
        if (IsOpen || !CanOpen())
        {
            return;
        }

        TimeSpan delay = ShowDelay;
        if (delay <= TimeSpan.Zero)
        {
            Open();
            return;
        }

        _showTimer.Interval = delay;
        _showTimer.Start();
    }

    private void OnTargetPointerExited(object? sender, PointerEventArgs args)
    {
        _showTimer.Stop();
        if (!IsOpen)
        {
            return;
        }

        TimeSpan delay = HideDelay;
        if (delay <= TimeSpan.Zero)
        {
            CancelTimersAndClose();
            return;
        }

        _hideTimer.Interval = delay;
        _hideTimer.Start();
    }

    private void OnTargetPointerPressed(object? sender, PointerEventArgs args)
    {
        CancelTimersAndClose();
    }

    private void OnTargetGotFocus(object? sender, RoutedEventArgs args)
    {
        if (Target is UIElement target &&
            target.States.HasFlag(ElementStates.FocusVisible) &&
            target.FocusState != FocusState.Pointer)
        {
            _showTimer.Stop();
            _hideTimer.Stop();
            Open();
        }
    }

    private void OnTargetLostFocus(object? sender, RoutedEventArgs args)
    {
        CancelTimersAndClose();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape)
        {
            return;
        }

        if (IsOpen && !args.Handled)
        {
            args.Handled = true;
        }

        CancelTimersAndClose();
    }

    private void OnShowTimerTick(object? sender, EventArgs args)
    {
        _showTimer.Stop();
        Open();
    }

    private void OnHideTimerTick(object? sender, EventArgs args)
    {
        _hideTimer.Stop();
        CancelTimersAndClose();
    }

    private bool CanOpen()
    {
        if (Target is not UIElement target || _contentElement is null)
        {
            return false;
        }

        if (_isAttachedTextTooltip && s_elementTooltips.TryGetValue(target, out _))
        {
            return false;
        }

        return true;
    }

    private void Open()
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        if (IsOpen || !CanOpen())
        {
            return;
        }

        UIElement target = Target!;
        if (Parent is null && FindHostView(target) is View hostView)
        {
            _isAttachedToHostView = true;
            hostView.Children.Add(this);
        }

        DetachOpenRoot();
        UIElement root = FindRoot(target);
        if (!ReferenceEquals(root, target))
        {
            _openRoot = root;
            _openRoot.AddHandler(KeyDownEvent, OnKeyDown);
        }

        IsOpen = true;
        UpdateOpenState(true);
        target.SetState(ElementStates.PopoverOpen, true);
        target.InvalidateStyle();
        _portal.Show(target);
    }

    private void CancelTimersAndClose()
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        if (IsOpen)
        {
            _portal.Close();
        }
    }

    private void OnPortalClosed(object? sender, EventArgs args)
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        DetachOpenRoot();
        if (Target is UIElement target)
        {
            target.SetState(ElementStates.PopoverOpen, false);
            target.InvalidateStyle();
        }

        IsOpen = false;
        UpdateOpenState(false);
    }

    private void OnPortalOpenChangeCompleted(object? sender, EventArgs args)
    {
        if (!IsOpen && _isAttachedToHostView)
        {
            _isAttachedToHostView = false;
            (Parent as View)?.Children.Remove(this);
        }
    }

    private void DetachOpenRoot()
    {
        if (_openRoot is not null)
        {
            _openRoot.RemoveHandler(KeyDownEvent, OnKeyDown);
            _openRoot = null;
        }
    }

    private void UpdateOpenState(bool isOpen)
    {
        SetState(ElementStates.PopoverOpen, isOpen);
        InvalidateStyle();
    }

    private static View? FindHostView(UIElement target)
    {
        View? topView = null;
        for (UIElement? current = target.Parent; current is not null; current = current.Parent)
        {
            if (current is View view)
            {
                if (view.StyleSheet is not null || current is IWindowHostedElement { HasOwnWindow: true })
                {
                    return view;
                }

                topView = view;
            }
        }

        return topView ?? Application.Current.MainView as View;
    }

    private static UIElement FindRoot(UIElement element)
    {
        UIElement current = element;
        while (current.Parent is UIElement parent)
        {
            current = parent;
        }

        return current;
    }
}
