using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>A circular gradient in vector coordinates.</summary>
public sealed record RadialGradientBrush : GradientBrush
{
    /// <summary>Gets the center.</summary>
    public Point Center { get; init; }
    /// <summary>Gets the radius.</summary>
    public double Radius { get; init; } = 1;
}
