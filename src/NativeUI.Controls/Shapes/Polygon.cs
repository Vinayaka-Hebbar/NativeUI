using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>A closed vector shape defined by immutable points.</summary>
public sealed class Polygon : Shape
{
    /// <summary>Identifies the immutable point collection.</summary>
    public static readonly BindableProperty<ImmutableArray<Point>> PointsProperty = BindableProperty.Create<Polygon, ImmutableArray<Point>>(nameof(Points), defaultValue: [], propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the immutable points in logical coordinates.</summary>
    public ImmutableArray<Point> Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "polygon";

    /// <inheritdoc />
    protected override PathGeometry CreateGeometry(Size size) => Points.IsDefaultOrEmpty ? new PathGeometry([]) : new PathGeometry(
    [
        new PathFigure { StartPoint = Points[0], IsClosed = true, Segments = [.. Points.Skip(1).Select(static point => (PathSegment)new LineSegment(point))] }
    ]);
}
