using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Lights surfaces that face up with a moving net of bright lines, as sunlight focused by waves lights a seabed.
/// The pattern advances with <see cref="Time"/>, which an application sets each frame, for example from
/// <c>Viewport3D.Rendering</c>. When a scene holds several, the last one applies.
/// </summary>
public class Caustics : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<Caustics, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="Intensity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> IntensityProperty =
        BindableProperty.Create<Caustics, double>(nameof(Intensity), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="Scale"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ScaleProperty =
        BindableProperty.Create<Caustics, double>(nameof(Scale), defaultValue: 4);

    /// <summary>
    /// Identifies the <see cref="Time"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TimeSpan> TimeProperty =
        BindableProperty.Create<Caustics, TimeSpan>(nameof(Time), defaultValue: TimeSpan.Zero);

    /// <summary>
    /// Gets or sets the color of the light. The default is white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to <see cref="Color"/>. The default is 1.
    /// </summary>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of one cell of the pattern in world units. The default is 4.
    /// </summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the moment of the pattern's animation. The default is zero.
    /// </summary>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>
    /// Returns the pattern's brightness from 0 to about 1.6 at a horizontal position, matching the GPU scene shader.
    /// Two layers of bent sine waves cancel along thin curves, which become the bright lines.
    /// </summary>
    /// <param name="x">The world X position divided by <see cref="Scale"/>.</param>
    /// <param name="z">The world Z position divided by <see cref="Scale"/>.</param>
    /// <param name="seconds">The <see cref="Time"/> in seconds.</param>
    internal static float GetPattern(float x, float z, float seconds)
    {
        return GetLayer(x, z, seconds) + (0.6f * GetLayer((x * 1.7f) + 3.1f, (z * 1.7f) + 1.3f, seconds * 1.3f));
    }

    private static float GetLayer(float x, float z, float seconds)
    {
        float bentX = x + (0.6f * MathF.Sin((z * 1.3f) + seconds));
        float bentZ = z + (0.6f * MathF.Cos((x * 1.1f) - (seconds * 0.8f)));
        float waves = MathF.Sin((bentX * 2.1f) + (seconds * 0.7f))
            + MathF.Sin((bentZ * 2.3f) - (seconds * 0.9f))
            + MathF.Sin(((bentX + bentZ) * 1.7f) + (seconds * 0.5f));
        return MathF.Pow(Math.Clamp(1 - (MathF.Abs(waves) / 1.5f), 0, 1), 6);
    }
}
