namespace NativeUI.Media;

/// <summary>
/// Represents the resolved, concrete style values for a UI element after applying any active
/// <see cref="IStyleResolver"/> (such as optional CSS rules) and inline C# style overrides.
/// </summary>
public sealed class ComputedStyle
{
    /// <summary>
    /// Gets the default computed style with standard baseline typography and zero spacing.
    /// </summary>
    public static ComputedStyle Default { get; } = new();

    /// <summary>
    /// Gets the resolved foreground text color, or <see langword="null"/> if unspecified.
    /// </summary>
    public Color? TextColor { get; init; }

    /// <summary>
    /// Gets the resolved background color, or <see langword="null"/> if transparent/unspecified.
    /// </summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>
    /// Gets the resolved font size in logical pixels (defaults to 16.0).
    /// </summary>
    public double FontSize { get; init; } = 16.0;

    /// <summary>
    /// Gets the resolved font weight (defaults to <see cref="FontWeight.Normal"/>).
    /// </summary>
    public FontWeight FontWeight { get; init; } = FontWeight.Normal;

    /// <summary>
    /// Gets the resolved font family name, or <see langword="null"/> for the platform system font.
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Gets the resolved horizontal text alignment.
    /// </summary>
    public TextAlign TextAlign { get; init; } = TextAlign.Left;

    /// <summary>
    /// Gets the resolved inner padding thickness in logical pixels.
    /// </summary>
    public Thickness Padding { get; init; } = Thickness.Zero;

    /// <summary>
    /// Gets the resolved outer margin thickness in logical pixels.
    /// </summary>
    public Thickness Margin { get; init; } = Thickness.Zero;

    /// <summary>
    /// Gets the resolved corner border radius in logical pixels.
    /// </summary>
    public double BorderRadius { get; init; }

    /// <summary>
    /// Gets the resolved element opacity in the range [0.0, 1.0].
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Gets the explicit width in logical pixels, or <see langword="null"/> for auto sizing.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>
    /// Gets the explicit height in logical pixels, or <see langword="null"/> for auto sizing.
    /// </summary>
    public double? Height { get; init; }
}
