namespace NativeUI.Media;

/// <summary>
/// Specifies CSS transformations applied together to one fallback position.
/// </summary>
[Flags]
public enum PositionTryTactic
{
    /// <summary>
    /// No transformation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Reflect across the inline axis.
    /// </summary>
    FlipBlock = 1,

    /// <summary>
    /// Reflect across the block axis.
    /// </summary>
    FlipInline = 2,

    /// <summary>
    /// Reflect across the start-to-end diagonal.
    /// </summary>
    FlipStart = 4,

    /// <summary>
    /// Reflect across the vertical axis.
    /// </summary>
    FlipX = 8,

    /// <summary>
    /// Reflect across the horizontal axis.
    /// </summary>
    FlipY = 16
}
