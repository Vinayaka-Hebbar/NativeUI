using NativeUI.Layout;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>position</c> value (<c>static</c>, <c>relative</c>, <c>absolute</c>, or <c>fixed</c>).
/// </summary>
/// <param name="Position">The resolved positioning scheme.</param>
public sealed record CssPositionValue(Position Position) : CssValue;

