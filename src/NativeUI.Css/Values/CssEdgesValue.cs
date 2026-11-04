namespace NativeUI.Css;

/// <summary>
/// Represents a compiled <c>padding</c> or <c>margin</c> value whose edges are resolved during style computation, such
/// as <c>0 var(--spacing)</c> or <c>1em 2em</c>. Each edge is a <see cref="CssLengthValue"/> or a
/// <see cref="CssVariableReference"/>. Edges in logical pixels compile to a <see cref="CssThicknessValue"/> instead.
/// </summary>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Left">The left edge.</param>
public sealed record CssEdgesValue(CssValue Top, CssValue Right, CssValue Bottom, CssValue Left) : CssValue;
