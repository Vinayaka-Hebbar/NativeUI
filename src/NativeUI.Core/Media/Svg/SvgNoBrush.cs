namespace NativeUI.Media;

/// <summary>Suppresses SVG painting explicitly instead of inheriting a parent brush.</summary>
public sealed record SvgNoBrush : Brush
{
    /// <summary>Gets the shared immutable no-paint value.</summary>
    public static SvgNoBrush Instance { get; } = new();
}
