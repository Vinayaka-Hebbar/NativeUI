using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>
/// Implements <see cref="DrawingContext"/> targeting an HTML <c>&lt;canvas&gt;</c> 2D rendering context.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class WebCanvas2DDrawingContext : DrawingContext
{
    private readonly JSObject? _canvas;
    private readonly double _width;
    private readonly double _height;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCanvas2DDrawingContext"/> class.
    /// </summary>
    public WebCanvas2DDrawingContext(JSObject? canvas, double width, double height)
    {
        _canvas = canvas;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Gets the logical drawing surface width.
    /// </summary>
    public double Width => _width;

    /// <summary>
    /// Gets the logical drawing surface height.
    /// </summary>
    public double Height => _height;

    /// <summary>
    /// Prepares the canvas surface for a new drawing pass.
    /// </summary>
    public void Begin()
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Begin(_canvas, _width, _height);
        }
    }

    /// <summary>
    /// Finalizes the drawing pass and restores initial context state.
    /// </summary>
    public void End()
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.End(_canvas);
        }
    }

    /// <inheritdoc />
    public override void Save()
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Save(_canvas);
        }
    }

    /// <inheritdoc />
    public override void SaveLayer(Rect bounds, double opacity)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Save(_canvas);
        }
    }

    /// <inheritdoc />
    public override void Restore()
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Restore(_canvas);
        }
    }

    /// <inheritdoc />
    public override void Translate(double dx, double dy)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Translate(_canvas, dx, dy);
        }
    }

    /// <inheritdoc />
    public override void Scale(float scaleX, float scaleY)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Scale(_canvas, scaleX, scaleY);
        }
    }

    /// <inheritdoc />
    public override void Clear(Color color)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Clear(_canvas, color.ToCssString());
        }
    }

    /// <inheritdoc />
    public override void ClipRect(Rect rect)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.ClipRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    /// <inheritdoc />
    public override void ClipRoundedRectangle(Rect rect, double radius)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.ClipRoundRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height, radius);
        }
    }

    /// <inheritdoc />
    public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            foreach (Rect rect in region)
            {
                CanvasInterop.ClipRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }
    }

    /// <inheritdoc />
    public override void DrawRectangle(Rect rect, Paint paint)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawRoundRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height, radius, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (_canvas is null || !OperatingSystem.IsBrowser())
        {
            return;
        }

        double[] offsets = new double[stops.Count];
        string[] colors = new string[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            offsets[i] = stops[i].Offset;
            colors[i] = stops[i].Color.ToCssString();
        }

        CanvasInterop.FillLinearGradient(_canvas, rect.X, rect.Y, rect.Width, rect.Height, start.X, start.Y, end.X, end.Y, offsets, colors);
    }

    /// <inheritdoc />
    public override void FillPolygon(IReadOnlyList<Point> points, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (_canvas is null || !OperatingSystem.IsBrowser() || points.Count < 3)
        {
            return;
        }

        double[] coordinates = new double[points.Count * 2];
        for (int i = 0; i < points.Count; i++)
        {
            coordinates[i * 2] = points[i].X;
            coordinates[(i * 2) + 1] = points[i].Y;
        }

        CanvasInterop.FillPolygon(_canvas, coordinates, paint.Color.ToCssString(), paint.Opacity);
    }

    /// <inheritdoc />
    public override void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint)
    {
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.StrokeRoundRect(_canvas, rect.X, rect.Y, rect.Width, rect.Height, radius, strokeWidth, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (_canvas is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawText(_canvas, layout.Text, origin.X, origin.Y, paint.Color.ToCssString(), paint.Opacity);
        }
    }
}
