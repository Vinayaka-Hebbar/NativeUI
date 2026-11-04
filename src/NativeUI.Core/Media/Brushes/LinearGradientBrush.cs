using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>A gradient along a line in vector coordinates.</summary>
public sealed record LinearGradientBrush : GradientBrush
{
    /// <summary>Gets the start point.</summary>
    public Point Start { get; init; }
    /// <summary>Gets the endpoint.</summary>
    public Point End { get; init; } = new(1, 0);
}
