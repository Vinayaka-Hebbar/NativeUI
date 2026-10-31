namespace NativeUI.Layout;

/// <summary>
/// Unicode Character Database (UCD) lookup tables for UAX #9 (<c>Bidi_Class</c> and <c>Bidi_Paired_Bracket</c>),
/// UAX #14 (<c>Line_Break</c>), and UAX #29 (<c>Word_Break</c>) across <c>U+0000..U+10FFFF</c>.
/// </summary>
internal static partial class UnicodeDataTables
{
    /// <summary>
    /// Gets the Unicode bidirectional class of a code point.
    /// </summary>
    public static BidiClass GetBidiClass(int codePoint)
    {
        return (BidiClass)LookupRangeProperty(BidiClassRuns, codePoint);
    }

    /// <summary>
    /// Gets the Unicode line-break property using the line-break analyzer's numeric values.
    /// </summary>
    public static byte GetLineBreakProperty(int codePoint)
    {
        return LookupRangeProperty(LineBreakRuns, codePoint);
    }

    /// <summary>
    /// Gets the Unicode word-break property using the word-boundary analyzer's numeric values.
    /// </summary>
    public static byte GetWordBreakProperty(int codePoint)
    {
        return LookupRangeProperty(WordBreakRuns, codePoint);
    }

    /// <summary>
    /// Gets the paired closing code point when the input is a Unicode opening bracket.
    /// </summary>
    public static bool TryGetMatchingClosingBracket(int codePoint, out int closingCodePoint)
    {
        ReadOnlySpan<int> keys = OpenBracketCodePoints;
        int idx = keys.BinarySearch(codePoint);
        if (idx >= 0)
        {
            closingCodePoint = MatchingCloseBracketCodePoints[idx];
            return true;
        }

        closingCodePoint = 0;
        return false;
    }

    /// <summary>
    /// Gets whether the code point has Unicode paired-bracket type Close.
    /// </summary>
    public static bool IsClosingBracket(int codePoint)
    {
        return CloseBracketCodePoints.BinarySearch(codePoint) >= 0;
    }

    private static byte LookupRangeProperty(ReadOnlySpan<uint> packedRuns, int codePoint)
    {
        if ((uint)codePoint > 0x10FFFFu)
        {
            return 0;
        }

        // Each entry packs a range start into the high bits and its property into the low byte.
        uint target = ((uint)codePoint << 8) | 0xFFu;
        int lo = 0;
        int hi = packedRuns.Length - 1;

        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (packedRuns[mid] <= target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return hi >= 0 ? (byte)(packedRuns[hi] & 0xFFu) : (byte)0;
    }

}
