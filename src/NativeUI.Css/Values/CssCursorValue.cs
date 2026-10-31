using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>cursor</c> value.
/// </summary>
/// <param name="Cursor">The resolved <see cref="Cursor"/> appearance.</param>
public sealed record CssCursorValue(Cursor Cursor) : CssValue;
