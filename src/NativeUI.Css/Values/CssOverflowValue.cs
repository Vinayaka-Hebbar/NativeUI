using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>overflow</c>, <c>overflow-x</c>, or <c>overflow-y</c> value.
/// </summary>
/// <param name="Overflow">The resolved <see cref="Overflow"/> behavior.</param>
public sealed record CssOverflowValue(Overflow Overflow) : CssValue;
