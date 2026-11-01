using NativeUI.Media;

namespace NativeUI.Graphics;

/// <summary>
/// Represents a color at a position along a gradient.
/// </summary>
/// <param name="Offset">The position along the gradient, in the range [0, 1].</param>
/// <param name="Color">The color at <paramref name="Offset"/>.</param>
public readonly record struct GradientStop(double Offset, Color Color);
