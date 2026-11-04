namespace NativeUI.Media;

/// <summary>Defines typed SVG viewport alignment and aspect-ratio fitting.</summary>
/// <param name="Alignment">The viewport alignment.</param>
/// <param name="Stretch">Uniform for meet, UniformToFill for slice, or Fill for none.</param>
public readonly record struct SvgAspectRatio(SvgAlignment Alignment, Stretch Stretch = Stretch.Uniform);
