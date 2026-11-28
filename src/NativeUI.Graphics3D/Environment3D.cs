using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Identifies how <see cref="Environment3D"/> maps high-dynamic-range light to the screen.
/// </summary>
public enum ToneMapper
{
    /// <summary>
    /// Clamps each channel to 1.
    /// </summary>
    Linear,

    /// <summary>
    /// Rolls highlights off along the ACES filmic curve.
    /// </summary>
    Aces
}

/// <summary>
/// Describes how a scene's image is finished: tone mapping, glow around bright areas, light shafts, color adjustments,
/// and a vignette. A GPU renderer then draws the scene in high dynamic range and runs these passes. The polygon renderer
/// applies tone mapping and the color adjustments to each face and skips glow, light shafts, and the vignette. When a
/// scene holds several, the last one applies.
/// </summary>
public class Environment3D : SceneNode
{
    /// <summary>
    /// Identifies the <see cref="TonemapMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ToneMapper> TonemapModeProperty =
        BindableProperty.Create<Environment3D, ToneMapper>(nameof(TonemapMode), defaultValue: ToneMapper.Aces);

    /// <summary>
    /// Identifies the <see cref="TonemapExposure"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> TonemapExposureProperty =
        BindableProperty.Create<Environment3D, double>(nameof(TonemapExposure), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="IsGlowEnabled"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsGlowEnabledProperty =
        BindableProperty.Create<Environment3D, bool>(nameof(IsGlowEnabled), defaultValue: false);

    /// <summary>
    /// Identifies the <see cref="GlowIntensity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> GlowIntensityProperty =
        BindableProperty.Create<Environment3D, double>(nameof(GlowIntensity), defaultValue: 0.8);

    /// <summary>
    /// Identifies the <see cref="GlowStrength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> GlowStrengthProperty =
        BindableProperty.Create<Environment3D, double>(nameof(GlowStrength), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="GlowThreshold"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> GlowThresholdProperty =
        BindableProperty.Create<Environment3D, double>(nameof(GlowThreshold), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="AdjustmentBrightness"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AdjustmentBrightnessProperty =
        BindableProperty.Create<Environment3D, double>(nameof(AdjustmentBrightness), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="AdjustmentContrast"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AdjustmentContrastProperty =
        BindableProperty.Create<Environment3D, double>(nameof(AdjustmentContrast), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="AdjustmentSaturation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> AdjustmentSaturationProperty =
        BindableProperty.Create<Environment3D, double>(nameof(AdjustmentSaturation), defaultValue: 1);

    /// <summary>
    /// Identifies the <see cref="LightShaftIntensity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> LightShaftIntensityProperty =
        BindableProperty.Create<Environment3D, double>(nameof(LightShaftIntensity), defaultValue: 0);

    /// <summary>
    /// Identifies the <see cref="LightShaftDirection"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Vector3> LightShaftDirectionProperty =
        BindableProperty.Create<Environment3D, Vector3>(nameof(LightShaftDirection), defaultValue: Vector3.UnitY);

    /// <summary>
    /// Identifies the <see cref="LightShaftColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> LightShaftColorProperty =
        BindableProperty.Create<Environment3D, Color>(nameof(LightShaftColor), defaultValue: Colors.White);

    /// <summary>
    /// Identifies the <see cref="LightShaftDecay"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> LightShaftDecayProperty =
        BindableProperty.Create<Environment3D, double>(nameof(LightShaftDecay), defaultValue: 0.95);

    /// <summary>
    /// Identifies the <see cref="VignetteIntensity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> VignetteIntensityProperty =
        BindableProperty.Create<Environment3D, double>(nameof(VignetteIntensity), defaultValue: 0);

    /// <summary>
    /// Identifies the <see cref="VignetteSmoothness"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> VignetteSmoothnessProperty =
        BindableProperty.Create<Environment3D, double>(nameof(VignetteSmoothness), defaultValue: 0.5);

    /// <summary>
    /// Identifies the <see cref="VignetteColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Color> VignetteColorProperty =
        BindableProperty.Create<Environment3D, Color>(nameof(VignetteColor), defaultValue: Colors.Black);

    /// <summary>
    /// Gets or sets the curve that maps scene light to screen colors. The default is <see cref="ToneMapper.Aces"/>.
    /// </summary>
    public ToneMapper TonemapMode
    {
        get => GetValue(TonemapModeProperty);
        set => SetValue(TonemapModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to scene light before tone mapping. The default is 1.
    /// </summary>
    public double TonemapExposure
    {
        get => GetValue(TonemapExposureProperty);
        set => SetValue(TonemapExposureProperty, value);
    }

    /// <summary>
    /// Gets or sets whether light brighter than <see cref="GlowThreshold"/> spreads a soft glow around it. The default
    /// is <see langword="false"/>.
    /// </summary>
    public bool IsGlowEnabled
    {
        get => GetValue(IsGlowEnabledProperty);
        set => SetValue(IsGlowEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to the glow added to the image. The default is 0.8.
    /// </summary>
    public double GlowIntensity
    {
        get => GetValue(GlowIntensityProperty);
        set => SetValue(GlowIntensityProperty, value);
    }

    /// <summary>
    /// Gets or sets how far the glow spreads, from 0 for a tight halo to 1 for the widest. The default is 1.
    /// </summary>
    public double GlowStrength
    {
        get => GetValue(GlowStrengthProperty);
        set => SetValue(GlowStrengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the scene light, before exposure, above which pixels glow. The default is 1.
    /// </summary>
    public double GlowThreshold
    {
        get => GetValue(GlowThresholdProperty);
        set => SetValue(GlowThresholdProperty, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to the tone-mapped colors. The default is 1.
    /// </summary>
    public double AdjustmentBrightness
    {
        get => GetValue(AdjustmentBrightnessProperty);
        set => SetValue(AdjustmentBrightnessProperty, value);
    }

    /// <summary>
    /// Gets or sets how far colors move away from middle gray, where 1 keeps them unchanged. The default is 1.
    /// </summary>
    public double AdjustmentContrast
    {
        get => GetValue(AdjustmentContrastProperty);
        set => SetValue(AdjustmentContrastProperty, value);
    }

    /// <summary>
    /// Gets or sets how far colors move away from their gray, where 0 removes color and 1 keeps it unchanged. The
    /// default is 1.
    /// </summary>
    public double AdjustmentSaturation
    {
        get => GetValue(AdjustmentSaturationProperty);
        set => SetValue(AdjustmentSaturationProperty, value);
    }

    /// <summary>
    /// Gets or sets the strength of shafts streaming from bright areas away from the light. 0 disables them. The default is 0.
    /// </summary>
    public double LightShaftIntensity
    {
        get => GetValue(LightShaftIntensityProperty);
        set => SetValue(LightShaftIntensityProperty, value);
    }

    /// <summary>
    /// Gets or sets the direction pointing from the scene toward the light the shafts stream from. Its length does not
    /// matter. The shafts fade out as that direction leaves the view. Defaults to straight up.
    /// </summary>
    public Vector3 LightShaftDirection
    {
        get => GetValue(LightShaftDirectionProperty);
        set => SetValue(LightShaftDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the color the shafts multiply. The default is white.
    /// </summary>
    public Color LightShaftColor
    {
        get => GetValue(LightShaftColorProperty);
        set => SetValue(LightShaftColorProperty, value);
    }

    /// <summary>
    /// Gets or sets how much of the shaft light each step away from the light keeps, from 0 to 1. Higher values give
    /// longer shafts. The default is 0.95.
    /// </summary>
    public double LightShaftDecay
    {
        get => GetValue(LightShaftDecayProperty);
        set => SetValue(LightShaftDecayProperty, value);
    }

    /// <summary>
    /// Gets or sets how strongly the corners darken toward <see cref="VignetteColor"/>, from 0 for none to 1. The default is 0.
    /// </summary>
    public double VignetteIntensity
    {
        get => GetValue(VignetteIntensityProperty);
        set => SetValue(VignetteIntensityProperty, value);
    }

    /// <summary>
    /// Gets or sets how gradually the vignette fades in from the center, from 0 for a hard edge to 1. The default is 0.5.
    /// </summary>
    public double VignetteSmoothness
    {
        get => GetValue(VignetteSmoothnessProperty);
        set => SetValue(VignetteSmoothnessProperty, value);
    }

    /// <summary>
    /// Gets or sets the color the corners darken toward. The default is black.
    /// </summary>
    public Color VignetteColor
    {
        get => GetValue(VignetteColorProperty);
        set => SetValue(VignetteColorProperty, value);
    }
}
