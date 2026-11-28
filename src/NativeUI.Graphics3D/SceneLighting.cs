using System.Numerics;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Identifies how a scene's fog thickens with distance. Values are those a GPU scene shader reads from
/// <see cref="SceneFrame.FogParameters"/>.
/// </summary>
internal enum SceneFogMode
{
    None = 0,
    Linear = 1,
    Exponential = 2
}

/// <summary>
/// Converts colors between sRGB and linear light, in which lights and fog combine. Every renderer lights surfaces in
/// linear light and writes sRGB.
/// </summary>
internal static class SceneLighting
{
    /// <summary>
    /// Returns the linear-light channels of <paramref name="color"/>, with its alpha unchanged.
    /// </summary>
    public static Vector4 ToLinear(Color color)
    {
        (byte red, byte green, byte blue, byte alpha) = color.ToSrgbBytes();
        return new Vector4(ToLinear(red / 255f), ToLinear(green / 255f), ToLinear(blue / 255f), alpha / 255f);
    }

    /// <summary>
    /// Returns the sRGB color of linear-light channels, each clamped to [0, 1].
    /// </summary>
    public static Color ToSrgb(Vector3 linear, float alpha)
    {
        return Color.FromSrgb(ToSrgb(linear.X), ToSrgb(linear.Y), ToSrgb(linear.Z), alpha);
    }

    /// <summary>
    /// Returns the sRGB color of linear scene light finished by <paramref name="environment"/>: exposure, tone mapping,
    /// brightness, contrast, and saturation, as the GPU composite pass applies them. Without an environment, each channel
    /// is clamped to 1.
    /// </summary>
    public static Color Finish(Vector3 linear, float alpha, Environment3D? environment)
    {
        if (environment is null)
        {
            return ToSrgb(Vector3.Min(Vector3.One, linear), alpha);
        }

        Vector3 exposed = linear * (float)environment.TonemapExposure;
        Vector3 mapped = environment.TonemapMode == ToneMapper.Aces
            ? new Vector3(ToneMapAces(exposed.X), ToneMapAces(exposed.Y), ToneMapAces(exposed.Z))
            : Vector3.Min(Vector3.One, exposed);
        Vector3 encoded = new Vector3(ToSrgb(mapped.X), ToSrgb(mapped.Y), ToSrgb(mapped.Z)) * (float)environment.AdjustmentBrightness;
        encoded = ((encoded - new Vector3(0.5f)) * (float)environment.AdjustmentContrast) + new Vector3(0.5f);
        float gray = Vector3.Dot(encoded, new Vector3(0.2126f, 0.7152f, 0.0722f));
        encoded = Vector3.Lerp(new Vector3(gray), encoded, (float)environment.AdjustmentSaturation);
        return Color.FromSrgb(Math.Clamp(encoded.X, 0, 1), Math.Clamp(encoded.Y, 0, 1), Math.Clamp(encoded.Z, 0, 1), alpha);
    }

    /// <summary>
    /// Maps scene light to [0, 1] along a rational fit of the ACES filmic curve.
    /// </summary>
    public static float ToneMapAces(float channel)
    {
        channel = Math.Max(channel, 0);
        return Math.Clamp(channel * ((2.51f * channel) + 0.03f) / ((channel * ((2.43f * channel) + 0.59f)) + 0.14f), 0, 1);
    }

    /// <summary>
    /// Returns the light a surface facing <paramref name="normal"/> receives from the scene's hemisphere lights, which
    /// blend from the summed ground colors below to the summed sky colors above.
    /// </summary>
    public static Vector3 GetHemisphereLight(Vector3 normal, Vector3 skyColor, Vector3 groundColor)
    {
        return Vector3.Lerp(groundColor, skyColor, (normal.Y * 0.5f) + 0.5f);
    }

    private static float ToLinear(float channel)
    {
        return channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private static float ToSrgb(float channel)
    {
        channel = Math.Clamp(channel, 0, 1);
        return channel <= 0.0031308f ? channel * 12.92f : (1.055f * MathF.Pow(channel, 1 / 2.4f)) - 0.055f;
    }
}
