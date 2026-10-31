using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace NativeUI.Layout;

/// <summary>
/// Represents a directional run of text with a resolved Unicode bidirectional embedding level.
/// </summary>
/// <param name="StartIndex">The starting UTF-16 character index in the source string.</param>
/// <param name="Length">The number of UTF-16 code units in the run.</param>
/// <param name="EmbeddingLevel">The resolved BiDi embedding level (even = left-to-right, odd = right-to-left).</param>
public readonly record struct BiDiTextRun(int StartIndex, int Length, byte EmbeddingLevel)
{
    /// <summary>
    /// Gets the exclusive ending UTF-16 character index.
    /// </summary>
    public int EndIndex => StartIndex + Length;

    /// <summary>
    /// Gets a value indicating whether this run has right-to-left directionality (odd embedding level).
    /// </summary>
    public bool IsRightToLeft => (EmbeddingLevel & 1) != 0;
}

/// <summary>
/// Provides Unicode grapheme boundary segmentation (<see cref="StringInfo.ParseCombiningCharacters"/>),
/// UAX #29 word-boundary detection using UCD <see cref="UnicodeDataTables"/>,
/// UAX #14 line-break opportunities, and UAX #9 bidirectional run resolution.
/// </summary>
public static class UnicodeTextSegmentation
{
    private enum WordBreakProperty : byte
    {
        Other = 0,
        ALetter = 1,
        Format = 2,
        Katakana = 3,
        MidLetter = 4,
        MidNum = 5,
        Numeric = 6,
        ExtendNumLet = 7,
        CR = 8,
        Extend = 9,
        LF = 10,
        MidNumLet = 11,
        Newline = 12,
        RegionalIndicator = 13,
        HebrewLetter = 14,
        SingleQuote = 15,
        DoubleQuote = 16,
        EBase = 17,
        EBaseGAZ = 18,
        EModifier = 19,
        GlueAfterZwj = 20,
        ZWJ = 21,
        WSegSpace = 22
    }

    /// <summary>
    /// Returns the starting UTF-16 indices of each extended grapheme cluster in <paramref name="text"/>
    /// using <see cref="StringInfo.ParseCombiningCharacters(string)"/>.
    /// </summary>
    public static int[] GetCharacterBoundaries(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return StringInfo.ParseCombiningCharacters(text);
    }

    /// <summary>
    /// Returns the immutable sequence of complete grapheme clusters covering <c>[startIndex, startIndex + length)</c>.
    /// The input range is normalized to grapheme boundaries first so no cluster is ever split.
    /// </summary>
    public static ImmutableArray<TextRange> GetGraphemeClusters(string text, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(text) || length <= 0)
        {
            return ImmutableArray<TextRange>.Empty;
        }

        int[] boundaries = GetCharacterBoundaries(text);
        return GetGraphemeClusters(text, boundaries, startIndex, length);
    }

    /// <summary>
    /// Returns the immutable sequence of complete grapheme clusters covering <c>[startIndex, startIndex + length)</c>
    /// using precomputed <paramref name="characterBoundaries"/>.
    /// </summary>
    public static ImmutableArray<TextRange> GetGraphemeClusters(
        string text,
        int[] characterBoundaries,
        int startIndex,
        int length)
    {
        ArgumentNullException.ThrowIfNull(characterBoundaries);
        if (string.IsNullOrEmpty(text) || length <= 0 || characterBoundaries.Length == 0)
        {
            return ImmutableArray<TextRange>.Empty;
        }

        TextRange normalized = NormalizeRangeToGraphemeBoundaries(text, characterBoundaries, startIndex, length);
        if (normalized.IsEmpty)
        {
            return ImmutableArray<TextRange>.Empty;
        }

        int startPos = Array.BinarySearch(characterBoundaries, normalized.Start);
        if (startPos < 0)
        {
            startPos = Math.Max(0, ~startPos - 1);
        }

        ImmutableArray<TextRange>.Builder builder = ImmutableArray.CreateBuilder<TextRange>();
        for (int i = startPos; i < characterBoundaries.Length; i++)
        {
            int clusterStart = characterBoundaries[i];
            if (clusterStart >= normalized.End)
            {
                break;
            }

            int clusterEnd = (i + 1 < characterBoundaries.Length) ? characterBoundaries[i + 1] : text.Length;
            builder.Add(new TextRange(clusterStart, clusterEnd - clusterStart));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Normalizes <c>[startIndex, startIndex + length)</c> so both endpoints lie on valid grapheme cluster boundaries.
    /// Any partially covered cluster at the start or end is expanded to include the full cluster.
    /// </summary>
    public static TextRange NormalizeRangeToGraphemeBoundaries(string text, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TextRange.Empty;
        }

        int[] boundaries = GetCharacterBoundaries(text);
        return NormalizeRangeToGraphemeBoundaries(text, boundaries, startIndex, length);
    }

    /// <summary>
    /// Normalizes <c>[startIndex, startIndex + length)</c> using precomputed <paramref name="characterBoundaries"/>.
    /// </summary>
    public static TextRange NormalizeRangeToGraphemeBoundaries(
        string text,
        int[] characterBoundaries,
        int startIndex,
        int length)
    {
        ArgumentNullException.ThrowIfNull(characterBoundaries);
        if (string.IsNullOrEmpty(text) || characterBoundaries.Length == 0)
        {
            return TextRange.Empty;
        }

        if (length <= 0)
        {
            int snapped = SnapToLeadingBoundary(characterBoundaries, text.Length, Math.Clamp(startIndex, 0, text.Length));
            return new TextRange(snapped, 0);
        }

        int clampedStart = Math.Clamp(startIndex, 0, text.Length);
        int clampedEnd = Math.Clamp(startIndex + length, clampedStart, text.Length);

        int leadingStart = SnapToLeadingBoundary(characterBoundaries, text.Length, clampedStart);
        int trailingEnd = SnapToTrailingBoundary(characterBoundaries, text.Length, clampedEnd);

        return new TextRange(leadingStart, Math.Max(0, trailingEnd - leadingStart));
    }

    /// <summary>
    /// Snaps <paramref name="textIndex"/> backward to the leading boundary of the enclosing grapheme cluster.
    /// </summary>
    public static int SnapToLeadingGraphemeBoundary(string text, int textIndex)
    {
        if (string.IsNullOrEmpty(text) || textIndex <= 0)
        {
            return 0;
        }

        if (textIndex >= text.Length)
        {
            return text.Length;
        }

        int[] boundaries = GetCharacterBoundaries(text);
        return SnapToLeadingBoundary(boundaries, text.Length, textIndex);
    }

    /// <summary>
    /// Snaps <paramref name="textIndex"/> forward to the trailing boundary of the enclosing grapheme cluster.
    /// </summary>
    public static int SnapToTrailingGraphemeBoundary(string text, int textIndex)
    {
        if (string.IsNullOrEmpty(text) || textIndex <= 0)
        {
            return 0;
        }

        if (textIndex >= text.Length)
        {
            return text.Length;
        }

        int[] boundaries = GetCharacterBoundaries(text);
        return SnapToTrailingBoundary(boundaries, text.Length, textIndex);
    }

    /// <summary>
    /// Finds the word boundary range enclosing <paramref name="textIndex"/> using UAX #29 Word_Break properties.
    /// </summary>
    public static TextRange GetWordRangeAt(string text, int textIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TextRange.Empty;
        }

        ImmutableArray<TextRange> clusters = GetGraphemeClusters(text, 0, text.Length);
        return GetWordRangeAt(text, clusters, textIndex);
    }

    /// <summary>
    /// Finds the word boundary range enclosing <paramref name="textIndex"/> using precomputed <paramref name="clusters"/>.
    /// </summary>
    public static TextRange GetWordRangeAt(string text, ImmutableArray<TextRange> clusters, int textIndex)
    {
        if (string.IsNullOrEmpty(text) || clusters.IsEmpty)
        {
            return TextRange.Empty;
        }

        int clampedIndex = Math.Clamp(textIndex, 0, text.Length - 1);
        int targetIdx = 0;
        for (int i = 0; i < clusters.Length; i++)
        {
            if (clampedIndex >= clusters[i].Start && clampedIndex < clusters[i].End)
            {
                targetIdx = i;
                break;
            }
        }

        WordBreakProperty[] props = new WordBreakProperty[clusters.Length];
        for (int i = 0; i < clusters.Length; i++)
        {
            props[i] = ClassifyWordCluster(text, clusters[i]);
        }

        int start = targetIdx;
        while (start > 0 && !IsWordBreakBetween(props, start - 1, start))
        {
            start--;
        }

        int end = targetIdx;
        while (end + 1 < clusters.Length && !IsWordBreakBetween(props, end, end + 1))
        {
            end++;
        }

        return new TextRange(clusters[start].Start, clusters[end].End - clusters[start].Start);
    }

    /// <summary>
    /// Returns the line-break opportunities within <c>[startIndex, startIndex + length)</c>.
    /// </summary>
    public static ImmutableArray<LineBreakOpportunity> GetLineBreakOpportunities(string text, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(text) || length <= 0)
        {
            return ImmutableArray<LineBreakOpportunity>.Empty;
        }

        ImmutableArray<TextRange> clusters = GetGraphemeClusters(text, startIndex, length);
        return LineBreakEnumerator.GetBreakOpportunities(text, clusters);
    }

    /// <summary>
    /// Resolves UAX #9 bidirectional embedding levels for a paragraph slice <c>[paragraphStart, paragraphStart + paragraphLength)</c>
    /// and returns the logical <see cref="BiDiTextRun"/> sequence along with <paramref name="baseIsRightToLeft"/>.
    /// </summary>
    public static ImmutableArray<BiDiTextRun> AnalyzeParagraphBiDi(
        string text,
        int paragraphStart,
        int paragraphLength,
        out bool baseIsRightToLeft)
    {
        baseIsRightToLeft = false;
        if (string.IsNullOrEmpty(text) || paragraphLength <= 0)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        ImmutableArray<TextRange> clusters = GetGraphemeClusters(text, paragraphStart, paragraphLength);
        if (clusters.IsEmpty)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        BidiAlgorithm.ParagraphBidiData data = BidiAlgorithm.AnalyzeParagraph(text, paragraphStart, clusters);
        baseIsRightToLeft = data.BaseIsRightToLeft;
        return BidiAlgorithm.BuildLogicalRuns(in data);
    }

    /// <summary>
    /// Orders the directional runs on a wrapped line <c>[lineStart, lineStart + lineLength)</c> in visual left-to-right order
    /// applying UAX #9 rules L1 and L2.
    /// </summary>
    public static ImmutableArray<BiDiTextRun> OrderLineVisualRuns(
        string text,
        int paragraphStart,
        int paragraphLength,
        int lineStart,
        int lineLength,
        out bool baseIsRightToLeft)
    {
        baseIsRightToLeft = false;
        if (string.IsNullOrEmpty(text) || paragraphLength <= 0 || lineLength <= 0)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        ImmutableArray<TextRange> clusters = GetGraphemeClusters(text, paragraphStart, paragraphLength);
        if (clusters.IsEmpty)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        BidiAlgorithm.ParagraphBidiData data = BidiAlgorithm.AnalyzeParagraph(text, paragraphStart, clusters);
        baseIsRightToLeft = data.BaseIsRightToLeft;
        return BidiAlgorithm.OrderLineVisualRuns(in data, lineStart, lineLength);
    }

    private static int SnapToLeadingBoundary(int[] boundaries, int textLength, int textIndex)
    {
        if (textIndex <= 0 || boundaries.Length == 0)
        {
            return 0;
        }

        if (textIndex >= textLength)
        {
            return textLength;
        }

        int pos = Array.BinarySearch(boundaries, textIndex);
        if (pos >= 0)
        {
            return boundaries[pos];
        }

        int insertion = ~pos;
        return insertion > 0 ? boundaries[insertion - 1] : 0;
    }

    private static int SnapToTrailingBoundary(int[] boundaries, int textLength, int textIndex)
    {
        if (textIndex <= 0 || boundaries.Length == 0)
        {
            return 0;
        }

        if (textIndex >= textLength)
        {
            return textLength;
        }

        int pos = Array.BinarySearch(boundaries, textIndex);
        if (pos >= 0)
        {
            return boundaries[pos];
        }

        int insertion = ~pos;
        return insertion < boundaries.Length ? boundaries[insertion] : textLength;
    }

    private static WordBreakProperty ClassifyWordCluster(string text, TextRange cluster)
    {
        if (!Rune.TryGetRuneAt(text, cluster.Start, out Rune rune))
        {
            return WordBreakProperty.Other;
        }

        return (WordBreakProperty)UnicodeDataTables.GetWordBreakProperty(rune.Value);
    }

    private static bool IsWordBreakBetween(WordBreakProperty[] props, int leftIdx, int rightIdx)
    {
        WordBreakProperty left = props[leftIdx];
        WordBreakProperty right = props[rightIdx];

        if (left is WordBreakProperty.CR or WordBreakProperty.LF or WordBreakProperty.Newline ||
            right is WordBreakProperty.CR or WordBreakProperty.LF or WordBreakProperty.Newline)
        {
            return true;
        }

        if (left == WordBreakProperty.WSegSpace && right == WordBreakProperty.WSegSpace)
        {
            return false;
        }

        bool leftIsAhLetter = left is WordBreakProperty.ALetter or WordBreakProperty.HebrewLetter;
        bool rightIsAhLetter = right is WordBreakProperty.ALetter or WordBreakProperty.HebrewLetter;

        if (leftIsAhLetter && rightIsAhLetter)
        {
            return false;
        }

        bool rightIsMidLetter = right is WordBreakProperty.MidLetter or WordBreakProperty.MidNumLet or WordBreakProperty.SingleQuote;
        bool leftIsMidLetter = left is WordBreakProperty.MidLetter or WordBreakProperty.MidNumLet or WordBreakProperty.SingleQuote;

        if (leftIsAhLetter && rightIsMidLetter && rightIdx + 1 < props.Length &&
            props[rightIdx + 1] is WordBreakProperty.ALetter or WordBreakProperty.HebrewLetter)
        {
            return false;
        }

        if (leftIsMidLetter && rightIsAhLetter && leftIdx > 0 &&
            props[leftIdx - 1] is WordBreakProperty.ALetter or WordBreakProperty.HebrewLetter)
        {
            return false;
        }

        if (left == WordBreakProperty.HebrewLetter && right == WordBreakProperty.SingleQuote)
        {
            return false;
        }

        if (left == WordBreakProperty.HebrewLetter && right == WordBreakProperty.DoubleQuote &&
            rightIdx + 1 < props.Length && props[rightIdx + 1] == WordBreakProperty.HebrewLetter)
        {
            return false;
        }

        if (left == WordBreakProperty.DoubleQuote && right == WordBreakProperty.HebrewLetter &&
            leftIdx > 0 && props[leftIdx - 1] == WordBreakProperty.HebrewLetter)
        {
            return false;
        }

        if ((left == WordBreakProperty.Numeric && right == WordBreakProperty.Numeric) ||
            (leftIsAhLetter && right == WordBreakProperty.Numeric) ||
            (left == WordBreakProperty.Numeric && rightIsAhLetter))
        {
            return false;
        }

        bool rightIsMidNum = right is WordBreakProperty.MidNum or WordBreakProperty.MidNumLet or WordBreakProperty.SingleQuote;
        bool leftIsMidNum = left is WordBreakProperty.MidNum or WordBreakProperty.MidNumLet or WordBreakProperty.SingleQuote;

        if (left == WordBreakProperty.Numeric && rightIsMidNum &&
            rightIdx + 1 < props.Length && props[rightIdx + 1] == WordBreakProperty.Numeric)
        {
            return false;
        }

        if (leftIsMidNum && right == WordBreakProperty.Numeric &&
            leftIdx > 0 && props[leftIdx - 1] == WordBreakProperty.Numeric)
        {
            return false;
        }

        if (left == WordBreakProperty.Katakana && right == WordBreakProperty.Katakana)
        {
            return false;
        }

        bool leftIsExtendable = leftIsAhLetter || left is WordBreakProperty.Numeric or WordBreakProperty.Katakana or WordBreakProperty.ExtendNumLet;
        bool rightIsExtendable = rightIsAhLetter || right is WordBreakProperty.Numeric or WordBreakProperty.Katakana;

        if (leftIsExtendable && right == WordBreakProperty.ExtendNumLet)
        {
            return false;
        }

        if (left == WordBreakProperty.ExtendNumLet && rightIsExtendable)
        {
            return false;
        }

        if (left == WordBreakProperty.RegionalIndicator && right == WordBreakProperty.RegionalIndicator)
        {
            return false;
        }

        return true;
    }
}
