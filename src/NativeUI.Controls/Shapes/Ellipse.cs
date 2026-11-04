using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>An elliptical vector shape sized by its layout content box.</summary>
public sealed class Ellipse : Shape
{
    /// <inheritdoc />
    public override string TagName => "ellipse";

    /// <inheritdoc />
    protected override PathGeometry CreateGeometry(Size size)
    {
        Size radii = new(Math.Max(0, size.Width / 2), Math.Max(0, size.Height / 2));
        return new PathGeometry(
        [
            new PathFigure
            {
                StartPoint = new Point(0, radii.Height),
                IsClosed = true,
                Segments =
                [
                    new ArcSegment { Size = radii, Point = new Point(size.Width, radii.Height), SweepDirection = SweepDirection.Clockwise },
                    new ArcSegment { Size = radii, Point = new Point(0, radii.Height), SweepDirection = SweepDirection.Clockwise }
                ]
            }
        ]);
    }
}
