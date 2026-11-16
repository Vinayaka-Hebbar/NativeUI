using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>text-overflow</c> value.
/// </summary>
/// <param name="Overflow">The resolved <see cref="TextOverflow"/>.</param>
public sealed record CssTextOverflowValue(TextOverflow Overflow) : CssValue;
