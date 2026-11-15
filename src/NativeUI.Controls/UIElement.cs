using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Media.Animation;
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
    private Control? _templatedParent;
    private string? _partName;
    private INativeHandle? _nativeHandle;
    private Rect _renderedBounds;
    private bool _isEffectivelyEnabled = true;
    private bool _isUpdatingEffectiveEnabledState;
    private IStyleResolver? _styleResolver;
    private RoutedEventHandlerStore? _routedHandlers;
    private StyleAnimator? _animator;
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
    /// Identifies the <see cref="SizeChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<SizeChangedEventArgs> SizeChangedEvent =
        RoutedEvent.Register<UIElement, SizeChangedEventArgs>(nameof(SizeChanged), RoutingStrategy.Direct);

    /// <summary>
    /// Identifies the <see cref="GotFocus"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> GotFocusEvent =
        RoutedEvent.Register<UIElement, RoutedEventArgs>(nameof(GotFocus), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="LostFocus"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> LostFocusEvent =
        RoutedEvent.Register<UIElement, RoutedEventArgs>(nameof(LostFocus), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="KeyDown"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<KeyEventArgs> KeyDownEvent =
        RoutedEvent.Register<UIElement, KeyEventArgs>(nameof(KeyDown), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="KeyUp"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<KeyEventArgs> KeyUpEvent =
        RoutedEvent.Register<UIElement, KeyEventArgs>(nameof(KeyUp), RoutingStrategy.Bubble);

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
            propertyChanged: static (owner, _, _) => owner.OnIsVisibleChanged());

    /// <summary>
    /// Identifies the <see cref="IsEnabled"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsEnabledProperty =
        BindableProperty.Create<UIElement, bool>(
            nameof(IsEnabled),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.UpdateEffectiveEnabledState());

    /// <summary>
    /// Identifies the effective enabled state after control-specific restrictions are applied.
    /// </summary>
    public static readonly ComputedBindableProperty<UIElement, bool> IsEffectivelyEnabledProperty =
        BindableProperty.CreateComputed<UIElement, bool>(nameof(IsEffectivelyEnabled), static element => element.IsEffectivelyEnabled);

    /// <summary>
    /// Identifies the <see cref="Focusable"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> FocusableProperty =
        BindableProperty.Create<UIElement, bool>(
            nameof(Focusable),
            defaultValue: false,
            propertyChanged: static (owner, _, newValue) => owner.OnFocusablePropertyChanged(newValue));

    /// <summary>
    /// Identifies the <see cref="TabIndex"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> TabIndexProperty =
        BindableProperty.Create<UIElement, int>(nameof(TabIndex), defaultValue: 0);

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
    /// Identifies the inline cursor of an element, which is <see cref="Style.CursorProperty"/>. A control type overrides
    /// its default to set the cursor that the element and its descendants show while no stylesheet sets one.
    /// </summary>
    public static readonly BindableProperty<Cursor?> CursorProperty = Style.CursorProperty.AddOwner<UIElement>();

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
    /// Identifies the <see cref="Bounds"/> computed bindable property.
    /// </summary>
    public static readonly ComputedBindableProperty<UIElement, Rect> BoundsProperty =
        BindableProperty.CreateComputed<UIElement, Rect>(
            nameof(Bounds),
            static element => element.ComputeBounds(),
            observe: static element => element.NativeHandle?.CreateSizeObserver(element));

    /// <summary>
    /// Identifies the <see cref="ActualSize"/> computed bindable property.
    /// </summary>
    public static readonly ComputedBindableProperty<UIElement, Size> ActualSizeProperty =
        BindableProperty.CreateComputed<UIElement, Size>(
            nameof(ActualSize),
            static element => element.Bounds.Size,
            observe: static element => BoundsProperty.Subscribe(element));

    /// <summary>
    /// Identifies the <see cref="ActualWidth"/> computed bindable property.
    /// </summary>
    public static readonly ComputedBindableProperty<UIElement, double> ActualWidthProperty =
        BindableProperty.CreateComputed<UIElement, double>(
            nameof(ActualWidth),
            static element => element.Bounds.Width,
            observe: static element => BoundsProperty.Subscribe(element));

    /// <summary>
    /// Identifies the <see cref="ActualHeight"/> computed bindable property.
    /// </summary>
    public static readonly ComputedBindableProperty<UIElement, double> ActualHeightProperty =
        BindableProperty.CreateComputed<UIElement, double>(
            nameof(ActualHeight),
            static element => element.Bounds.Height,
            observe: static element => BoundsProperty.Subscribe(element));

    static UIElement()
    {
        EventManager.RegisterEventObserver<UIElement>(
            SizeChangedEvent,
            static element => element.CreateSizeObserver());
    }

    /// <summary>
    /// Occurs whenever a visual property or computed style on this element changes.
    /// </summary>
    public event EventHandler? VisualInvalidated;

    /// <summary>
    /// Occurs when this element's parent changes, allowing internal observers to rebind inherited tree state.
    /// </summary>
    internal event EventHandler? ParentChanged;

    /// <summary>
    /// Occurs when this element's own visibility changes, including visibility composed by a derived control.
    /// </summary>
    internal event EventHandler? VisibilityChanged;

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
    /// Gets the rendered bounds of this element in logical coordinates.
    /// </summary>
    public Rect Bounds => GetValue(BoundsProperty);

    /// <summary>
    /// Gets the rendered size of this element.
    /// </summary>
    public Size ActualSize => Bounds.Size;

    /// <summary>
    /// Gets the rendered width of this element.
    /// </summary>
    public double ActualWidth => Bounds.Width;

    /// <summary>
    /// Gets the rendered height of this element.
    /// </summary>
    public double ActualHeight => Bounds.Height;

    /// <summary>
    /// Occurs when the rendered size of this element changes.
    /// </summary>
    public event EventHandler<SizeChangedEventArgs> SizeChanged
    {
        add => AddHandler(SizeChangedEvent, value);
        remove => RemoveHandler(SizeChangedEvent, value);
    }

    /// <summary>
    /// Occurs when this element or one of its descendants receives keyboard focus. The source is the element that received focus.
    /// </summary>
    public event EventHandler<RoutedEventArgs> GotFocus
    {
        add => AddHandler(GotFocusEvent, value);
        remove => RemoveHandler(GotFocusEvent, value);
    }

    /// <summary>
    /// Occurs when this element or one of its descendants loses keyboard focus. The source is the element that lost focus.
    /// </summary>
    public event EventHandler<RoutedEventArgs> LostFocus
    {
        add => AddHandler(LostFocusEvent, value);
        remove => RemoveHandler(LostFocusEvent, value);
    }

    /// <summary>
    /// Occurs when a key is pressed while this element or one of its descendants has keyboard focus. While no element has
    /// focus, the root element receives the event.
    /// </summary>
    public event EventHandler<KeyEventArgs> KeyDown
    {
        add => AddHandler(KeyDownEvent, value);
        remove => RemoveHandler(KeyDownEvent, value);
    }

    /// <summary>
    /// Occurs when a key is released while this element or one of its descendants has keyboard focus. While no element
    /// has focus, the root element receives the event.
    /// </summary>
    public event EventHandler<KeyEventArgs> KeyUp
    {
        add => AddHandler(KeyUpEvent, value);
        remove => RemoveHandler(KeyUpEvent, value);
    }

    /// <summary>
    /// Gets a value indicating whether the rendered size or bounds of this element is currently observed.
    /// </summary>
    public bool IsSizeObserved => BoundsProperty.HasSubscribers(this) || HasHandler(SizeChangedEvent);

    internal Rect ComputeBounds()
    {
        if (_nativeHandle is not null)
        {
            Rect? bounds = _nativeHandle.GetBounds();
            if (bounds.HasValue)
            {
                return bounds.Value;
            }
        }

        return _renderedBounds;
    }

    /// <summary>
    /// Updates the element's rendered bounds and dispatches notifications for changed geometry properties.
    /// </summary>
    /// <param name="newBounds">The new rendered bounds.</param>
    internal void UpdateRenderedBounds(Rect newBounds)
    {
        Rect previousBounds = _renderedBounds;
        if (previousBounds == newBounds)
        {
            return;
        }

        _renderedBounds = newBounds;

        NotifyPropertyChanged(BoundsProperty, previousBounds, newBounds);

        if (previousBounds.Size != newBounds.Size)
        {
            NotifyPropertyChanged(ActualSizeProperty, previousBounds.Size, newBounds.Size);

            if (previousBounds.Width != newBounds.Width)
            {
                NotifyPropertyChanged(ActualWidthProperty, previousBounds.Width, newBounds.Width);
            }

            if (previousBounds.Height != newBounds.Height)
            {
                NotifyPropertyChanged(ActualHeightProperty, previousBounds.Height, newBounds.Height);
            }

            RaiseEvent(SizeChangedEvent, new SizeChangedEventArgs(previousBounds.Size, newBounds.Size));
        }
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
    /// Gets whether this element, its ancestors, and its control-specific conditions permit interaction.
    /// </summary>
    public bool IsEffectivelyEnabled => IsEnabled && IsEnabledCore && (Parent?.IsEnabledForChild(this) ?? true);

    /// <summary>Gets whether this element permits inherited interaction for a child.</summary>
    protected internal virtual bool IsEnabledForChild(UIElement child) => IsEffectivelyEnabled;

    /// <inheritdoc />
    protected override void OnBindingValidationChanged() => Validation.Refresh(this);

    /// <summary>
    /// Gets whether the element participates in pointer hit testing while it is drawn.
    /// </summary>
    internal virtual bool IsHitTestVisible => IsVisible;

    /// <summary>
    /// Gets whether this element and every ancestor remain visible to pointer input.
    /// </summary>
    internal bool IsHitTestVisibleInTree
    {
        get
        {
            for (UIElement? current = this; current is not null; current = current.Parent)
            {
                if (!current.IsHitTestVisible)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Returns the nearest effectively enabled element at or above a hit that remains visible to input.
    /// </summary>
    internal UIElement? GetInputTarget()
    {
        if (!IsHitTestVisibleInTree)
        {
            return null;
        }

        for (UIElement? current = this; current is not null; current = current.Parent)
        {
            if (current.IsEffectivelyEnabled)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets whether control-specific conditions permit interaction independently of the local enabled value.
    /// </summary>
    /// <remarks>Overrides must call <see cref="UpdateEffectiveEnabledState"/> whenever their dependencies change.</remarks>
    protected virtual bool IsEnabledCore => true;

    /// <summary>
    /// Updates interaction states without changing the local enabled value or its binding.
    /// </summary>
    protected internal void UpdateEffectiveEnabledState()
    {
        if (_isUpdatingEffectiveEnabledState)
        {
            return;
        }

        _isUpdatingEffectiveEnabledState = true;
        try
        {
            while (_isEffectivelyEnabled != IsEffectivelyEnabled)
            {
                bool isEnabled;
                do
                {
                    isEnabled = IsEffectivelyEnabled;
                    OnEffectiveEnabledChanged(isEnabled);
                }
                while (isEnabled != IsEffectivelyEnabled);

                if (_isEffectivelyEnabled == isEnabled)
                {
                    continue;
                }

                bool previous = _isEffectivelyEnabled;
                _isEffectivelyEnabled = isEnabled;
                NotifyPropertyChanged(IsEffectivelyEnabledProperty, previous, isEnabled);
            }
        }
        finally
        {
            _isUpdatingEffectiveEnabledState = false;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this element can receive keyboard focus. A control type overrides the
    /// default through <see cref="FocusableProperty"/> metadata. A disabled or hidden element cannot receive focus.
    /// </summary>
    public bool Focusable
    {
        get => GetValue(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    /// <summary>
    /// Gets or sets the position of this element in the keyboard navigation order. Elements with a positive value come
    /// first in ascending order, followed by elements with zero in tree order. An element with a negative value receives
    /// focus only from <see cref="Focus()"/> or a pointer press.
    /// </summary>
    public int TabIndex
    {
        get => GetValue(TabIndexProperty);
        set => SetValue(TabIndexProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether this element has keyboard focus.
    /// </summary>
    public bool IsFocused => States.HasFlag(ElementStates.Focus);

    /// <summary>
    /// Gets how this element received keyboard focus, or <see cref="FocusState.Unfocused"/> while it does not have focus.
    /// </summary>
    public FocusState FocusState { get; internal set; }

    /// <summary>
    /// Moves keyboard focus to this element as code does. Focus moves only while the element is <see cref="Focusable"/>,
    /// enabled, visible, and presented by a platform host.
    /// </summary>
    /// <returns><see langword="true"/> when this element has focus after the call.</returns>
    public bool Focus()
    {
        return Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Moves keyboard focus to this element as <paramref name="state"/> describes. Focus moves only while the element is
    /// <see cref="Focusable"/>, enabled, visible, and presented by a platform host.
    /// </summary>
    /// <param name="state">How focus moves to the element, which decides whether the focus is visible.
    /// <see cref="FocusState.Unfocused"/> is not valid.</param>
    /// <returns><see langword="true"/> when this element has focus after the call.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is <see cref="FocusState.Unfocused"/>.</exception>
    public bool Focus(FocusState state)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(state, FocusState.Unfocused);
        return InputManager.Current.PrimaryKeyboardDevice.Focus(this, state);
    }

    /// <summary>
    /// Moves keyboard focus from this element in <paramref name="direction"/> as code does.
    /// <see cref="FocusNavigationDirection.Next"/> and <see cref="FocusNavigationDirection.Previous"/> move to the element
    /// after or before this one in the navigation order. <see cref="FocusNavigationDirection.First"/> and
    /// <see cref="FocusNavigationDirection.Last"/> move to the first or last element inside this one.
    /// </summary>
    /// <param name="direction">The direction to move focus.</param>
    /// <returns><see langword="true"/> when an element received focus.</returns>
    public bool MoveFocus(FocusNavigationDirection direction)
    {
        return InputManager.Current.PrimaryKeyboardDevice.MoveFocus(this, direction, FocusState.Programmatic);
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

            UIElement? previousParent = _parent;
            _parent = value;
            if (VisualRoot is UIElement previousRoot && !ReferenceEquals(previousRoot, this))
            {
                DetachFromVisualTree(previousRoot, previousParent);
            }

            // Lifecycle callbacks may have moved this element again.
            if (!ReferenceEquals(_parent, value))
            {
                return;
            }

            if (!ReferenceEquals(VisualRoot, this) && value?.VisualRoot is UIElement root)
            {
                AttachToVisualTree(root);
            }

            if (!ReferenceEquals(_parent, value))
            {
                return;
            }

            OnParentChanged();
            if (!ReferenceEquals(_parent, value))
            {
                return;
            }

            OnAncestorChanged();
        }
    }

    /// <summary>
    /// Determines whether this element is an ancestor of <paramref name="descendant"/>, including itself.
    /// </summary>
    /// <param name="descendant">The element whose parent chain is examined.</param>
    /// <returns><see langword="true"/> when this element is the descendant or one of its ancestors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is <see langword="null"/>.</exception>
    public bool IsAncestorOf(UIElement descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        for (UIElement? current = descendant; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the value used by attribute selectors, or <see langword="null"/> when the attribute is absent.
    /// Template owners can supply attributes for their parts; controls can override this method for their own attributes.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public virtual string? GetAttribute(string name)
    {
        return _templatedParent?.GetTemplateChildAttribute(this, name);
    }

    /// <summary>
    /// Gets the name that <c>::part()</c> selectors use for this element within its <see cref="TemplatedParent"/>'s
    /// template, or <see langword="null"/> when the element is not a template part.
    /// </summary>
    public string? PartName => _partName;

    /// <summary>
    /// Gets the control whose template created this element, or <see langword="null"/> when the element is not a template part.
    /// </summary>
    public Control? TemplatedParent => _templatedParent;

    /// <summary>
    /// Records that <paramref name="owner"/>'s template created this element as the part named <paramref name="name"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The element is already a template part.</exception>
    internal void SetTemplatePart(Control owner, string name)
    {
        if (_templatedParent is not null)
        {
            throw new InvalidOperationException("The element is already a part of a control template.");
        }

        _templatedParent = owner;
        _partName = name;
        InvalidateStyle();
    }

    /// <summary>
    /// Records that this element is no longer a part of its templated parent's template.
    /// </summary>
    internal void ClearTemplatePart()
    {
        if (_templatedParent is not null)
        {
            _templatedParent = null;
            _partName = null;
            InvalidateStyle();
        }
    }

    /// <summary>
    /// Gets the platform-owned native handle or browser DOM proxy associated with this element.
    /// </summary>
    public INativeHandle? NativeHandle
    {
        get => _nativeHandle;
        internal set
        {
            if (ReferenceEquals(_nativeHandle, value))
            {
                return;
            }

            _nativeHandle?.OnDetachView(this);
            _nativeHandle = value;
            _nativeHandle?.OnAttachView(this);
        }
    }

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

    IUIElement? IUIElement.TemplatedParent => TemplatedParent;

    IStyle? IUIElement.Style => Style;

    WidthValue? IUIElement.Width => Width;

    double? IUIElement.Height => Height;

    Overflow? IUIElement.OverflowX => OverflowX;

    Overflow? IUIElement.OverflowY => OverflowY;

    Position? IUIElement.Position => Position;

    Cursor? IUIElement.Cursor => GetValue(CursorProperty);

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
        if (NativeHandle is IAnimationPeer peer)
        {
            peer.Animate(spec, animation);
        }
        else if (AnimationClock.IsEnabled && StyleResolver?.ResolveKeyframes(name, this) is { } frames)
        {
            _animator ??= new StyleAnimator(this);
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
    /// Shows a style that <see cref="StyleAnimator"/> produced. Descendants recompute only when an inherited value changed.
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
    internal virtual void MarkRendered()
    {
        HasRendered = true;
    }

    /// <summary>
    /// Gets a value indicating whether this element accepts text input. Focus on such an element is visible after a
    /// pointer press too.
    /// </summary>
    internal virtual bool AcceptsTextInput => false;

    /// <summary>
    /// Gets a value indicating whether this element has a running transition or an animation with a finite iteration count.
    /// </summary>
    internal bool HasRunningAnimations => _animator?.HasRunningAnimations(AnimationClock.Now) ?? false;

    /// <summary>
    /// Sets or clears <paramref name="state"/> without recomputing styles, so a host can update a complete path first.
    /// </summary>
    internal void SetState(ElementStates state, bool enabled)
    {
        States = enabled ? States | state : States & ~state;
    }

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

        _routedHandlers ??= new RoutedEventHandlerStore(this);
        _routedHandlers.Add(routedEvent, handler, handledEventsToo);
    }

    /// <summary>
    /// Removes the most recently added registration of <paramref name="handler"/> for <paramref name="routedEvent"/>.
    /// </summary>
    public void RemoveHandler<TArgs>(RoutedEvent<TArgs> routedEvent, EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);

        _routedHandlers?.Remove(routedEvent, handler);
    }

    /// <summary>
    /// Gets a value indicating whether any handlers are registered for the specified routed event on this element.
    /// </summary>
    /// <param name="routedEvent">The routed event to check.</param>
    /// <returns><see langword="true"/> if at least one handler is registered; otherwise, <see langword="false"/>.</returns>
    public bool HasHandler(RoutedEvent routedEvent)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        return _routedHandlers?.HasHandler(routedEvent) ?? false;
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
    /// Notifies observers and invalidates the visual after this element's <see cref="IsVisible"/> value changes.
    /// Derived controls whose visibility depends on additional state call this method when that state changes.
    /// </summary>
    protected virtual void OnIsVisibleChanged()
    {
        if (!IsVisible && States.HasFlag(ElementStates.FocusWithin))
        {
            InputManager.Current.PrimaryKeyboardDevice.ClearFocus(this);
        }

        OnVisibilityChanged();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>
    /// Gets the platform-hosted root that owns this element's visual attachment.
    /// </summary>
    internal UIElement? VisualRoot { get; private set; }

    /// <summary>
    /// Gets whether this element belongs to a platform-hosted visual tree.
    /// </summary>
    internal bool IsAttachedToVisualTree => VisualRoot is not null;

    /// <summary>
    /// Attaches this element to the specified hosted visual root.
    /// </summary>
    internal virtual void AttachToVisualTree(UIElement root)
    {
        if (ReferenceEquals(VisualRoot, root))
        {
            return;
        }

        if (VisualRoot is UIElement previousRoot)
        {
            DetachFromVisualTree(previousRoot, Parent);
            if (VisualRoot is not null)
            {
                return;
            }
        }

        VisualRoot = root;
        OnAttachedToVisualTree();
    }

    /// <summary>
    /// Detaches this element if it belongs to the specified hosted visual root.
    /// </summary>
    /// <param name="root">The visual root being left.</param>
    /// <param name="parent">The parent before removal, used to clear logical focus in the old scope.</param>
    internal virtual void DetachFromVisualTree(UIElement root, UIElement? parent)
    {
        if (!ReferenceEquals(VisualRoot, root))
        {
            return;
        }

        UIElement? previousParent = Parent;
        FocusManager.ClearFocus(this, parent);
        if (!ReferenceEquals(VisualRoot, root) || !ReferenceEquals(Parent, previousParent))
        {
            return;
        }

        VisualRoot = null;
        InputManager.Current.PrimaryKeyboardDevice.ClearFocus(this);
        if (VisualRoot is null)
        {
            OnDetachedFromVisualTree();
        }
    }

    /// <summary>
    /// Invoked when this element becomes attached to a platform-hosted visual tree.
    /// </summary>
    protected virtual void OnAttachedToVisualTree()
    {
    }

    /// <summary>
    /// Invoked when this element leaves its platform-hosted visual tree.
    /// </summary>
    protected virtual void OnDetachedFromVisualTree()
    {
    }

    /// <summary>
    /// Notifies tree-dependent controls after the ancestor chain changes.
    /// </summary>
    internal virtual void OnAncestorChanged()
    {
    }

    /// <summary>
    /// Notifies controls whose eligibility depends on this element's visibility.
    /// </summary>
    internal virtual void OnVisibilityChanged()
    {
    }

    /// <summary>
    /// Invoked when the <see cref="Parent"/> of this element changes.
    /// </summary>
    protected virtual void OnParentChanged()
    {
        SetInheritedDataContext(_parent?.DataContext);
        UpdateEffectiveEnabledState();
        if (_parent is null)
        {
            _animator?.Cancel();
        }

        ParentChanged?.Invoke(this, EventArgs.Empty);
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
            FocusManager.ClearFocus(this, Parent);
            if (States.HasFlag(ElementStates.FocusWithin))
            {
                InputManager.Current.PrimaryKeyboardDevice.ClearFocus(this);
            }

            _animator?.Cancel();
            _animator = null;
            _nativeHandle?.Dispose();
            _nativeHandle = null;
            _routedHandlers?.Dispose();
            _routedHandlers = null;
        }

        base.Dispose(disposing);
    }

    private IDisposable? CreateSizeObserver()
    {
        return BoundsProperty.Subscribe(this);
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
        return _routedHandlers?.GetHandlers(routedEvent) ?? [];
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
        return cascadedStyle.ApplyInline(Style, resolver, this);
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

            _animator = new StyleAnimator(this);
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

    private void OnFocusablePropertyChanged(bool isFocusable)
    {
        if (!isFocusable && IsFocused)
        {
            InputManager.Current.PrimaryKeyboardDevice.ClearFocus(this);
        }
    }

    /// <summary>
    /// Updates focus and interaction states when the effective enabled state changes.
    /// </summary>
    /// <param name="isEnabled">The effective enabled state.</param>
    protected virtual void OnEffectiveEnabledChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;
            if (States.HasFlag(ElementStates.FocusWithin) && keyboard.FocusedElement is { IsEffectivelyEnabled: false })
            {
                keyboard.ClearFocus(this);
            }

            SetState(ElementStates.Pressed, false);
        }

        SetState(ElementStates.Disabled, !isEnabled);
        InvalidateStyle();
        InvalidateVisual();
    }
}
