using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>A vector shape backed by immutable geometry.</summary>
public sealed class Path : Shape
{
    /// <summary>Identifies the Data bindable property.</summary>
    public static readonly BindableProperty<Geometry?> DataProperty =
        BindableProperty.Create<Path, Geometry?>(nameof(Data), defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the Data value.</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "path";

    /// <inheritdoc />
    protected override PathGeometry CreateGeometry(Size size) => Data switch
    {
        PathGeometry geometry => geometry,
        null => new PathGeometry([]),
        _ => throw new NotSupportedException("Unsupported geometry type.")
    };
}
