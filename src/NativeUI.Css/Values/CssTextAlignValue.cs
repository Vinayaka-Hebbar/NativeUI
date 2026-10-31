using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>text-align</c> value.
/// </summary>
/// <param name="Align">The resolved horizontal <see cref="TextAlign"/>.</param>
public sealed record CssTextAlignValue(TextAlign Align) : CssValue;
