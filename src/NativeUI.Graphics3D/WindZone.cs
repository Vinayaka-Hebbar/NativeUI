using System.Numerics;
using NativeUI.Core;

namespace NativeUI.Graphics3D;

/// <summary>
/// Sways the surfaces of materials with a nonzero <see cref="StandardMaterial.WindResponse"/> along
/// <see cref="Direction"/>. The sway advances with <see cref="Time"/>, which an application sets each frame, and varies
/// with each instance's position. When a scene holds several, the last one applies.
/// </summary>
public class WindZone : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="Direction"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> DirectionProperty =
        BindableProperty.Create<WindZone, Vector3>(nameof(Direction), defaultValue: Vector3.UnitX);

    /// <summary>
    /// Identifies the <see cref="Strength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> StrengthProperty =
        BindableProperty.Create<WindZone, double>(nameof(Strength), defaultValue: 0.1);

    /// <summary>
    /// Identifies the <see cref="Turbulence"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> TurbulenceProperty =
        BindableProperty.Create<WindZone, double>(nameof(Turbulence), defaultValue: 0.3);

    /// <summary>
    /// Identifies the <see cref="Frequency"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> FrequencyProperty =
        BindableProperty.Create<WindZone, double>(nameof(Frequency), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="Time"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TimeSpan> TimeProperty =
        BindableProperty.Create<WindZone, TimeSpan>(nameof(Time), defaultValue: TimeSpan.Zero);

    /// <summary>
    /// Gets or sets the horizontal direction the wind blows toward. Its length and vertical part do not matter. Defaults to +X.
    /// </summary>
    public Vector3 Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets how far the top of a surface one unit tall bends, relative to its height. The default is 0.1.
    /// </summary>
    public double Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the share of the sway that flutters at a higher frequency. The default is 0.3.
    /// </summary>
    public double Turbulence
    {
        get => GetValue(TurbulenceProperty);
        set => SetValue(TurbulenceProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of sways per second. The default is 1.
    /// </summary>
    public double Frequency
    {
        get => GetValue(FrequencyProperty);
        set => SetValue(FrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the moment of the sway's animation. The default is zero.
    /// </summary>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }
}
