namespace NativeUI.Layout;

/// <summary>
/// Represents a contiguous UTF-16 character range within a text string.
/// </summary>
/// <param name="Start">The zero-based starting UTF-16 character index.</param>
/// <param name="Length">The number of UTF-16 code units in the range.</param>
public readonly record struct TextRange(int Start, int Length)
{
    /// <summary>
    /// Gets an empty <see cref="TextRange"/> at index 0.
    /// </summary>
    public static TextRange Empty => new(0, 0);

    /// <summary>
    /// Gets the exclusive ending UTF-16 character index (<see cref="Start"/> + <see cref="Length"/>).
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// Gets a value indicating whether the range has zero or negative length.
    /// </summary>
    public bool IsEmpty => Length <= 0;

    /// <summary>
    /// Returns a value indicating whether <paramref name="textIndex"/> falls within <c>[Start, End)</c>.
    /// </summary>
    public bool Contains(int textIndex)
    {
        return textIndex >= Start && textIndex < End;
    }
}
