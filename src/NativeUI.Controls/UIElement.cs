using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Base class for all visual elements in NativeUI, participating in inline C# <see cref="Controls.Style"/>,
/// optional <see cref="StyleResolver"/> styling, desktop layout/rendering, and Web DOM synchronization.
/// </summary>
public abstract class UIElement : BindableObject, ILayoutNode
{
    private IReadOnlyList<string> _classes = [];
    private ComputedStyle _computedStyle = ComputedStyle.Default;
    private UIElement? _parent;

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
    /// Gets the parent <see cref="UIElement"/> in the visual tree, or <see langword="null"/> if this is a root element.
    /// </summary>
    public UIElement? Parent
    {
        get => _parent;
        internal set
        {
            _parent = value;
            SetInheritedDataContext(value?.DataContext);
            RecomputeStyle();
        }
    }

    /// <summary>
    /// Gets or sets the platform-owned native handle or browser DOM <c>JSObject</c> associated with this element.
    /// </summary>
    public object? NativePeer { get; set; }

    /// <summary>
    /// Gets the canonical type name used by optional style resolvers (for example, <c>"view"</c> or <c>"text"</c>).
    /// </summary>
    public abstract string StyleTypeName { get; }

    /// <inheritdoc />
    public ComputedStyle ComputedStyle => _computedStyle;

    /// <inheritdoc />
    string? ILayoutNode.TextContent => GetLayoutTextContent();

    /// <inheritdoc />
    IReadOnlyList<ILayoutNode> ILayoutNode.LayoutChildren => GetLayoutChildren();

    /// <summary>
    /// Recomputes the element's <see cref="ComputedStyle"/> from any registered <see cref="StyleResolver.Current"/>
    /// and inline <see cref="Style"/> properties.
    /// </summary>
    public virtual void RecomputeStyle()
    {
        string scopeName = FindScopeTypeName();
        IStyleResolver? resolver = StyleResolver.Current;

        ComputedStyle baseStyle = resolver is not null
            ? resolver.ResolveBaseStyle(StyleTypeName, _classes, scopeName)
            : ComputedStyle.Default;

        Style? inline = Style;
        if (inline is null)
        {
            _computedStyle = baseStyle;
            InvalidateVisual();
            return;
        }

        Color? resolvedTextColor = baseStyle.TextColor;
        if (inline.Color is ThemeValue<Color> fg)
        {
            resolvedTextColor = resolver is not null
                ? resolver.ResolveColor(fg, scopeName)
                : (fg.HasValue ? fg.Value : resolvedTextColor);
        }

        Color? resolvedBgColor = baseStyle.BackgroundColor;
        if (inline.BackgroundColor is ThemeValue<Color> bg)
        {
            resolvedBgColor = resolver is not null
                ? resolver.ResolveColor(bg, scopeName)
                : (bg.HasValue ? bg.Value : resolvedBgColor);
        }

        _computedStyle = new ComputedStyle
        {
            TextColor = resolvedTextColor,
            BackgroundColor = resolvedBgColor,
            FontSize = inline.FontSize ?? baseStyle.FontSize,
            FontWeight = inline.FontWeight ?? baseStyle.FontWeight,
            FontFamily = inline.FontFamily ?? baseStyle.FontFamily,
            TextAlign = inline.TextAlign ?? baseStyle.TextAlign,
            Padding = inline.Padding ?? baseStyle.Padding,
            Margin = inline.Margin ?? baseStyle.Margin,
            BorderRadius = inline.BorderRadius ?? baseStyle.BorderRadius,
            Opacity = inline.Opacity is double op ? Math.Clamp(op, 0.0, 1.0) : baseStyle.Opacity,
            Width = inline.Width ?? baseStyle.Width,
            Height = inline.Height ?? baseStyle.Height
        };

        InvalidateVisual();
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

        RecomputeStyle();
    }

    private void OnInlineStylePropertyChanged(object? sender, EventArgs e)
    {
        RecomputeStyle();
    }

    private void UpdateClassList(string rawClass)
    {
        _classes = string.IsNullOrWhiteSpace(rawClass)
            ? []
            : rawClass.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        RecomputeStyle();
    }
}
