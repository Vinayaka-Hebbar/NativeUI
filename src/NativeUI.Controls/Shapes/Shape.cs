using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>A styled vector shape whose geometry is independent of its drawing backend.</summary>
public abstract class Shape : UIElement, IIntrinsicSize
{
    private PathGeometry? _geometry;
    private Size _geometrySize;

    /// <summary>Identifies the Fill bindable property.</summary>
    public static readonly BindableProperty<Brush?> FillProperty =
        BindableProperty.Create<Shape, Brush?>(nameof(Fill), defaultValue: Brushes.Black,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the Fill value.</summary>
    public Brush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Identifies the Stroke bindable property.</summary>
    public static readonly BindableProperty<Brush?> StrokeProperty =
        BindableProperty.Create<Shape, Brush?>(nameof(Stroke), defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>Gets or sets the Stroke value.</summary>
    public Brush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Identifies the StrokeThickness bindable property.</summary>
    public static readonly BindableProperty<double> StrokeThicknessProperty =
        BindableProperty.Create<Shape, double>(nameof(StrokeThickness), defaultValue: 1,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>Gets or sets the StrokeThickness value.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Identifies the Stretch bindable property.</summary>
    public static readonly BindableProperty<Stretch> StretchProperty =
        BindableProperty.Create<Shape, Stretch>(nameof(Stretch), defaultValue: Stretch.Uniform,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>Gets or sets the Stretch value.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Resolves shape dimensions from explicit styles and intrinsic geometry.</summary>
    internal Size RenderSize
    {
        get
        {
            Size natural = GetIntrinsicSize();
            double width = natural.Width;
            double height = natural.Height;

            if (ComputedStyle.Width.IsPixels)
            {
                width = ComputedStyle.Width.Value;
            }

            if (ComputedStyle.Height.IsPixels)
            {
                height = ComputedStyle.Height.Value;
            }

            return new Size(width, height);
        }
    }

    /// <summary>Gets immutable geometry, retained until a defining property or size changes.</summary>
    public PathGeometry GetGeometry(Size? size = null)
    {
        Size actual = size ?? new Size(ComputedStyle.Width.IsPixels ? ComputedStyle.Width.Value : 0, ComputedStyle.Height.IsPixels ? ComputedStyle.Height.Value : 0);
        if (_geometry is null || _geometrySize != actual)
        {
            _geometry = CreateGeometry(actual);
            _geometrySize = actual;
        }
        return _geometry;
    }

    /// <inheritdoc />
    public Size GetIntrinsicSize(Size? containingSize = null) => GetGeometryBounds().Size;

    /// <summary>Gets geometry bounds including the stroke extent.</summary>
    public Rect GetGeometryBounds(Size? size = null)
    {
        Rect bounds = GeometryFormatting.Bounds(GetGeometry(size));
        double inset = Stroke is not null ? Math.Max(0, StrokeThickness) / 2 : 0;
        return new Rect(bounds.X - inset, bounds.Y - inset, bounds.Width + 2 * inset, bounds.Height + 2 * inset);
    }

    /// <summary>Invalidates geometry after a defining property changes.</summary>
    protected void InvalidateGeometry()
    {
        _geometry = null;
        InvalidateMeasure();
    }

    /// <summary>Builds immutable geometry for the shape's logical content dimensions.</summary>
    protected abstract PathGeometry CreateGeometry(Size size);

}
