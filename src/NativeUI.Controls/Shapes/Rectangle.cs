using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Shapes;

/// <summary>A rectangular vector shape sized by its layout content box.</summary>
public sealed class Rectangle : Shape
{
    /// <inheritdoc />
    public override string TagName => "rectangle";

    /// <inheritdoc />
    protected override PathGeometry CreateGeometry(Size size) => new(
    [
        new PathFigure
        {
            IsClosed = true,
            Segments = [new LineSegment(new Point(size.Width, 0)), new LineSegment(new Point(size.Width, size.Height)), new LineSegment(new Point(0, size.Height))]
        }
    ]);
}
