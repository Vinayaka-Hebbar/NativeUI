using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>An SVG viewport owning vector visuals and sharing an immutable document definition.</summary>
public class Svg : UIElement, IIntrinsicSize
{
    /// <summary>Identifies the ViewBox bindable property.</summary>
    public static readonly BindableProperty<Rect> ViewBoxProperty =
        BindableProperty.Create<Svg, Rect>(nameof(ViewBox), defaultValue: new Rect(0, 0, 300, 150),
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the ViewBox value.</summary>
    public Rect ViewBox
    {
        get => GetValue(ViewBoxProperty);
        set => SetValue(ViewBoxProperty, value);
    }

    /// <summary>Identifies the Definition bindable property.</summary>
    public static readonly BindableProperty<SvgNode?> DefinitionProperty =
        BindableProperty.Create<Svg, SvgNode?>(nameof(Definition), defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the Definition value.</summary>
    public SvgNode? Definition
    {
        get => GetValue(DefinitionProperty);
        set => SetValue(DefinitionProperty, value);
    }

    /// <summary>Identifies the Stretch bindable property.</summary>
    public static readonly BindableProperty<Stretch> StretchProperty =
        BindableProperty.Create<Svg, Stretch>(nameof(Stretch), defaultValue: Stretch.Uniform,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the Stretch value.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "svg";

    /// <inheritdoc />
    public Size GetIntrinsicSize(Size? containingSize = null) => Definition?.GetIntrinsicSize(containingSize) ?? ViewBox.Size;

    /// <summary>Gets the vector coordinate viewport.</summary>
    public Rect GetViewBox(Size? containingSize = null) => Definition?.GetViewBox(containingSize) ?? ViewBox;

}
