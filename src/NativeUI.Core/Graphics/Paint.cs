using NativeUI.Media;

namespace NativeUI.Graphics;

/// <summary>
/// Describes the painting attributes (such as color and opacity) used to fill or render graphical primitives and text layouts.
/// </summary>
public readonly record struct Paint
{
    /// <summary>
    /// Gets the paint fill or stroke color.
    /// </summary>
    public Color Color { get; init; }

    /// <summary>
    /// Gets the paint opacity in the range [0.0, 1.0]. Defaults to 1.0.
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Paint"/> struct.
    /// </summary>
    public Paint()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Paint"/> struct with the specified <paramref name="color"/> and <paramref name="opacity"/>.
    /// </summary>
    /// <param name="color">The fill or stroke color.</param>
    /// <param name="opacity">The opacity in the range [0.0, 1.0].</param>
    public Paint(Color color, double opacity = 1.0)
    {
        Color = color;
        Opacity = opacity;
    }
}
