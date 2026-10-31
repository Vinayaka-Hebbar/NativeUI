using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>font-weight</c> value.
/// </summary>
/// <param name="Weight">The resolved <see cref="FontWeight"/>.</param>
public sealed record CssFontWeightValue(FontWeight Weight) : CssValue;
