using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Base class for all visual elements in NativeUI, participating in visual-tree <see cref="Core.Theme"/>
/// and <see cref="IStyleResolver"/> inheritance, inline C# <see cref="Controls.Style"/>, desktop layout/rendering,
/// and Web DOM synchronization.
/// </summary>
public abstract class UIElement : BindableObject, ILayoutNode, IUIElement
{
    private IReadOnlyList<string> _classes = [];
    private ComputedStyle _computedStyle = ComputedStyle.Default;
    private SelectionStyle _selectionStyle = SelectionStyle.Default;
    private Theme _computedTheme = Theme.Light;
    private UIElement? _parent;
    private Theme _inheritedTheme = Theme.Light;
    private IStyleResolver? _styleResolver;
    private Dictionary<RoutedEvent, RoutedHandler[]>? _routedHandlers;

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
    /// Identifies the <see cref="Theme"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Theme> ThemeProperty =
        BindableProperty.Create<UIElement, Theme>(
            nameof(Theme),
            defaultValue: Theme.Default,
            propertyChanged: static (owner, _, _) => owner.UpdateInheritedProperties());

    /// <summary>
    /// Occurs whenever a visual property or computed style on this element changes.
    /// </summary>
    public event EventHandler? VisualInvalidated;

    /// <summary>
    /// Occurs after <see cref="ActualTheme"/> changes and this element's style reflects the new theme.
    /// </summary>
    public event EventHandler? ActualThemeChanged;

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
    public bool IsVisible
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
    /// Gets or sets the theme explicitly requested on this element (<see cref="Theme.Default"/> inherits from parent/host).
    /// </summary>
    public Theme Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Gets the effective <see cref="Core.Theme"/> (<see cref="Theme.Light"/> or <see cref="Theme.Dark"/>)
    /// resolved from <see cref="Theme"/> or inherited from the parent/host.
    /// </summary>
    public Theme ActualTheme => Theme != Theme.Default ? Theme : _inheritedTheme;

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

    /// <inheritdoc />
    public ElementStates States { get; private set; }

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
    /// <see cref="StyleResolver"/>, <see cref="ActualTheme"/>, and inline <see cref="Style"/> overrides.
    /// </summary>
    public virtual void RecomputeStyle()
    {
        ComputedStyle computedStyle = ResolveComputedStyle();
        SelectionStyle selectionStyle = ResolveSelectionStyle();
        UpdateComputedStyle(computedStyle, selectionStyle, ActualTheme);
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
        UpdateInheritedProperties();
    }

    /// <summary>
    /// Sets the inherited <paramref name="theme"/> from a platform host when this element is the root of a window.
    /// </summary>
    protected internal void SetInheritedTheme(Theme theme)
    {
        _inheritedTheme = theme != Theme.Default ? theme : Theme.Light;
        UpdateInheritedProperties();
    }

    /// <summary>
    /// Signals that this element's visual appearance or layout requires an incremental update.
    /// </summary>
    protected internal void InvalidateVisual()
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
    /// Gets the control's default style, whose sizing values apply where no stylesheet specifies one.
    /// Stylesheets and inline styles take precedence.
    /// </summary>
    protected virtual IStyle? DefaultStyle => null;

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
        ComputedStyle computed = resolver is not null
            ? resolver.ResolveStyle(this)
            : ComputedStyle.CreateInherited(_parent?.ComputedStyle);

        computed = ViewStyleResolver.Resolve(this, computed, resolver);

        return computed.ApplySizingDefaults(DefaultStyle).ApplyInline(Style, resolver, this);
    }

    private void UpdateComputedStyle(ComputedStyle computedStyle, SelectionStyle selectionStyle, Theme theme)
    {
        if (_computedStyle == computedStyle && _selectionStyle == selectionStyle && _computedTheme == theme)
        {
            return;
        }

        bool themeChanged = _computedTheme != theme;
        _computedStyle = computedStyle;
        _selectionStyle = selectionStyle;
        _computedTheme = theme;
        InvalidateVisual();
        if (themeChanged)
        {
            ActualThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Updates properties inherited from ancestor elements (such as effective theme) and invalidates styles.
    /// </summary>
    protected internal virtual void UpdateInheritedProperties()
    {
        if (_parent is not null)
        {
            _inheritedTheme = _parent.ActualTheme;
        }

        InvalidateStyle();
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

    private void OnInlineStyleReplaced(Style? oldStyle, Style? newStyle)
    {
        if (oldStyle is not null)
        {
            oldStyle.Changed -= OnInlineStylePropertyChanged;
        }

        if (newStyle is not null)
        {
            newStyle.Changed += OnInlineStylePropertyChanged;
        }

        InvalidateStyle();
    }

    private void OnInlineStylePropertyChanged(object? sender, EventArgs e)
    {
        InvalidateStyle();
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
