using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>white-space</c> value.
/// </summary>
/// <param name="WhiteSpace">The resolved <see cref="Media.WhiteSpace"/>.</param>
public sealed record CssWhiteSpaceValue(WhiteSpace WhiteSpace) : CssValue;
