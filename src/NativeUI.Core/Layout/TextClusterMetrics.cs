namespace NativeUI.Layout;

/// <summary>
/// Represents the visual horizontal span and directionality of a single indivisible text cluster on a laid-out line.
/// </summary>
/// <param name="StartIndex">The starting UTF-16 index of the cluster in the full text.</param>
/// <param name="Length">The UTF-16 length of the cluster.</param>
/// <param name="Left">The visual left X coordinate relative to the text layout origin.</param>
/// <param name="Right">The visual right X coordinate relative to the text layout origin.</param>
/// <param name="IsRightToLeft">Whether this cluster belongs to a right-to-left directional run.</param>
public readonly record struct TextClusterMetrics(
    int StartIndex,
    int Length,
    double Left,
    double Right,
    bool IsRightToLeft)
{
    /// <summary>
    /// Gets the <see cref="TextRange"/> of this cluster.
    /// </summary>
    public TextRange Range => new(StartIndex, Length);

    /// <summary>
    /// Gets the exclusive ending UTF-16 index (<see cref="StartIndex"/> + <see cref="Length"/>).
    /// </summary>
    public int EndIndex => StartIndex + Length;

    /// <summary>
    /// Gets the visual width of the cluster (<see cref="Right"/> - <see cref="Left"/>).
    /// </summary>
    public double Width => Math.Max(0, Right - Left);

    /// <summary>
    /// Gets the horizontal midpoint of the cluster.
    /// </summary>
    public double Midpoint => (Left + Right) * 0.5;
}
