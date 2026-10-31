namespace NativeUI.Css;

/// <summary>
/// Represents a unitless numeric CSS value (such as <c>opacity</c> or <c>line-height</c> multiplier).
/// </summary>
/// <param name="Value">The numeric value.</param>
public sealed record CssNumberValue(double Value) : CssValue;
