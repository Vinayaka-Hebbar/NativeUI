using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>A straight line between two logical-coordinate endpoints.</summary>
public sealed class Line : Shape
{
    /// <summary>Identifies the X1 coordinate property.</summary>
    public static readonly BindableProperty<double> X1Property = BindableProperty.Create<Line, double>(nameof(X1), propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the X1 endpoint coordinate.</summary>
    public double X1
    {
        get => GetValue(X1Property);
        set => SetValue(X1Property, value);
    }

    /// <summary>Identifies the Y1 coordinate property.</summary>
    public static readonly BindableProperty<double> Y1Property = BindableProperty.Create<Line, double>(nameof(Y1), propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the Y1 endpoint coordinate.</summary>
    public double Y1
    {
        get => GetValue(Y1Property);
        set => SetValue(Y1Property, value);
    }

    /// <summary>Identifies the X2 coordinate property.</summary>
    public static readonly BindableProperty<double> X2Property = BindableProperty.Create<Line, double>(nameof(X2), propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the X2 endpoint coordinate.</summary>
    public double X2
    {
        get => GetValue(X2Property);
        set => SetValue(X2Property, value);
    }

    /// <summary>Identifies the Y2 coordinate property.</summary>
    public static readonly BindableProperty<double> Y2Property = BindableProperty.Create<Line, double>(nameof(Y2), propertyChanged: static (owner, _, _) => owner.InvalidateGeometry());

    /// <summary>Gets or sets the Y2 endpoint coordinate.</summary>
    public double Y2
    {
        get => GetValue(Y2Property);
        set => SetValue(Y2Property, value);
    }

    /// <inheritdoc />
    public override string TagName => "line";

    /// <inheritdoc />
    protected override PathGeometry CreateGeometry(Size size) => new(
    [
        new PathFigure { StartPoint = new Point(X1, Y1), Segments = [new LineSegment(new Point(X2, Y2))] }
    ]);
}
