using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>scrollbar-color</c> value. Each color can be a <see cref="CssVariableReference"/>.
/// </summary>
/// <param name="Thumb">The thumb color, or <see langword="null"/> for the <c>auto</c> keyword.</param>
/// <param name="Track">The track color, or <see langword="null"/> for the <c>auto</c> keyword.</param>
public sealed record CssScrollbarColorValue(CssValue? Thumb, CssValue? Track) : CssValue;

/// <summary>
/// Represents a compiled CSS <c>scrollbar-width</c> value.
/// </summary>
/// <param name="Width">The resolved <see cref="ScrollbarWidth"/>.</param>
public sealed record CssScrollbarWidthValue(ScrollbarWidth Width) : CssValue;
