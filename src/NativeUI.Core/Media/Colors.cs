namespace NativeUI.Media;

/// <summary>
/// Provides predefined <see cref="Color"/> instances for common colors.
/// </summary>
public static class Colors
{
    /// <summary>
    /// Gets a completely transparent color.
    /// </summary>
    public static Color Transparent => new(ColorSpace.Srgb, 0.0, 0.0, 0.0, 0.0);

    /// <summary>
    /// Gets the color black (#000000).
    /// </summary>
    public static Color Black => new(ColorSpace.Srgb, 0.0, 0.0, 0.0, 1.0);

    /// <summary>
    /// Gets the color white (#FFFFFF).
    /// </summary>
    public static Color White => new(ColorSpace.Srgb, 1.0, 1.0, 1.0, 1.0);

    /// <summary>
    /// Gets a primary blue color (#2563EB).
    /// </summary>
    public static Color Blue => Color.FromRgbBytes(37, 99, 235);

    /// <summary>
    /// Gets an indigo color (#4F46E5).
    /// </summary>
    public static Color Indigo => Color.FromRgbBytes(79, 70, 229);

    /// <summary>
    /// Gets a green color (#16A34A).
    /// </summary>
    public static Color Green => Color.FromRgbBytes(22, 163, 74);

    /// <summary>
    /// Gets a red color (#DC2626).
    /// </summary>
    public static Color Red => Color.FromRgbBytes(220, 38, 38);
}
