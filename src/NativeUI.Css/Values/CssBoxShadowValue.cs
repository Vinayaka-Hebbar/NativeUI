using System.Collections.Immutable;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>box-shadow</c> value. An empty list is the <c>none</c> keyword.
/// </summary>
/// <param name="Shadows">The shadows from top to bottom.</param>
public sealed record CssBoxShadowValue(ImmutableArray<CssShadow> Shadows) : CssValue;

/// <summary>
/// Represents one compiled shadow of a CSS <c>box-shadow</c> value, whose lengths resolve during style computation.
/// </summary>
/// <param name="OffsetX">The horizontal offset.</param>
/// <param name="OffsetY">The vertical offset.</param>
/// <param name="BlurRadius">The blur radius.</param>
/// <param name="SpreadRadius">The spread radius.</param>
/// <param name="Color">A <see cref="CssColorValue"/> or a <see cref="CssVariableReference"/>, or <see langword="null"/> for the
/// element's text color.</param>
/// <param name="IsInset">Whether the shadow has the <c>inset</c> keyword.</param>
public sealed record CssShadow(
    CssLengthValue OffsetX,
    CssLengthValue OffsetY,
    CssLengthValue BlurRadius,
    CssLengthValue SpreadRadius,
    CssValue? Color,
    bool IsInset);
