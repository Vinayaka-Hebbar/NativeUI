using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

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
    private Theme _computedTheme = Theme.Light;
    private UIElement? _parent;
    private Theme _inheritedTheme = Theme.Light;
    private IStyleResolver? _styleResolver;

    /// <summary>
    /// Identifies the <see cref="Id"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> IdProperty =
        BindableProperty.Create<UIElement, string?>(nameof(Id), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="Class"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ClassProperty =
        BindableProperty.Create<UIElement, string>(
            nameof(Class),
            defaultValue: string.Empty,
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
    /// Gets or sets the unique element identifier.
    /// </summary>
    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>
    /// Gets or sets the space-separated list of style class names applied to this element.
    /// </summary>
    public string Class
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
    public Theme ActualTheme
    {
        get
        {
            Theme requested = Theme;
            if (requested != Theme.Default)
            {
                return requested;
            }

            return _parent?.ActualTheme ?? _inheritedTheme;
        }
    }

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
    /// Gets or sets the platform-owned native handle or browser DOM <c>JSObject</c> associated with this element.
    /// </summary>
    public object? NativePeer { get; set; }

    /// <summary>
    /// Gets the canonical NativeUI tag name used by CSS type selectors, such as <c>view</c> or <c>text</c>.
    /// </summary>
    public abstract string TagName { get; }

    /// <inheritdoc />
    public ComputedStyle ComputedStyle => _computedStyle;

    /// <inheritdoc />
    public string StyleSheetId => FindScopeTypeName();

    IUIElement? IUIElement.Parent => Parent;

    IStyle? IUIElement.Style => Style;

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
    /// Recomputes the element's <see cref="ComputedStyle"/> using its effective <see cref="StyleResolver"/>,
    /// <see cref="ActualTheme"/>, and inline <see cref="Style"/> overrides.
    /// </summary>
    public virtual void RecomputeStyle()
    {
        ComputedStyle computedStyle = ResolveComputedStyle();
        UpdateComputedStyle(computedStyle, ActualTheme);
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
    /// Returns the child layout nodes for container controls.
    /// </summary>
    protected virtual IReadOnlyList<ILayoutNode> GetLayoutChildren()
    {
        return [];
    }

    private ComputedStyle ResolveComputedStyle()
    {
        IStyleResolver? resolver = StyleResolver;
        if (resolver is null)
        {
            ComputedStyle inheritedStyle = ComputedStyle.CreateInherited(_parent?.ComputedStyle);
            return inheritedStyle.ApplyInline(Style);
        }

        return resolver.ResolveStyle(this);
    }

    private void UpdateComputedStyle(ComputedStyle computedStyle, Theme theme)
    {
        if (_computedStyle == computedStyle && _computedTheme == theme)
        {
            return;
        }

        _computedStyle = computedStyle;
        _computedTheme = theme;
        InvalidateVisual();
    }

    private void UpdateInheritedProperties()
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

    private void UpdateClassList(string rawClass)
    {
        _classes = string.IsNullOrWhiteSpace(rawClass)
            ? []
            : rawClass.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        InvalidateStyle();
    }
}
