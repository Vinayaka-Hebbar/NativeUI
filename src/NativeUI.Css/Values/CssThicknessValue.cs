using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS box edge thickness value (such as <c>padding</c> or <c>margin</c>).
/// </summary>
/// <param name="Thickness">The four-edge thickness in logical pixels.</param>
public sealed record CssThicknessValue(Thickness Thickness) : CssValue;
