using System.Collections.Immutable;

namespace NativeUI.Layout;

/// <summary>
/// Represents an immutable single wrapped line in an <see cref="ITextLayout"/>.
/// </summary>
public sealed class TextLine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextLine"/> class.
    /// </summary>
    public TextLine(
        int lineIndex,
        int startTextIndex,
        int length,
        Rect bounds,
        double baselineY,
        bool isRightToLeft,
        ImmutableArray<TextClusterMetrics> clusters,
        Rect? selectionBounds = null)
    {
        LineIndex = lineIndex;
        StartTextIndex = startTextIndex;
        Length = length;
        Bounds = bounds;
        SelectionBounds = selectionBounds ?? bounds;
        BaselineY = baselineY;
        IsRightToLeft = isRightToLeft;
        Clusters = clusters.IsDefault ? ImmutableArray<TextClusterMetrics>.Empty : clusters;
    }

    /// <summary>
    /// Gets the zero-based index of this line within the <see cref="ITextLayout"/>.
    /// </summary>
    public int LineIndex { get; }

    /// <summary>
    /// Gets the starting UTF-16 character index of this line in the full text.
    /// </summary>
    public int StartTextIndex { get; }

    /// <summary>
    /// Gets the UTF-16 character length of this line (excluding trailing line break characters).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the exclusive ending UTF-16 character index of this line.
    /// </summary>
    public int EndTextIndex => StartTextIndex + Length;

    /// <summary>
    /// Gets the bounding rectangle of the line relative to the text layout origin.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the font ascent/descent bounds used for selection, excluding extra line spacing.
    /// </summary>
    public Rect SelectionBounds { get; }

    /// <summary>
    /// Gets the baseline Y offset relative to the text layout origin.
    /// </summary>
    public double BaselineY { get; }

    /// <summary>
    /// Gets a value indicating whether the paragraph base direction of this line is right-to-left.
    /// </summary>
    public bool IsRightToLeft { get; }

    /// <summary>
    /// Gets the immutable grapheme cluster metrics along this line in visual left-to-right order.
    /// </summary>
    public ImmutableArray<TextClusterMetrics> Clusters { get; }
}
