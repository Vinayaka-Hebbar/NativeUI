using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Fills the background of a scene with a sky above the horizon and ground below it, each blended by viewing elevation.
/// </summary>
public class GradientSky : SceneNode
{
    private const double MinimumCurve = 0.001;

    /// <summary>
    /// Identifies the <see cref="TopColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> TopColorProperty =
        BindableProperty.Create<GradientSky, Color>(nameof(TopColor), defaultValue: Color.FromHex("#2F80ED"));

    /// <summary>
    /// Identifies the <see cref="HorizonColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> HorizonColorProperty =
        BindableProperty.Create<GradientSky, Color>(nameof(HorizonColor), defaultValue: Color.FromHex("#DCEEFF"));

    /// <summary>
    /// Identifies the <see cref="GroundColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> GroundColorProperty =
        BindableProperty.Create<GradientSky, Color>(nameof(GroundColor), defaultValue: Color.FromHex("#30343B"));

    /// <summary>
    /// Identifies the <see cref="GroundHorizonColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> GroundHorizonColorProperty =
        BindableProperty.Create<GradientSky, Color>(nameof(GroundHorizonColor), defaultValue: Color.FromHex("#8A9099"));

    /// <summary>
    /// Identifies the <see cref="SkyCurve"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SkyCurveProperty =
        BindableProperty.Create<GradientSky, double>(nameof(SkyCurve), defaultValue: 0.15);

    /// <summary>
    /// Identifies the <see cref="GroundCurve"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> GroundCurveProperty =
        BindableProperty.Create<GradientSky, double>(nameof(GroundCurve), defaultValue: 0.1);

    /// <summary>
    /// Identifies the <see cref="EnergyMultiplier"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> EnergyMultiplierProperty =
        BindableProperty.Create<GradientSky, double>(nameof(EnergyMultiplier), defaultValue: 1);

    /// <summary>
    /// Gets or sets the color directly overhead.
    /// </summary>
    public Color TopColor
    {
        get => GetValue(TopColorProperty);
        set => SetValue(TopColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the sky at the horizon.
    /// </summary>
    public Color HorizonColor
    {
        get => GetValue(HorizonColorProperty);
        set => SetValue(HorizonColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color directly below.
    /// </summary>
    public Color GroundColor
    {
        get => GetValue(GroundColorProperty);
        set => SetValue(GroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the ground at the horizon.
    /// </summary>
    public Color GroundHorizonColor
    {
        get => GetValue(GroundHorizonColorProperty);
        set => SetValue(GroundHorizonColorProperty, value);
    }

    /// <summary>
    /// Gets or sets how quickly the sky changes from <see cref="HorizonColor"/> to <see cref="TopColor"/>. Smaller values
    /// reach <see cref="TopColor"/> closer to the horizon.
    /// </summary>
    public double SkyCurve
    {
        get => GetValue(SkyCurveProperty);
        set => SetValue(SkyCurveProperty, value);
    }

    /// <summary>
    /// Gets or sets how quickly the ground changes from <see cref="GroundHorizonColor"/> to <see cref="GroundColor"/>. Smaller
    /// values reach <see cref="GroundColor"/> closer to the horizon.
    /// </summary>
    public double GroundCurve
    {
        get => GetValue(GroundCurveProperty);
        set => SetValue(GroundCurveProperty, value);
    }

    /// <summary>
    /// Gets or sets the brightness factor applied to the sky and ground colors.
    /// </summary>
    public double EnergyMultiplier
    {
        get => GetValue(EnergyMultiplierProperty);
        set => SetValue(EnergyMultiplierProperty, value);
    }

    /// <summary>
    /// Returns the color seen along the unit vector <paramref name="direction"/>.
    /// </summary>
    internal Color GetColor(Vector3 direction)
    {
        double angleFromTop = Math.Acos(Math.Clamp(direction.Y, -1f, 1f));
        return angleFromTop <= Math.PI / 2
            ? Blend(HorizonColor, TopColor, 1 - (angleFromTop / (Math.PI / 2)), SkyCurve)
            : Blend(GroundHorizonColor, GroundColor, (angleFromTop / (Math.PI / 2)) - 1, GroundCurve);
    }

    private Color Blend(Color from, Color to, double elevation, double curve)
    {
        double amount = Math.Clamp(1 - Math.Pow(1 - elevation, 1 / Math.Max(curve, MinimumCurve)), 0, 1);
        (byte fromRed, byte fromGreen, byte fromBlue, _) = from.ToSrgbBytes();
        (byte toRed, byte toGreen, byte toBlue, _) = to.ToSrgbBytes();
        return Color.FromSrgb(
            BlendChannel(fromRed, toRed, amount),
            BlendChannel(fromGreen, toGreen, amount),
            BlendChannel(fromBlue, toBlue, amount));
    }

    private double BlendChannel(byte from, byte to, double amount)
    {
        return Math.Min(1, (from + ((to - from) * amount)) / 255.0 * EnergyMultiplier);
    }
}
