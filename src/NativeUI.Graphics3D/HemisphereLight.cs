using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Lights every surface from all directions with <see cref="Color"/> from above and <see cref="GroundColor"/> from below,
/// blended by how far the surface faces up. Use it for light scattered by the sky, by water, or by the ground.
/// </summary>
public class HemisphereLight : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<HemisphereLight, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="GroundColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> GroundColorProperty =
        BindableProperty.Create<HemisphereLight, Color>(nameof(GroundColor), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="Intensity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> IntensityProperty =
        BindableProperty.Create<HemisphereLight, double>(nameof(Intensity), defaultValue: 1);

    /// <summary>
    /// Gets or sets the light reaching surfaces that face straight up. The default is white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the light reaching surfaces that face straight down. The default is white.
    /// </summary>
    public Color GroundColor
    {
        get => GetValue(GroundColorProperty);
        set => SetValue(GroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to both colors. The default is 1.
    /// </summary>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }
}
