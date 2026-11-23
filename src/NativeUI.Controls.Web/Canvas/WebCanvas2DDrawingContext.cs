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
    private JSObject? _context;
    private readonly double _width;
    private readonly double _height;

    /// <summary>
    /// Initializes a drawing pass, acquiring the native 2D context once.
    /// </summary>
    /// <param name="canvas">The borrowed native canvas, or null without a browser surface.</param>
    /// <param name="width">The logical drawing width.</param>
    /// <param name="height">The logical drawing height.</param>
    public WebCanvas2DDrawingContext(JSObject? canvas, double width, double height)
    {
        _context = canvas is not null && OperatingSystem.IsBrowser()
            ? CanvasInterop.CreateContext2D(canvas)
            : null;
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
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Begin(_context, _width, _height);
        }
    }

    /// <summary>
    /// Finalizes the drawing pass and restores initial context state.
    /// </summary>
    public void End()
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.End(_context);
        }
    }

    /// <summary>
    /// Releases the retained browser context handle and drawing resources.
    /// </summary>
    public override void Dispose()
    {
        _context?.Dispose();
        _context = null;
        base.Dispose();
    }

    /// <inheritdoc />
    public override void Save()
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Save(_context);
        }
    }

    /// <inheritdoc />
    public override void SaveLayer(Rect bounds, double opacity)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Save(_context);
        }
    }

    /// <inheritdoc />
    public override void Restore()
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Restore(_context);
        }
    }

    /// <inheritdoc />
    public override void Translate(double dx, double dy)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Translate(_context, dx, dy);
        }
    }

    /// <inheritdoc />
    public override void Scale(float scaleX, float scaleY)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Scale(_context, scaleX, scaleY);
        }
    }

    /// <inheritdoc />
    public override void Clear(Color color)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.Clear(_context, color.ToCssString());
        }
    }

    /// <inheritdoc />
    public override void ClipRect(Rect rect)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.ClipRect(_context, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    /// <inheritdoc />
    public override void ClipRoundedRectangle(Rect rect, double radius)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.ClipRoundRect(_context, rect.X, rect.Y, rect.Width, rect.Height, radius);
        }
    }

    /// <inheritdoc />
    public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            foreach (Rect rect in region)
            {
                CanvasInterop.ClipRect(_context, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }
    }

    /// <inheritdoc />
    public override void DrawRectangle(Rect rect, Paint paint)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawRect(_context, rect.X, rect.Y, rect.Width, rect.Height, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawRoundRect(_context, rect.X, rect.Y, rect.Width, rect.Height, radius, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (_context is null || !OperatingSystem.IsBrowser())
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

        CanvasInterop.FillLinearGradient(_context, rect.X, rect.Y, rect.Width, rect.Height, start.X, start.Y, end.X, end.Y, offsets, colors);
    }

    /// <inheritdoc />
    public override void FillPolygon(IReadOnlyList<Point> points, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (_context is null || !OperatingSystem.IsBrowser() || points.Count < 3)
        {
            return;
        }

        double[] coordinates = new double[points.Count * 2];
        for (int i = 0; i < points.Count; i++)
        {
            coordinates[i * 2] = points[i].X;
            coordinates[(i * 2) + 1] = points[i].Y;
        }

        CanvasInterop.FillPolygon(_context, coordinates, paint.Color.ToCssString(), paint.Opacity);
    }

    /// <inheritdoc />
    public override void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint)
    {
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.StrokeRoundRect(_context, rect.X, rect.Y, rect.Width, rect.Height, radius, strokeWidth, paint.Color.ToCssString(), paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (_context is not null && OperatingSystem.IsBrowser())
        {
            CanvasInterop.DrawText(_context, layout.Text, origin.X, origin.Y, paint.Color.ToCssString(), paint.Opacity);
        }
    }
}
