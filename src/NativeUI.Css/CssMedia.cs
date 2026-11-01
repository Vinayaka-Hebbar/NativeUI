namespace NativeUI.Css;

/// <summary>
/// Represents a single CSS media feature condition.
/// </summary>
public readonly record struct CssMedia
{
    /// <summary>
    /// Gets an empty media query condition that applies unconditionally.
    /// </summary>
    public static CssMedia None => default;

    /// <summary>
    /// Gets the condition matching <c>@media (prefers-reduced-motion: reduce)</c>.
    /// </summary>
    public static CssMedia ReducedMotion => new("prefers-reduced-motion", "reduce");

    /// <summary>
    /// Gets the condition matching <c>@media (prefers-color-scheme: light)</c>.
    /// </summary>
    public static CssMedia Light => new("prefers-color-scheme", "light");

    /// <summary>
    /// Gets the condition matching <c>@media (prefers-color-scheme: dark)</c>.
    /// </summary>
    public static CssMedia Dark => new("prefers-color-scheme", "dark");

    /// <summary>
    /// Gets the media feature name (e.g. <c>"prefers-reduced-motion"</c>), or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? Feature { get; }

    /// <summary>
    /// Gets the required media feature value (e.g. <c>"reduce"</c>), or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssMedia"/> struct.
    /// </summary>
    /// <param name="feature">The media feature name.</param>
    /// <param name="value">The required media feature value.</param>
    public CssMedia(string feature, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Feature = feature.Trim().ToLowerInvariant();
        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Formats this media query condition as CSS text (e.g. <c>"@media (prefers-reduced-motion: reduce)"</c>).
    /// </summary>
    public string ToCssString()
    {
        return Feature is null ? string.Empty : $"@media ({Feature}: {Value})";
    }
}
