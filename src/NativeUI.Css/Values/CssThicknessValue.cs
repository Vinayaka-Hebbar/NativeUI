using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS box edge thickness value (such as <c>padding</c> or <c>margin</c>), or the four corners of a
/// <c>border-radius</c> with more than one value, from top-left in the positions of top, right, bottom, and left.
/// </summary>
/// <param name="Thickness">The four-edge thickness in logical pixels.</param>
public sealed record CssThicknessValue(Thickness Thickness) : CssValue;
