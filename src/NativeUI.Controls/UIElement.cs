using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Base class for all visual elements in NativeUI, participating in visual-tree
/// <see cref="IStyleResolver"/> inheritance, inline C# <see cref="Controls.Style"/>, desktop layout/rendering,
/// and Web DOM synchronization.
/// </summary>
public abstract class UIElement : BindableObject, ILayoutNode, IUIElement
{
    private IReadOnlyList<string> _classes = [];
    private ComputedStyle _computedStyle = ComputedStyle.Default;
    private SelectionStyle _selectionStyle = SelectionStyle.Default;
    private Theme _computedTheme = Theme.Light;
    private UIElement? _parent;
    private IStyleResolver? _styleResolver;
    private Dictionary<RoutedEvent, RoutedHandler[]>? _routedHandlers;
    private ElementAnimator? _animator;
    private static readonly TimeSpan s_defaultAnimationDuration = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Identifies the <see cref="PointerPressed"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<PointerEventArgs> PointerPressedEvent =
        RoutedEvent.Register<UIElement, PointerEventArgs>(nameof(PointerPressed), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="PointerReleased"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<PointerEventArgs> PointerReleasedEvent =
        RoutedEvent.Register<UIElement, PointerEventArgs>(nameof(PointerReleased), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="PointerMoved"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<PointerEventArgs> PointerMovedEvent =
        RoutedEvent.Register<UIElement, PointerEventArgs>(nameof(PointerMoved), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="PointerEntered"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<PointerEventArgs> PointerEnteredEvent =
        RoutedEvent.Register<UIElement, PointerEventArgs>(nameof(PointerEntered), RoutingStrategy.Direct);

    /// <summary>
    /// Identifies the <see cref="PointerExited"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<PointerEventArgs> PointerExitedEvent =
        RoutedEvent.Register<UIElement, PointerEventArgs>(nameof(PointerExited), RoutingStrategy.Direct);

    /// <summary>
    /// Identifies the <see cref="Id"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> IdProperty =
        BindableProperty.Create<UIElement, string?>(nameof(Id), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="Class"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<StyleClass> ClassProperty =
        BindableProperty.Create<UIElement, StyleClass>(
            nameof(Class),
            defaultValue: StyleClass.Empty,
            propertyChanged: static (owner, _, newValue) => owner.UpdateClassList(newValue));

    /// <summary>
    /// Identifies the <see cref="IsVisible"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsVisibleProperty =
        BindableProperty.Create<UIElement, bool>(
            nameof(IsVisible),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="IsEnabled"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsEnabledProperty =
        BindableProperty.Create<UIElement, bool>(
            nameof(IsEnabled),
            defaultValue: true,
            propertyChanged: static (owner, _, newValue) => owner.OnIsEnabledPropertyChanged(newValue));

    /// <summary>
    /// Identifies the <see cref="Style"/> bindable property for inline C# styling.
    /// </summary>
    public static readonly BindableProperty<Style?> StyleProperty =
        BindableProperty.Create<UIElement, Style?>(
            nameof(Style),
            defaultValue: null,
            propertyChanged: static (owner, oldStyle, newStyle) => owner.OnInlineStyleReplaced(oldStyle, newStyle));

    /// <summary>
    /// Identifies the <see cref="Width"/> property, which is <see cref="Style.WidthProperty"/>.
    /// </summary>
    public static readonly BindableProperty<WidthValue?> WidthProperty = Style.WidthProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Height"/> property, which is <see cref="Style.HeightProperty"/>.
    /// </summary>
    public static readonly BindableProperty<double?> HeightProperty = Style.HeightProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="OverflowX"/> property, which is <see cref="Style.OverflowXProperty"/>.
    /// </summary>
    public static readonly BindableProperty<Overflow?> OverflowXProperty = Style.OverflowXProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="OverflowY"/> property, which is <see cref="Style.OverflowYProperty"/>.
    /// </summary>
    public static readonly BindableProperty<Overflow?> OverflowYProperty = Style.OverflowYProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Position"/> property, which is <see cref="Style.PositionProperty"/>.
    /// </summary>
    public static readonly BindableProperty<Position?> PositionProperty = Style.PositionProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Top"/> property, which is <see cref="Style.TopProperty"/>.
    /// </summary>
    public static readonly BindableProperty<double?> TopProperty = Style.TopProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Right"/> property, which is <see cref="Style.RightProperty"/>.
    /// </summary>
    public static readonly BindableProperty<double?> RightProperty = Style.RightProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Bottom"/> property, which is <see cref="Style.BottomProperty"/>.
    /// </summary>
    public static readonly BindableProperty<double?> BottomProperty = Style.BottomProperty.AddOwner<UIElement>();

    /// <summary>
    /// Identifies the <see cref="Left"/> property, which is <see cref="Style.LeftProperty"/>.
    /// </summary>
    public static readonly BindableProperty<double?> LeftProperty = Style.LeftProperty.AddOwner<UIElement>();

    /// <summary>
    /// Occurs whenever a visual property or computed style on this element changes.
    /// </summary>
    public event EventHandler? VisualInvalidated;

    /// <summary>
    /// Occurs when a pointer button is pressed over this element or one of its descendants.
    /// </summary>
    public event EventHandler<PointerEventArgs> PointerPressed
    {
        add => AddHandler(PointerPressedEvent, value);
        remove => RemoveHandler(PointerPressedEvent, value);
    }

    /// <summary>
    /// Occurs when a pointer button is released over this element or one of its descendants.
    /// </summary>
    public event EventHandler<PointerEventArgs> PointerReleased
    {
        add => AddHandler(PointerReleasedEvent, value);
        remove => RemoveHandler(PointerReleasedEvent, value);
    }

    /// <summary>
    /// Occurs when a pointer moves over this element or one of its descendants.
    /// </summary>
    public event EventHandler<PointerEventArgs> PointerMoved
    {
        add => AddHandler(PointerMovedEvent, value);
        remove => RemoveHandler(PointerMovedEvent, value);
    }

    /// <summary>
    /// Occurs when a pointer enters the bounds of this element or one of its descendants.
    /// </summary>
    public event EventHandler<PointerEventArgs> PointerEntered
    {
        add => AddHandler(PointerEnteredEvent, value);
        remove => RemoveHandler(PointerEnteredEvent, value);
    }

    /// <summary>
    /// Occurs when a pointer leaves the bounds of this element and its descendants.
    /// </summary>
    public event EventHandler<PointerEventArgs> PointerExited
    {
        add => AddHandler(PointerExitedEvent, value);
        remove => RemoveHandler(PointerExitedEvent, value);
    }

    /// <summary>
    /// Gets or sets the unique element identifier.
    /// </summary>
    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>
    /// Gets or sets the style class names applied to this element.
    /// </summary>
    public StyleClass Class
    {
        get => GetValue(ClassProperty);
        set => SetValue(ClassProperty, value);
    }

    /// <summary>
    /// Gets the parsed list of individual class names on this element.
    /// </summary>
    public IReadOnlyList<string> Classes => _classes;

    /// <summary>
    /// Gets or sets a value indicating whether this element is visible.
    /// </summary>
    public virtual bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this element can respond to user interaction.
    /// </summary>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Gets the mouse pointer cursor appearance resolved for this element.
    /// </summary>
    public virtual Cursor Cursor => ComputedStyle.Cursor;

    /// <summary>
    /// Gets or sets the inline C# <see cref="Controls.Style"/> applied to this element.
    /// </summary>
    public Style? Style
    {
        get => GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline width, the same value as <see cref="Controls.Style.Width"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public WidthValue? Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline height, the same value as <see cref="Controls.Style.Height"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public double? Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline horizontal overflow, the same value as <see cref="Controls.Style.OverflowX"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public Overflow? OverflowX
    {
        get => GetValue(OverflowXProperty);
        set => SetValue(OverflowXProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline vertical overflow, the same value as <see cref="Controls.Style.OverflowY"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public Overflow? OverflowY
    {
        get => GetValue(OverflowYProperty);
        set => SetValue(OverflowYProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline positioning scheme, the same value as <see cref="Controls.Style.Position"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public Position? Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline top offset, the same value as <see cref="Controls.Style.Top"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public double? Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline right offset, the same value as <see cref="Controls.Style.Right"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public double? Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline bottom offset, the same value as <see cref="Controls.Style.Bottom"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public double? Bottom
    {
        get => GetValue(BottomProperty);
        set => SetValue(BottomProperty, value);
    }

    /// <summary>
    /// Gets or sets the inline left offset, the same value as <see cref="Controls.Style.Left"/> on <see cref="Style"/>.
    /// Setting it creates <see cref="Style"/> when none exists.
    /// </summary>
    public double? Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    /// <inheritdoc />
    public virtual double? FontSize => ComputedStyle.FontSize;

    /// <summary>
    /// Gets or sets the <see cref="IStyleResolver"/> used to resolve styles for this element and its descendants.
    /// Inherits from <see cref="Parent"/> when not explicitly set on this instance.
    /// </summary>
    public IStyleResolver? StyleResolver
    {
        get => _styleResolver ?? _parent?.StyleResolver ?? Media.StyleResolver.Default;
        set
        {
            if (ReferenceEquals(_styleResolver, value))
            {
                return;
            }

            _styleResolver = value;
            InvalidateStyle();
        }
    }

    /// <summary>
    /// Gets the parent <see cref="UIElement"/> in the visual tree, or <see langword="null"/> if this is a root element.
    /// </summary>
    public UIElement? Parent
    {
        get => _parent;
        internal set
        {
            if (ReferenceEquals(_parent, value))
            {
                return;
            }

            _parent = value;
            OnParentChanged();
        }
    }

    /// <summary>
    /// Gets the platform-owned native handle or browser DOM proxy associated with this element.
    /// </summary>
    public INativePeer? NativePeer { get; internal set; }

    /// <summary>
    /// Gets the canonical NativeUI tag name used by CSS type selectors, such as <c>view</c> or <c>text</c>.
    /// </summary>
    public abstract string TagName { get; }

    /// <inheritdoc />
    public ComputedStyle ComputedStyle => _computedStyle;

    /// <inheritdoc />
    public SelectionStyle SelectionStyle => _selectionStyle;

    /// <inheritdoc />
    public string StyleSheetId => FindScopeTypeName();

    IUIElement? IUIElement.Parent => Parent;

    IStyle? IUIElement.Style => Style;

    WidthValue? IUIElement.Width => Width;

    double? IUIElement.Height => Height;

    Overflow? IUIElement.OverflowX => OverflowX;

    Overflow? IUIElement.OverflowY => OverflowY;

    Position? IUIElement.Position => Position;

    double? IUIElement.Top => Top;

    double? IUIElement.Right => Right;

    double? IUIElement.Bottom => Bottom;

    double? IUIElement.Left => Left;

    ComputedStyle IUIElement.ComputedStyle => ComputedStyle;

    SelectionStyle IUIElement.SelectionStyle => SelectionStyle;

    /// <inheritdoc />
    public ElementStates States { get; private set; }

    /// <summary>
    /// Plays the <c>@keyframes</c> rule called <paramref name="name"/> on top of this element's style.
    /// </summary>
    /// <param name="name">The keyframes name.</param>
    /// <param name="duration">The duration of one iteration. Defaults to 300 milliseconds.</param>
    /// <param name="easing">The timing function. Defaults to <see cref="Easing.Ease"/>.</param>
    /// <param name="iterations">The number of iterations, or <see cref="double.PositiveInfinity"/> to repeat until cancelled.</param>
    public Animation Animate(string name, TimeSpan? duration = null, Easing? easing = null, double iterations = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        AnimationSpec spec = new(
            name,
            (duration ?? s_defaultAnimationDuration).TotalMilliseconds,
            0,
            easing ?? Easing.Ease,
            iterations,
            AnimationDirection.Normal,
            AnimationFillMode.None);

        Animation animation = new();
        if (NativePeer is IAnimationPeer peer)
        {
            peer.Animate(spec, animation);
        }
        else if (AnimationClock.IsEnabled && StyleResolver?.ResolveKeyframes(name, this) is { } frames)
        {
            _animator ??= new ElementAnimator(this);
            _animator.Start(spec, frames, animation);
        }
        else
        {
            animation.Complete();
        }

        return animation;
    }

    /// <summary>
    /// Gets a value indicating whether this element and all its ancestors are visible.
    /// </summary>
    internal bool IsVisibleInTree
    {
        get
        {
            for (UIElement? current = this; current is not null; current = current._parent)
            {
                if (!current.IsVisible)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a host has drawn this element, after which style changes can transition.
    /// </summary>
    internal bool HasRendered { get; private set; }

    /// <summary>
    /// Shows a style that <see cref="ElementAnimator"/> produced. Descendants recompute only when an inherited value changed.
    /// </summary>
    internal void ApplyAnimatedStyle(ComputedStyle style)
    {
        bool inheritedChanged = style.TextColor != _computedStyle.TextColor;
        UpdateComputedStyle(style, _selectionStyle, _computedTheme);
        if (inheritedChanged)
        {
            OnInheritedStyleChanged();
        }
    }

    /// <summary>
    /// Recomputes the styles that inherit from this element.
    /// </summary>
    internal virtual void OnInheritedStyleChanged()
    {
    }

    /// <summary>
    /// Records that a host drew this element.
    /// </summary>
    internal void MarkRendered()
    {
        HasRendered = true;
    }

    /// <summary>
    /// Sets or clears <paramref name="state"/> without recomputing styles, so a host can update a complete path first.
    /// </summary>
    internal void SetState(ElementStates state, bool enabled)
    {
        States = enabled ? States | state : States & ~state;
    }

    /// <inheritdoc />
    string? ILayoutNode.TextContent => GetLayoutTextContent();

    /// <inheritdoc />
    IReadOnlyList<ILayoutNode> ILayoutNode.LayoutChildren => GetLayoutChildren();

    /// <summary>
    /// Invalidates and recomputes the element's <see cref="ComputedStyle"/> and propagates style invalidation.
    /// </summary>
    public virtual void InvalidateStyle()
    {
        RecomputeStyle();
    }

    /// <summary>
    /// Recomputes the element's <see cref="ComputedStyle"/> and <see cref="SelectionStyle"/> using its effective
    /// <see cref="StyleResolver"/>, <see cref="Application.CurrentTheme"/>, and inline <see cref="Style"/> overrides.
    /// </summary>
    public virtual void RecomputeStyle()
    {
        ComputedStyle computedStyle = ApplyAnimations(ResolveComputedStyle());
        SelectionStyle selectionStyle = ResolveSelectionStyle();
        UpdateComputedStyle(computedStyle, selectionStyle, Application.Current.CurrentTheme);
    }

    /// <summary>
    /// Adds a handler for <paramref name="routedEvent"/>.
    /// </summary>
    /// <param name="routedEvent">The event to handle.</param>
    /// <param name="handler">The handler to invoke with the current route element as its sender.</param>
    /// <param name="handledEventsToo">Whether to invoke the handler after an earlier handler sets <see cref="RoutedEventArgs.Handled"/>.</param>
    public void AddHandler<TArgs>(RoutedEvent<TArgs> routedEvent, EventHandler<TArgs> handler, bool handledEventsToo = false)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);

        _routedHandlers ??= [];
        RoutedHandler[] existing = _routedHandlers.GetValueOrDefault(routedEvent, []);
        _routedHandlers[routedEvent] = [.. existing, new RoutedHandler(handler, handledEventsToo)];
    }

    /// <summary>
    /// Removes the most recently added registration of <paramref name="handler"/> for <paramref name="routedEvent"/>.
    /// </summary>
    public void RemoveHandler<TArgs>(RoutedEvent<TArgs> routedEvent, EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);

        if (_routedHandlers is null || !_routedHandlers.TryGetValue(routedEvent, out RoutedHandler[]? existing))
        {
            return;
        }

        int index = Array.FindLastIndex(existing, entry => Equals(entry.Handler, handler));
        if (index < 0)
        {
            return;
        }

        if (existing.Length == 1)
        {
            _routedHandlers.Remove(routedEvent);
            return;
        }

        _routedHandlers[routedEvent] = [.. existing[..index], .. existing[(index + 1)..]];
    }

    /// <summary>
    /// Raises <paramref name="routedEvent"/> from this element along its registered route.
    /// The route and its handlers are captured first, so changes made by a handler affect only subsequent events.
    /// </summary>
    public void RaiseEvent<TArgs>(RoutedEvent<TArgs> routedEvent, TArgs args)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(args);

        args.BeginRoute(routedEvent, this);
        (UIElement Element, RoutedHandler[] Handlers)[] route = CreateRoute(routedEvent);
        foreach ((UIElement element, RoutedHandler[] handlers) in route)
        {
            foreach (RoutedHandler entry in handlers)
            {
                if (!args.Handled || entry.HandledEventsToo)
                {
                    ((EventHandler<TArgs>)entry.Handler)(element, args);
                }
            }
        }
    }

    /// <summary>
    /// Invoked when the <see cref="Parent"/> of this element changes.
    /// </summary>
    protected virtual void OnParentChanged()
    {
        SetInheritedDataContext(_parent?.DataContext);
        if (_parent is null)
        {
            _animator?.Cancel();
        }

        InvalidateStyle();
    }

    /// <summary>
    /// Signals that this element's visual appearance or layout requires an incremental update.
    /// </summary>
    public void InvalidateVisual()
    {
        VisualInvalidated?.Invoke(this, EventArgs.Empty);
        _parent?.InvalidateVisual();
    }

    /// <summary>
    /// Returns the text content used by <see cref="LayoutEngine"/> when measuring leaf text controls.
    /// </summary>
    protected virtual string? GetLayoutTextContent()
    {
        return null;
    }

    /// <summary>
    /// Returns the child layout nodes for container controls.
    /// </summary>
    protected virtual IReadOnlyList<ILayoutNode> GetLayoutChildren()
    {
        return [];
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animator?.Cancel();
            _animator = null;
            NativePeer?.Dispose();
            NativePeer = null;
            _routedHandlers = null;
        }

        base.Dispose(disposing);
    }

    private (UIElement Element, RoutedHandler[] Handlers)[] CreateRoute(RoutedEvent routedEvent)
    {
        if (routedEvent.RoutingStrategy == RoutingStrategy.Direct)
        {
            return [(this, GetHandlers(routedEvent))];
        }

        List<(UIElement Element, RoutedHandler[] Handlers)> route = [];
        for (UIElement? current = this; current is not null; current = current._parent)
        {
            route.Add((current, current.GetHandlers(routedEvent)));
        }

        if (routedEvent.RoutingStrategy == RoutingStrategy.Tunnel)
        {
            route.Reverse();
        }

        return [.. route];
    }

    private RoutedHandler[] GetHandlers(RoutedEvent routedEvent)
    {
        return _routedHandlers?.GetValueOrDefault(routedEvent) ?? [];
    }

    private SelectionStyle ResolveSelectionStyle()
    {
        return StyleResolver?.ResolveSelectionStyle(this) ?? _parent?.SelectionStyle ?? SelectionStyle.Default;
    }

    private ComputedStyle ResolveComputedStyle()
    {
        IStyleResolver? resolver = StyleResolver;
        ComputedStyle resolvedStyle = resolver?.ResolveStyle(this) ?? ComputedStyle.CreateInitial(this);
        ComputedStyle cascadedStyle = ViewStyleResolver.Resolve(this, resolvedStyle, resolver);
        return ResolveAutoSizes(cascadedStyle.ApplyInline(Style, resolver, this));
    }

    /// <summary>
    /// Replaces an automatic size with the default of this control type, which is its natural size.
    /// </summary>
    private ComputedStyle ResolveAutoSizes(ComputedStyle style)
    {
        ComputedStyle resolved = style;
        if (resolved.Width.IsAuto && WidthProperty.GetDefaultValue(this) is WidthValue defaultWidth)
        {
            resolved = resolved with { Width = defaultWidth };
        }

        if (resolved.Height is null && HeightProperty.GetDefaultValue(this) is double defaultHeight)
        {
            resolved = resolved with { Height = defaultHeight };
        }

        return resolved;
    }

    private ComputedStyle ApplyAnimations(ComputedStyle target)
    {
        if (!AnimationClock.IsEnabled)
        {
            _animator?.Cancel();
            _animator = null;
            return target;
        }

        if (_animator is null)
        {
            if (target.Transitions.IsEmpty && target.Animation is null)
            {
                return target;
            }

            _animator = new ElementAnimator(this);
        }

        return _animator.Update(target);
    }

    private void UpdateComputedStyle(ComputedStyle computedStyle, SelectionStyle selectionStyle, Theme theme)
    {
        if (_computedStyle == computedStyle && _selectionStyle == selectionStyle && _computedTheme == theme)
        {
            return;
        }

        _computedStyle = computedStyle;
        _selectionStyle = selectionStyle;
        _computedTheme = theme;
        InvalidateVisual();
    }

    private string FindScopeTypeName()
    {
        UIElement current = this;
        while (current._parent is not null)
        {
            current = current._parent;
        }

        return current.GetType().Name;
    }

    /// <summary>
    /// Keeps a single value for a <see cref="Controls.Style"/> property this type registered with through
    /// <see cref="BindableProperty{TValue}.AddOwner{TOwner}"/>, whichever API sets it, by storing it on <see cref="Style"/>.
    /// </summary>
    protected override BindableObject? GetPropertyStore(BindableProperty property, bool createIfMissing)
    {
        if (property.OwnerType != typeof(Style) || !property.HasOwner(typeof(UIElement)))
        {
            return this;
        }

        return createIfMissing ? Style ??= new Style() : Style;
    }

    private void OnInlineStyleReplaced(Style? oldStyle, Style? newStyle)
    {
        if (oldStyle is not null)
        {
            oldStyle.PropertyChanged -= OnInlineStylePropertyChanged;
        }

        if (newStyle is not null)
        {
            newStyle.PropertyChanged += OnInlineStylePropertyChanged;
        }

        InvalidateStyle();
    }


    private void OnInlineStylePropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
    {
        InvalidateStyle();
        if (e.Property.HasOwner(typeof(UIElement)))
        {
            RaisePropertyChanged(e);
        }
    }

    private void UpdateClassList(StyleClass styleClass)
    {
        _classes = [.. styleClass];
        InvalidateStyle();
    }

    private void OnIsEnabledPropertyChanged(bool isEnabled)
    {
        SetState(ElementStates.Disabled, !isEnabled);
        InvalidateStyle();
        InvalidateVisual();
    }

    /// <summary>
    /// Stores a typed handler delegate with its handled-event delivery option.
    /// </summary>
    private readonly record struct RoutedHandler(Delegate Handler, bool HandledEventsToo);
}
