using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Fades surfaces toward <see cref="Color"/> with their distance from the camera, linearly from <see cref="Near"/> to
/// <see cref="Far"/>. The sky is not fogged. When a scene holds several fogs, the last one applies.
/// </summary>
public class Fog : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Create<Fog, Color>(nameof(Color), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="Near"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> NearProperty =
        BindableProperty.Create<Fog, double>(nameof(Near), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="Far"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> FarProperty =
        BindableProperty.Create<Fog, double>(nameof(Far), defaultValue: 1000);

    /// <summary>
    /// Gets or sets the color that distant surfaces fade to. The default is white.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the distance from the camera where fog starts. The default is 1.
    /// </summary>
    public double Near
    {
        get => GetValue(NearProperty);
        set => SetValue(NearProperty, value);
    }

    /// <summary>
    /// Gets or sets the distance from the camera where surfaces become <see cref="Color"/>. The default is 1000.
    /// </summary>
    public double Far
    {
        get => GetValue(FarProperty);
        set => SetValue(FarProperty, value);
    }

    /// <summary>
    /// Returns how far a surface at <paramref name="distance"/> has faded toward <see cref="Color"/>, from 0 to 1.
    /// </summary>
    internal static float GetAmount(float distance, float near, float far)
    {
        return far <= near ? (distance >= far ? 1 : 0) : Math.Clamp((distance - near) / (far - near), 0, 1);
    }
}
