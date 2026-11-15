using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Supplies formatting and bindings for content within a Text control without creating a layout element.
/// </summary>
public abstract class Inline : BindableObject, IUIElement
{
    /// <summary>Identifies the inline style property.</summary>
    public static readonly BindableProperty<Style?> StyleProperty =
        BindableProperty.Create<Inline, Style?>(nameof(Style),
            propertyChanged: static (owner, previous, current) => owner.OnStyleChanged(previous, current));

    /// <summary>Identifies the CSS classes applied to this inline.</summary>
    public static readonly BindableProperty<StyleClass> ClassProperty =
        BindableProperty.Create<Inline, StyleClass>(nameof(Class), StyleClass.Empty);

    /// <summary>Initializes property-change propagation to the owning collection.</summary>
    protected Inline()
    {
        PropertyChanged += OnPropertyChanged;
    }

    /// <summary>Gets or sets the local text formatting.</summary>
    public Style? Style
    {
        get => GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    /// <summary>Gets or sets the CSS classes used to format this inline.</summary>
    public StyleClass Class
    {
        get => GetValue(ClassProperty);
        set => SetValue(ClassProperty, value);
    }

    internal InlineCollection? ParentCollection { get; set; }
    internal IUIElement? Parent => ParentCollection?.Owner as IUIElement;
    internal Text? TextOwner => ParentCollection?.Owner switch
    {
        Text text => text,
        Inline inline => inline.TextOwner,
        _ => null
    };

    string IUIElement.TagName => GetType().Name.ToLowerInvariant();
    IReadOnlyList<string> IUIElement.Classes => Class.ToArray();
    string? IUIElement.StyleSheetId => Parent?.StyleSheetId;
    IUIElement? IUIElement.Parent => Parent;
    string? IUIElement.PartName => null;
    IUIElement? IUIElement.TemplatedParent => null;
    string? IUIElement.GetAttribute(string name) => null;
    IStyle? IUIElement.Style => Style;
    Cursor? IUIElement.Cursor => Style?.Cursor;
    WidthValue? IUIElement.Width => null;
    double? IUIElement.Height => null;
    Overflow? IUIElement.OverflowX => null;
    Overflow? IUIElement.OverflowY => null;
    Position? IUIElement.Position => null;
    double? IUIElement.Top => null;
    double? IUIElement.Right => null;
    double? IUIElement.Bottom => null;
    double? IUIElement.Left => null;
    SelectionStyle IUIElement.SelectionStyle => Parent?.SelectionStyle ?? SelectionStyle.Default;
    ElementStates IUIElement.States => ElementStates.None;
    double? IUIElement.FontSize => Style?.FontSize;
    ComputedStyle IUIElement.ComputedStyle => ComputedStyle;

    internal ComputedStyle ComputedStyle
    {
        get
        {
            IStyleResolver? resolver = TextOwner?.StyleResolver;
            ComputedStyle initial = resolver?.ResolveStyle(this) ?? ComputedStyle.CreateInitial(this);
            return ViewStyleResolver.Resolve(this, initial, resolver).ApplyInline(Style, resolver, this);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ParentCollection?.Remove(this);
            PropertyChanged -= OnPropertyChanged;
            if (Style is Style style)
            {
                style.PropertyChanged -= OnStylePropertyChanged;
            }
        }

        base.Dispose(disposing);
    }

    private void OnPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        ParentCollection?.NotifyChanged();
    }

    private void OnStylePropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        ParentCollection?.NotifyChanged();
    }

    private void OnStyleChanged(Style? previous, Style? current)
    {
        if (previous is not null)
        {
            previous.PropertyChanged -= OnStylePropertyChanged;
        }

        if (current is not null)
        {
            current.PropertyChanged += OnStylePropertyChanged;
        }
    }
}
