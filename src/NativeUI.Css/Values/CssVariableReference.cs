namespace NativeUI.Css;

/// <summary>
/// Represents a CSS <c>var(--name, fallback)</c> custom property reference.
/// </summary>
/// <param name="Name">The custom property name (including the leading <c>--</c> prefix).</param>
/// <param name="Fallback">The optional fallback <see cref="CssValue"/> used when the variable is undefined.</param>
public sealed record CssVariableReference(
    string Name,
    CssValue? Fallback = null
) : CssValue;
