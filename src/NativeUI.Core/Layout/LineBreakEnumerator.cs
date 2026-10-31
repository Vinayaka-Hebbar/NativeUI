using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace NativeUI.Layout;

/// <summary>
/// Represents a line break opportunity produced by the line-breaking analyzer.
/// </summary>
/// <param name="TextPosition">The UTF-16 character offset where the next line begins if wrapped here.</param>
/// <param name="IsRequired">Whether a line break is mandatory at this offset (for example, after a newline or paragraph separator).</param>
public readonly record struct LineBreakOpportunity(int TextPosition, bool IsRequired);

/// <summary>
/// Identifies mandatory and optional line-wrapping opportunities across grapheme clusters using UCD <see cref="UnicodeDataTables"/>.
/// </summary>
public static class LineBreakEnumerator
{
    private enum LineBreakClass : byte
    {
        XX = 0,
        AI = 1,
        AL = 2,
        B2 = 3,
        BA = 4,
        BB = 5,
        BK = 6,
        CB = 7,
        CL = 8,
        CM = 9,
        CR = 10,
        EX = 11,
        GL = 12,
        HY = 13,
        ID = 14,
        IN = 15,
        IS = 16,
        LF = 17,
        NS = 18,
        NU = 19,
        OP = 20,
        PO = 21,
        PR = 22,
        QU = 23,
        SA = 24,
        SG = 25,
        SP = 26,
        SY = 27,
        ZW = 28,
        NL = 29,
        WJ = 30,
        H2 = 31,
        H3 = 32,
        JL = 33,
        JT = 34,
        JV = 35,
        CP = 36,
        CJ = 37,
        HL = 38,
        RI = 39,
        EB = 40,
        EM = 41,
        ZWJ = 42,
        AK = 43,
        AP = 44,
        AS = 45,
        VF = 46,
        VI = 47,
        HH = 48
    }

    /// <summary>
    /// Computes the immutable sequence of <see cref="LineBreakOpportunity"/> positions for <paramref name="clusters"/>.
    /// </summary>
    public static ImmutableArray<LineBreakOpportunity> GetBreakOpportunities(
        string text,
        ImmutableArray<TextRange> clusters)
    {
        if (string.IsNullOrEmpty(text) || clusters.IsEmpty)
        {
            return ImmutableArray<LineBreakOpportunity>.Empty;
        }

        int count = clusters.Length;
        LineBreakClass[] rawClasses = new LineBreakClass[count];
        for (int i = 0; i < count; i++)
        {
            rawClasses[i] = ClassifyCluster(text, clusters[i]);
        }

        ImmutableArray<LineBreakOpportunity>.Builder breaks = ImmutableArray.CreateBuilder<LineBreakOpportunity>();

        LineBreakClass prevRaw = rawClasses[0];
        LineBreakClass prevBase = NormalizeBaseClass(prevRaw);
        LineBreakClass baseBeforeSpaces = prevBase;
        bool hadInterveningSpace = prevRaw == LineBreakClass.SP;
        int riCount = prevBase == LineBreakClass.RI ? 1 : 0;

        for (int i = 1; i < count; i++)
        {
            LineBreakClass currRaw = rawClasses[i];

            if (prevRaw == LineBreakClass.BK ||
                prevRaw == LineBreakClass.NL ||
                prevRaw == LineBreakClass.LF ||
                (prevRaw == LineBreakClass.CR && currRaw != LineBreakClass.LF))
            {
                breaks.Add(new LineBreakOpportunity(clusters[i].Start, IsRequired: true));
                prevRaw = currRaw;
                prevBase = NormalizeBaseClass(currRaw);
                baseBeforeSpaces = prevBase;
                hadInterveningSpace = currRaw == LineBreakClass.SP;
                riCount = prevBase == LineBreakClass.RI ? 1 : 0;
                continue;
            }

            if (currRaw is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
            {
                prevRaw = currRaw;
                continue;
            }

            if (currRaw == LineBreakClass.SP)
            {
                if (!hadInterveningSpace)
                {
                    baseBeforeSpaces = prevBase;
                }

                hadInterveningSpace = true;
                prevRaw = currRaw;
                continue;
            }

            if (currRaw == LineBreakClass.ZW)
            {
                prevRaw = currRaw;
                prevBase = LineBreakClass.ZW;
                baseBeforeSpaces = LineBreakClass.ZW;
                hadInterveningSpace = false;
                riCount = 0;
                continue;
            }

            if (prevBase == LineBreakClass.ZW || baseBeforeSpaces == LineBreakClass.ZW)
            {
                breaks.Add(new LineBreakOpportunity(clusters[i].Start, IsRequired: false));
                prevRaw = currRaw;
                prevBase = NormalizeBaseClass(currRaw);
                baseBeforeSpaces = prevBase;
                hadInterveningSpace = false;
                riCount = prevBase == LineBreakClass.RI ? 1 : 0;
                continue;
            }

            if (prevRaw == LineBreakClass.ZWJ)
            {
                prevRaw = currRaw;
                prevBase = NormalizeBaseClass(currRaw);
                baseBeforeSpaces = prevBase;
                hadInterveningSpace = false;
                continue;
            }

            if (currRaw is LineBreakClass.CM or LineBreakClass.ZWJ &&
                !hadInterveningSpace &&
                prevBase is not (LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW))
            {
                prevRaw = currRaw;
                continue;
            }

            LineBreakClass currBase = NormalizeBaseClass(currRaw);

            bool allowBreak = EvaluatePairBreak(
                prevBase,
                baseBeforeSpaces,
                currBase,
                hadInterveningSpace,
                riCount,
                i,
                rawClasses);

            if (allowBreak)
            {
                breaks.Add(new LineBreakOpportunity(clusters[i].Start, IsRequired: false));
            }

            if (currBase == LineBreakClass.RI)
            {
                riCount++;
            }
            else
            {
                riCount = 0;
            }

            prevRaw = currRaw;
            prevBase = currBase;
            baseBeforeSpaces = currBase;
            hadInterveningSpace = false;
        }

        int finalEnd = clusters[^1].End;
        bool endsWithHardBreak = prevRaw is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL;
        if (breaks.Count == 0 || breaks[^1].TextPosition != finalEnd)
        {
            breaks.Add(new LineBreakOpportunity(finalEnd, IsRequired: endsWithHardBreak));
        }

        return breaks.ToImmutable();
    }

    private static LineBreakClass NormalizeBaseClass(LineBreakClass cls)
    {
        return cls switch
        {
            LineBreakClass.AI or LineBreakClass.SG or LineBreakClass.XX or LineBreakClass.SA or LineBreakClass.CM or LineBreakClass.ZWJ => LineBreakClass.AL,
            LineBreakClass.CJ => LineBreakClass.NS,
            LineBreakClass.HH => LineBreakClass.BA,
            _ => cls
        };
    }

    private static bool EvaluatePairBreak(
        LineBreakClass immediateLeft,
        LineBreakClass leftBeforeSpaces,
        LineBreakClass right,
        bool hadSpace,
        int riCount,
        int rightIndex,
        LineBreakClass[] allClasses)
    {
        if ((!hadSpace && immediateLeft == LineBreakClass.WJ) || right == LineBreakClass.WJ)
        {
            return false;
        }

        if (!hadSpace && immediateLeft == LineBreakClass.GL)
        {
            return false;
        }

        if (right == LineBreakClass.GL && !hadSpace && immediateLeft is not (LineBreakClass.BA or LineBreakClass.HY))
        {
            return false;
        }

        if (right is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.IS or LineBreakClass.SY)
        {
            return false;
        }

        if (leftBeforeSpaces == LineBreakClass.OP)
        {
            return false;
        }

        if (leftBeforeSpaces == LineBreakClass.QU && right == LineBreakClass.OP)
        {
            return false;
        }

        if (leftBeforeSpaces is LineBreakClass.CL or LineBreakClass.CP && right == LineBreakClass.NS)
        {
            return false;
        }

        if (leftBeforeSpaces == LineBreakClass.B2 && right == LineBreakClass.B2)
        {
            return false;
        }

        if (hadSpace)
        {
            return true;
        }

        if (immediateLeft == LineBreakClass.QU || right == LineBreakClass.QU)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.CB || right == LineBreakClass.CB)
        {
            return true;
        }

        if (right is LineBreakClass.BA or LineBreakClass.HY or LineBreakClass.NS || immediateLeft == LineBreakClass.BB)
        {
            return false;
        }

        if (immediateLeft is LineBreakClass.HY or LineBreakClass.BA &&
            rightIndex >= 2 && allClasses[rightIndex - 2] == LineBreakClass.HL)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.SY && right == LineBreakClass.HL)
        {
            return false;
        }

        if (right == LineBreakClass.IN)
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.AL or LineBreakClass.HL && right == LineBreakClass.NU) ||
            (immediateLeft == LineBreakClass.NU && right is LineBreakClass.AL or LineBreakClass.HL))
        {
            return false;
        }

        if ((immediateLeft == LineBreakClass.PR && right is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM) ||
            (immediateLeft is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM && right == LineBreakClass.PO))
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.PR or LineBreakClass.PO && right is LineBreakClass.AL or LineBreakClass.HL) ||
            (immediateLeft is LineBreakClass.AL or LineBreakClass.HL && right is LineBreakClass.PR or LineBreakClass.PO))
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.NU or LineBreakClass.SY or LineBreakClass.IS or LineBreakClass.PR or LineBreakClass.PO) &&
            right == LineBreakClass.NU)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.NU && right is LineBreakClass.PO or LineBreakClass.PR)
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.PO or LineBreakClass.PR) && right == LineBreakClass.OP &&
            rightIndex + 1 < allClasses.Length && allClasses[rightIndex + 1] == LineBreakClass.NU)
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.HY or LineBreakClass.IS) && right == LineBreakClass.NU)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.JL && right is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3)
        {
            return false;
        }

        if (immediateLeft is LineBreakClass.JV or LineBreakClass.H2 && right is LineBreakClass.JV or LineBreakClass.JT)
        {
            return false;
        }

        if (immediateLeft is LineBreakClass.JT or LineBreakClass.H3 && right == LineBreakClass.JT)
        {
            return false;
        }

        if (immediateLeft is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3 &&
            right == LineBreakClass.PO)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.PR &&
            right is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3)
        {
            return false;
        }

        if (immediateLeft is LineBreakClass.AL or LineBreakClass.HL && right is LineBreakClass.AL or LineBreakClass.HL)
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.IS && right is LineBreakClass.AL or LineBreakClass.HL)
        {
            return false;
        }

        if ((immediateLeft is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU && right == LineBreakClass.OP) ||
            (immediateLeft == LineBreakClass.CP && right is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.NU))
        {
            return false;
        }

        if (immediateLeft == LineBreakClass.RI && right == LineBreakClass.RI)
        {
            return (riCount & 1) == 0;
        }

        if (immediateLeft == LineBreakClass.EB && right == LineBreakClass.EM)
        {
            return false;
        }

        return true;
    }

    private static LineBreakClass ClassifyCluster(string text, TextRange cluster)
    {
        if (cluster.Length == 2 && text[cluster.Start] == '\r' && text[cluster.Start + 1] == '\n')
        {
            return LineBreakClass.LF;
        }

        if (!Rune.TryGetRuneAt(text, cluster.Start, out Rune rune))
        {
            return LineBreakClass.AL;
        }

        LineBreakClass raw = (LineBreakClass)UnicodeDataTables.GetLineBreakProperty(rune.Value);
        if (raw == LineBreakClass.SA)
        {
            UnicodeCategory cat = Rune.GetUnicodeCategory(rune);
            return cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                ? LineBreakClass.CM
                : LineBreakClass.AL;
        }

        return raw;
    }
}
