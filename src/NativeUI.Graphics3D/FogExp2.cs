using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Fades surfaces toward <see cref="Color"/> from the camera outward, thickening with the square of the distance as haze
/// in water or dense air does. The sky is not fogged. When a scene holds several fogs, the last one applies.
/// </summary>
public class FogExp2 : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<FogExp2, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="Density"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> DensityProperty =
        BindableProperty.Create<FogExp2, double>(nameof(Density), defaultValue: 0.00025);

    /// <summary>
    /// Identifies the <see cref="AerialPerspective"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AerialPerspectiveProperty =
        BindableProperty.Create<FogExp2, double>(nameof(AerialPerspective), defaultValue: 0);

    /// <summary>
    /// Gets or sets the color that distant surfaces fade to. The default is white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets how quickly the fog thickens. A surface at distance d fades by 1 - e^-(Density × d)². The default is
    /// 0.00025.
    /// </summary>
    public double Density
    {
        get => GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    /// <summary>
    /// Gets or sets how much the fog takes the color of the scene's <see cref="GradientSky"/> in the direction of each
    /// surface, from 0 for <see cref="Color"/> alone to 1 for the sky color alone. Distant surfaces then dissolve into the
    /// sky behind them. Without a sky, the fog uses <see cref="Color"/>. The default is 0.
    /// </summary>
    public double AerialPerspective
    {
        get => GetValue(AerialPerspectiveProperty);
        set => SetValue(AerialPerspectiveProperty, value);
    }

    /// <summary>
    /// Returns how far a surface at <paramref name="distance"/> has faded toward <see cref="Color"/>, from 0 to 1.
    /// </summary>
    internal static float GetAmount(float distance, float density)
    {
        float thickness = density * distance;
        return 1 - MathF.Exp(-(thickness * thickness));
    }
}
