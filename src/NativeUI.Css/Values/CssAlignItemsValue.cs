using NativeUI.Layout;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>align-items</c> value.
/// </summary>
/// <param name="Align">The resolved vertical <see cref="Alignment"/> of the children of a horizontal stack.</param>
public sealed record CssAlignItemsValue(Alignment Align) : CssValue;
