using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>Shared image and vector fitting calculations in logical coordinates.</summary>
public static class StretchLayout
{
    /// <summary>Fits source dimensions inside a destination, centered using the requested stretch mode.</summary>
    public static Rect Fit(Size source, Rect destination, Stretch stretch)
    {
        if (source.Width <= 0 || source.Height <= 0 || destination.Width <= 0 || destination.Height <= 0) return Rect.Empty;
        double sx = destination.Width / source.Width, sy = destination.Height / source.Height;
        if (stretch == Stretch.None) sx = sy = 1;
        else if (stretch == Stretch.Uniform) sx = sy = Math.Min(sx, sy);
        else if (stretch == Stretch.UniformToFill) sx = sy = Math.Max(sx, sy);
        double width = source.Width * sx, height = source.Height * sy;
        return new Rect(destination.X + (destination.Width - width) / 2, destination.Y + (destination.Height - height) / 2, width, height);
    }
}
