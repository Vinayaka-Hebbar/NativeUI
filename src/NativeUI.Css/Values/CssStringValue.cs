namespace NativeUI.Css;

/// <summary>
/// Represents a string or identifier CSS value (such as <c>font-family</c>).
/// </summary>
/// <param name="Value">The string value.</param>
public sealed record CssStringValue(string Value) : CssValue
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
