namespace NativeUI.Media;

/// <summary>A brush resolved against the inherited SVG color at presentation time.</summary>
public sealed record SvgCurrentColorBrush : Brush
{
    /// <summary>Gets the immutable shared currentColor brush.</summary>
    public static SvgCurrentColorBrush Instance { get; } = new();
}
