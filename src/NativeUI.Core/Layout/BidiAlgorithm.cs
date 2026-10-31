using System.Collections.Immutable;
using System.Text;

namespace NativeUI.Layout;

/// <summary>
/// Defines the 23 character directional classes from the Unicode Character Database (UAX #9).
/// Explicit numeric values match the <see cref="UnicodeDataTables"/> property values.
/// </summary>
public enum BidiClass : byte
{
    /// <summary>Left-to-Right.</summary>
    L = 0,
    /// <summary>Right-to-Left.</summary>
    R = 1,
    /// <summary>European Number.</summary>
    EN = 2,
    /// <summary>European Number Separator.</summary>
    ES = 3,
    /// <summary>European Number Terminator.</summary>
    ET = 4,
    /// <summary>Arabic Number.</summary>
    AN = 5,
    /// <summary>Common Number Separator.</summary>
    CS = 6,
    /// <summary>Paragraph Separator.</summary>
    B = 7,
    /// <summary>Segment Separator.</summary>
    S = 8,
    /// <summary>Whitespace.</summary>
    WS = 9,
    /// <summary>Other Neutrals.</summary>
    ON = 10,
    /// <summary>Left-to-Right Embedding.</summary>
    LRE = 11,
    /// <summary>Left-to-Right Override.</summary>
    LRO = 12,
    /// <summary>Right-to-Left Arabic.</summary>
    AL = 13,
    /// <summary>Right-to-Left Embedding.</summary>
    RLE = 14,
    /// <summary>Right-to-Left Override.</summary>
    RLO = 15,
    /// <summary>Pop Directional Format.</summary>
    PDF = 16,
    /// <summary>Nonspacing Mark.</summary>
    NSM = 17,
    /// <summary>Boundary Neutral.</summary>
    BN = 18,
    /// <summary>First Strong Isolate.</summary>
    FSI = 19,
    /// <summary>Left-to-Right Isolate.</summary>
    LRI = 20,
    /// <summary>Right-to-Left Isolate.</summary>
    RLI = 21,
    /// <summary>Pop Directional Isolate.</summary>
    PDI = 22
}

/// <summary>
/// Resolves bidirectional embedding levels, isolating run sequences across matched isolate pairs,
/// paired brackets, and per-line visual run ordering using UCD <see cref="UnicodeDataTables"/>.
/// </summary>
public static class BidiAlgorithm
{
    private const int MaxDepth = 125;
    private const int MaxBracketStack = 63;

    /// <summary>
    /// Stores the resolved paragraph bidirectional state reused across wrapped lines of a paragraph.
    /// </summary>
    public readonly struct ParagraphBidiData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParagraphBidiData"/> struct.
        /// </summary>
        public ParagraphBidiData(
            int paragraphStart,
            ImmutableArray<TextRange> clusters,
            BidiClass[] initialClasses,
            byte[] resolvedLevels,
            byte paragraphBaseLevel)
        {
            ParagraphStart = paragraphStart;
            Clusters = clusters;
            InitialClasses = initialClasses;
            ResolvedLevels = resolvedLevels;
            ParagraphBaseLevel = paragraphBaseLevel;
        }

        /// <summary>
        /// Gets the starting UTF-16 index of the paragraph in the source text.
        /// </summary>
        public int ParagraphStart { get; }

        /// <summary>
        /// Gets the grapheme clusters spanning the paragraph.
        /// </summary>
        public ImmutableArray<TextRange> Clusters { get; }

        /// <summary>
        /// Gets the initial per-cluster <see cref="BidiClass"/> array prior to weak/neutral resolution (used by rule L1).
        /// </summary>
        public BidiClass[] InitialClasses { get; }

        /// <summary>
        /// Gets the resolved per-cluster embedding levels.
        /// </summary>
        public byte[] ResolvedLevels { get; }

        /// <summary>
        /// Gets the paragraph base embedding level (0 for LTR, 1 for RTL).
        /// </summary>
        public byte ParagraphBaseLevel { get; }

        /// <summary>
        /// Gets a value indicating whether the paragraph base direction is right-to-left.
        /// </summary>
        public bool BaseIsRightToLeft => (ParagraphBaseLevel & 1) != 0;
    }

    /// <summary>
    /// Analyzes a paragraph's grapheme clusters and resolves their bidirectional embedding levels.
    /// </summary>
    public static ParagraphBidiData AnalyzeParagraph(
        string text,
        int paragraphStart,
        ImmutableArray<TextRange> clusters)
    {
        int count = clusters.Length;
        if (count == 0)
        {
            return new ParagraphBidiData(paragraphStart, clusters, [], [], 0);
        }

        BidiClass[] initialClasses = new BidiClass[count];
        BidiClass[] workingClasses = new BidiClass[count];

        for (int i = 0; i < count; i++)
        {
            BidiClass cls = ClassifyCluster(text, clusters[i]);
            initialClasses[i] = cls;
            workingClasses[i] = cls;
        }

        byte paragraphBaseLevel = DetermineParagraphLevel(workingClasses, 0, count);
        byte[] levels = new byte[count];

        ResolveExplicitLevels(workingClasses, levels, paragraphBaseLevel);
        ProcessIsolatingRunSequences(text, clusters, initialClasses, workingClasses, levels, paragraphBaseLevel);
        ResolveImplicitLevels(workingClasses, levels);

        return new ParagraphBidiData(paragraphStart, clusters, initialClasses, levels, paragraphBaseLevel);
    }

    /// <summary>
    /// Groups consecutive clusters with identical embedding levels into logical <see cref="BiDiTextRun"/> instances.
    /// </summary>
    public static ImmutableArray<BiDiTextRun> BuildLogicalRuns(in ParagraphBidiData data)
    {
        ImmutableArray<TextRange> clusters = data.Clusters;
        if (clusters.IsEmpty)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        byte[] levels = data.ResolvedLevels;
        ImmutableArray<BiDiTextRun>.Builder builder = ImmutableArray.CreateBuilder<BiDiTextRun>();

        int runStart = clusters[0].Start;
        byte currentLevel = levels[0];

        for (int i = 1; i < clusters.Length; i++)
        {
            if (levels[i] != currentLevel)
            {
                int runEnd = clusters[i - 1].End;
                builder.Add(new BiDiTextRun(runStart, runEnd - runStart, currentLevel));
                runStart = clusters[i].Start;
                currentLevel = levels[i];
            }
        }

        int finalEnd = clusters[^1].End;
        builder.Add(new BiDiTextRun(runStart, finalEnd - runStart, currentLevel));
        return builder.ToImmutable();
    }

    /// <summary>
    /// Applies UAX #9 rules L1 (trailing whitespace reset) and L2 (level-based reversal) to a single wrapped line
    /// and returns its <see cref="BiDiTextRun"/> sequence in visual left-to-right order.
    /// </summary>
    public static ImmutableArray<BiDiTextRun> OrderLineVisualRuns(
        in ParagraphBidiData data,
        int lineStart,
        int lineLength)
    {
        ImmutableArray<TextRange> clusters = data.Clusters;
        if (clusters.IsEmpty || lineLength <= 0)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        int lineEnd = lineStart + lineLength;
        int firstCluster = -1;
        int lastCluster = -1;

        for (int i = 0; i < clusters.Length; i++)
        {
            if (clusters[i].End > lineStart && clusters[i].Start < lineEnd)
            {
                if (firstCluster == -1)
                {
                    firstCluster = i;
                }

                lastCluster = i;
            }
        }

        if (firstCluster == -1 || lastCluster < firstCluster)
        {
            return ImmutableArray<BiDiTextRun>.Empty;
        }

        int lineClusterCount = lastCluster - firstCluster + 1;
        byte[] lineLevels = new byte[lineClusterCount];
        Array.Copy(data.ResolvedLevels, firstCluster, lineLevels, 0, lineClusterCount);

        byte baseLevel = data.ParagraphBaseLevel;
        for (int i = 0; i < lineClusterCount; i++)
        {
            BidiClass orig = data.InitialClasses[firstCluster + i];
            if (orig is BidiClass.B or BidiClass.S)
            {
                lineLevels[i] = baseLevel;
                for (int k = i - 1; k >= 0 && IsResettableByL1(data.InitialClasses[firstCluster + k]); k--)
                {
                    lineLevels[k] = baseLevel;
                }
            }
        }

        for (int k = lineClusterCount - 1; k >= 0 && IsResettableByL1(data.InitialClasses[firstCluster + k]); k--)
        {
            lineLevels[k] = baseLevel;
        }

        List<BiDiTextRun> lineRuns = [];
        int runStart = Math.Max(lineStart, clusters[firstCluster].Start);
        byte currentLevel = lineLevels[0];

        for (int i = 1; i < lineClusterCount; i++)
        {
            if (lineLevels[i] != currentLevel)
            {
                int runEnd = Math.Min(lineEnd, clusters[firstCluster + i - 1].End);
                lineRuns.Add(new BiDiTextRun(runStart, runEnd - runStart, currentLevel));
                runStart = clusters[firstCluster + i].Start;
                currentLevel = lineLevels[i];
            }
        }

        int finalRunEnd = Math.Min(lineEnd, clusters[lastCluster].End);
        lineRuns.Add(new BiDiTextRun(runStart, finalRunEnd - runStart, currentLevel));

        if (lineRuns.Count <= 1)
        {
            return [.. lineRuns];
        }

        int maxLevel = 0;
        int minOddLevel = int.MaxValue;
        foreach (BiDiTextRun run in lineRuns)
        {
            if (run.EmbeddingLevel > maxLevel)
            {
                maxLevel = run.EmbeddingLevel;
            }

            if ((run.EmbeddingLevel & 1) != 0 && run.EmbeddingLevel < minOddLevel)
            {
                minOddLevel = run.EmbeddingLevel;
            }
        }

        if (minOddLevel == int.MaxValue)
        {
            return [.. lineRuns];
        }

        for (int level = maxLevel; level >= minOddLevel; level--)
        {
            int i = 0;
            while (i < lineRuns.Count)
            {
                if (lineRuns[i].EmbeddingLevel >= level)
                {
                    int seqStart = i;
                    while (i + 1 < lineRuns.Count && lineRuns[i + 1].EmbeddingLevel >= level)
                    {
                        i++;
                    }

                    lineRuns.Reverse(seqStart, i - seqStart + 1);
                }

                i++;
            }
        }

        return [.. lineRuns];
    }

    private static bool IsResettableByL1(BidiClass cls)
    {
        return cls is BidiClass.WS
            or BidiClass.FSI
            or BidiClass.LRI
            or BidiClass.RLI
            or BidiClass.PDI
            or BidiClass.LRE
            or BidiClass.RLE
            or BidiClass.LRO
            or BidiClass.RLO
            or BidiClass.PDF
            or BidiClass.BN;
    }

    private static int FindMatchingPdiIndex(BidiClass[] classes, int startAfterInitiator)
    {
        int depth = 1;
        for (int i = startAfterInitiator; i < classes.Length; i++)
        {
            BidiClass cls = classes[i];
            if (cls is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI)
            {
                depth++;
            }
            else if (cls == BidiClass.PDI)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return classes.Length;
    }

    private static byte DetermineParagraphLevel(BidiClass[] classes, int start, int end)
    {
        int isolateDepth = 0;
        for (int i = start; i < end; i++)
        {
            BidiClass cls = classes[i];
            if (cls is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI)
            {
                isolateDepth++;
            }
            else if (cls == BidiClass.PDI)
            {
                if (isolateDepth > 0)
                {
                    isolateDepth--;
                }
            }
            else if (isolateDepth == 0)
            {
                if (cls == BidiClass.L)
                {
                    return 0;
                }

                if (cls is BidiClass.R or BidiClass.AL)
                {
                    return 1;
                }
            }
        }

        return 0;
    }

    private readonly record struct DirectionalStatus(byte Level, BidiClass OverrideState, bool IsolateState);

    private static void ResolveExplicitLevels(BidiClass[] classes, byte[] levels, byte paragraphLevel)
    {
        Span<DirectionalStatus> stack = stackalloc DirectionalStatus[MaxDepth + 2];
        int stackTop = 0;
        stack[0] = new DirectionalStatus(paragraphLevel, BidiClass.ON, IsolateState: false);

        int overflowIsolateCount = 0;
        int overflowEmbeddingCount = 0;
        int validIsolateCount = 0;

        for (int i = 0; i < classes.Length; i++)
        {
            BidiClass cls = classes[i];
            switch (cls)
            {
                case BidiClass.RLE:
                case BidiClass.LRE:
                case BidiClass.RLO:
                case BidiClass.LRO:
                {
                    bool isRtl = cls is BidiClass.RLE or BidiClass.RLO;
                    byte currentLevel = stack[stackTop].Level;
                    byte newLevel = isRtl
                        ? (byte)((currentLevel + 1) | 1)
                        : (byte)((currentLevel + 2) & ~1);

                    if (newLevel <= MaxDepth && overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                    {
                        BidiClass overrideState = cls == BidiClass.LRO
                            ? BidiClass.L
                            : (cls == BidiClass.RLO ? BidiClass.R : BidiClass.ON);
                        stack[++stackTop] = new DirectionalStatus(newLevel, overrideState, IsolateState: false);
                    }
                    else if (overflowIsolateCount == 0)
                    {
                        overflowEmbeddingCount++;
                    }

                    levels[i] = currentLevel;
                    classes[i] = BidiClass.BN;
                    break;
                }

                case BidiClass.RLI:
                case BidiClass.LRI:
                case BidiClass.FSI:
                {
                    DirectionalStatus current = stack[stackTop];
                    levels[i] = current.Level;
                    if (current.OverrideState != BidiClass.ON)
                    {
                        classes[i] = current.OverrideState;
                    }

                    int matchingPdi = cls == BidiClass.FSI ? FindMatchingPdiIndex(classes, i + 1) : classes.Length;
                    bool isRtl = cls == BidiClass.RLI ||
                                 (cls == BidiClass.FSI && DetermineParagraphLevel(classes, i + 1, matchingPdi) == 1);
                    byte newLevel = isRtl
                        ? (byte)((current.Level + 1) | 1)
                        : (byte)((current.Level + 2) & ~1);

                    if (newLevel <= MaxDepth && overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                    {
                        validIsolateCount++;
                        stack[++stackTop] = new DirectionalStatus(newLevel, BidiClass.ON, IsolateState: true);
                    }
                    else
                    {
                        overflowIsolateCount++;
                    }

                    break;
                }

                case BidiClass.PDI:
                {
                    if (overflowIsolateCount > 0)
                    {
                        overflowIsolateCount--;
                    }
                    else if (validIsolateCount > 0)
                    {
                        overflowEmbeddingCount = 0;
                        while (!stack[stackTop].IsolateState && stackTop > 0)
                        {
                            stackTop--;
                        }

                        if (stackTop > 0)
                        {
                            stackTop--;
                        }

                        validIsolateCount--;
                    }

                    DirectionalStatus current = stack[stackTop];
                    levels[i] = current.Level;
                    if (current.OverrideState != BidiClass.ON)
                    {
                        classes[i] = current.OverrideState;
                    }

                    break;
                }

                case BidiClass.PDF:
                {
                    levels[i] = stack[stackTop].Level;
                    classes[i] = BidiClass.BN;
                    if (overflowIsolateCount == 0)
                    {
                        if (overflowEmbeddingCount > 0)
                        {
                            overflowEmbeddingCount--;
                        }
                        else if (!stack[stackTop].IsolateState && stackTop > 0)
                        {
                            stackTop--;
                        }
                    }

                    break;
                }

                case BidiClass.B:
                    levels[i] = paragraphLevel;
                    break;

                default:
                {
                    DirectionalStatus current = stack[stackTop];
                    levels[i] = current.Level;
                    if (current.OverrideState != BidiClass.ON && cls != BidiClass.BN)
                    {
                        classes[i] = current.OverrideState;
                    }

                    break;
                }
            }
        }
    }

    private static bool IsRemovedByX9(BidiClass cls)
    {
        return cls is BidiClass.RLE
            or BidiClass.LRE
            or BidiClass.RLO
            or BidiClass.LRO
            or BidiClass.PDF
            or BidiClass.BN;
    }

    private static void ProcessIsolatingRunSequences(
        string text,
        ImmutableArray<TextRange> clusters,
        BidiClass[] initialClasses,
        BidiClass[] classes,
        byte[] levels,
        byte paragraphLevel)
    {
        int count = classes.Length;
        if (count == 0)
        {
            return;
        }

        // Build non-X9 character index list so BD7 level runs and BD13 isolate linking operate over active characters
        List<int> activeIndices = new(count);
        for (int i = 0; i < count; i++)
        {
            if (!IsRemovedByX9(initialClasses[i]))
            {
                activeIndices.Add(i);
            }
        }

        if (activeIndices.Count == 0)
        {
            return;
        }

        List<(int ActiveStart, int ActiveEnd, byte Level)> levelRuns = [];
        int rStart = 0;
        while (rStart < activeIndices.Count)
        {
            byte lvl = levels[activeIndices[rStart]];
            int rEnd = rStart + 1;
            while (rEnd < activeIndices.Count && levels[activeIndices[rEnd]] == lvl)
            {
                rEnd++;
            }

            levelRuns.Add((rStart, rEnd, lvl));
            rStart = rEnd;
        }

        int runCount = levelRuns.Count;
        int[] nextRunInSequence = new int[runCount];
        bool[] hasPredecessor = new bool[runCount];
        Array.Fill(nextRunInSequence, -1);

        for (int r = 0; r < runCount; r++)
        {
            int lastClusterIdx = activeIndices[levelRuns[r].ActiveEnd - 1];
            if (initialClasses[lastClusterIdx] is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI)
            {
                int matchingPdiClusterIdx = FindMatchingPdiIndex(initialClasses, lastClusterIdx + 1);
                if (matchingPdiClusterIdx < count)
                {
                    for (int targetRun = r + 1; targetRun < runCount; targetRun++)
                    {
                        int firstClusterInTarget = activeIndices[levelRuns[targetRun].ActiveStart];
                        if (firstClusterInTarget == matchingPdiClusterIdx &&
                            levelRuns[targetRun].Level == levelRuns[r].Level)
                        {
                            nextRunInSequence[r] = targetRun;
                            hasPredecessor[targetRun] = true;
                            break;
                        }
                    }
                }
            }
        }

        List<int> seqIndices = [];
        for (int r = 0; r < runCount; r++)
        {
            if (hasPredecessor[r])
            {
                continue;
            }

            seqIndices.Clear();
            int currRun = r;
            int firstActiveStart = levelRuns[r].ActiveStart;
            int lastActiveEnd = levelRuns[r].ActiveEnd;
            byte seqLevel = levelRuns[r].Level;

            while (currRun != -1)
            {
                (int s, int e, _) = levelRuns[currRun];
                for (int idx = s; idx < e; idx++)
                {
                    seqIndices.Add(activeIndices[idx]);
                }

                lastActiveEnd = e;
                currRun = nextRunInSequence[currRun];
            }

            byte prevLevel = firstActiveStart > 0 ? levels[activeIndices[firstActiveStart - 1]] : paragraphLevel;
            int lastClusterInSeq = activeIndices[lastActiveEnd - 1];
            bool endsWithUnmatchedIsolate = initialClasses[lastClusterInSeq] is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;
            byte nextLevel = (!endsWithUnmatchedIsolate && lastActiveEnd < activeIndices.Count)
                ? levels[activeIndices[lastActiveEnd]]
                : paragraphLevel;

            BidiClass sos = ((Math.Max(prevLevel, seqLevel) & 1) != 0) ? BidiClass.R : BidiClass.L;
            BidiClass eos = ((Math.Max(nextLevel, seqLevel) & 1) != 0) ? BidiClass.R : BidiClass.L;
            BidiClass embeddingDirection = (seqLevel & 1) != 0 ? BidiClass.R : BidiClass.L;

            ResolveSequenceWeakAndNeutrals(
                text,
                clusters,
                initialClasses,
                classes,
                seqIndices,
                sos,
                eos,
                embeddingDirection);
        }
    }

    private static void ResolveSequenceWeakAndNeutrals(
        string text,
        ImmutableArray<TextRange> clusters,
        BidiClass[] initialClasses,
        BidiClass[] classes,
        List<int> seq,
        BidiClass sos,
        BidiClass eos,
        BidiClass embeddingDir)
    {
        int len = seq.Count;
        if (len == 0)
        {
            return;
        }

        BidiClass prevType = sos;
        for (int i = 0; i < len; i++)
        {
            int idx = seq[i];
            BidiClass c = classes[idx];
            if (c == BidiClass.NSM)
            {
                classes[idx] = prevType is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI
                    ? BidiClass.ON
                    : prevType;
            }
            else if (c != BidiClass.BN)
            {
                prevType = c;
            }
        }

        BidiClass lastStrong = sos;
        for (int i = 0; i < len; i++)
        {
            int idx = seq[i];
            BidiClass c = classes[idx];
            if (c is BidiClass.L or BidiClass.R or BidiClass.AL)
            {
                lastStrong = c;
            }
            else if (c == BidiClass.EN && lastStrong == BidiClass.AL)
            {
                classes[idx] = BidiClass.AN;
            }
        }

        for (int i = 0; i < len; i++)
        {
            int idx = seq[i];
            if (classes[idx] == BidiClass.AL)
            {
                classes[idx] = BidiClass.R;
            }
        }

        for (int i = 1; i + 1 < len; i++)
        {
            int idx = seq[i];
            BidiClass prev = classes[seq[i - 1]];
            BidiClass next = classes[seq[i + 1]];

            if (classes[idx] is BidiClass.ES or BidiClass.CS && prev == BidiClass.EN && next == BidiClass.EN)
            {
                classes[idx] = BidiClass.EN;
            }
            else if (classes[idx] == BidiClass.CS && prev == BidiClass.AN && next == BidiClass.AN)
            {
                classes[idx] = BidiClass.AN;
            }
        }

        for (int i = 0; i < len; i++)
        {
            if (classes[seq[i]] == BidiClass.ET)
            {
                int etStart = i;
                while (i + 1 < len && classes[seq[i + 1]] == BidiClass.ET)
                {
                    i++;
                }

                int etEnd = i;
                bool adjacentEn = (etStart > 0 && classes[seq[etStart - 1]] == BidiClass.EN) ||
                                  (etEnd + 1 < len && classes[seq[etEnd + 1]] == BidiClass.EN);
                if (adjacentEn)
                {
                    for (int k = etStart; k <= etEnd; k++)
                    {
                        classes[seq[k]] = BidiClass.EN;
                    }
                }
            }
        }

        for (int i = 0; i < len; i++)
        {
            int idx = seq[i];
            if (classes[idx] is BidiClass.ES or BidiClass.ET or BidiClass.CS or BidiClass.BN)
            {
                classes[idx] = BidiClass.ON;
            }
        }

        lastStrong = sos;
        for (int i = 0; i < len; i++)
        {
            int idx = seq[i];
            BidiClass c = classes[idx];
            if (c is BidiClass.L or BidiClass.R)
            {
                lastStrong = c;
            }
            else if (c == BidiClass.EN && lastStrong == BidiClass.L)
            {
                classes[idx] = BidiClass.L;
            }
        }

        ResolvePairedBracketsInSequence(text, clusters, initialClasses, classes, seq, sos, embeddingDir);

        for (int i = 0; i < len; i++)
        {
            if (!IsNeutralOrIsolate(classes[seq[i]]))
            {
                continue;
            }

            int neutralStart = i;
            while (i + 1 < len && IsNeutralOrIsolate(classes[seq[i + 1]]))
            {
                i++;
            }

            int neutralEnd = i;
            BidiClass leading = neutralStart > 0 ? NormalizeStrongForNeutral(classes[seq[neutralStart - 1]]) : sos;
            BidiClass trailing = neutralEnd + 1 < len ? NormalizeStrongForNeutral(classes[seq[neutralEnd + 1]]) : eos;
            BidiClass resolved = (leading == trailing) ? leading : embeddingDir;

            for (int k = neutralStart; k <= neutralEnd; k++)
            {
                classes[seq[k]] = resolved;
            }
        }
    }

    private readonly record struct BracketPair(int SeqOpenPos, int SeqClosePos);

    private static void ResolvePairedBracketsInSequence(
        string text,
        ImmutableArray<TextRange> clusters,
        BidiClass[] initialClasses,
        BidiClass[] classes,
        List<int> seq,
        BidiClass sos,
        BidiClass embeddingDir)
    {
        Span<(int SeqPos, int ClosingCodePoint)> stack = stackalloc (int, int)[MaxBracketStack];
        int stackDepth = 0;
        List<BracketPair> pairs = [];
        int len = seq.Count;

        for (int i = 0; i < len; i++)
        {
            int clusterIdx = seq[i];
            if (classes[clusterIdx] != BidiClass.ON)
            {
                continue;
            }

            if (!Rune.TryGetRuneAt(text, clusters[clusterIdx].Start, out Rune rune))
            {
                continue;
            }

            if (UnicodeDataTables.TryGetMatchingClosingBracket(rune.Value, out int closingCp))
            {
                if (stackDepth >= MaxBracketStack)
                {
                    break;
                }

                stack[stackDepth++] = (i, closingCp);
            }
            else if (UnicodeDataTables.IsClosingBracket(rune.Value))
            {
                for (int s = stackDepth - 1; s >= 0; s--)
                {
                    if (stack[s].ClosingCodePoint == rune.Value ||
                        NormalizeCanonicalBracket(stack[s].ClosingCodePoint) == NormalizeCanonicalBracket(rune.Value))
                    {
                        pairs.Add(new BracketPair(stack[s].SeqPos, i));
                        stackDepth = s;
                        break;
                    }
                }
            }
        }

        if (pairs.Count == 0)
        {
            return;
        }

        pairs.Sort(static (a, b) => a.SeqOpenPos.CompareTo(b.SeqOpenPos));
        BidiClass oppositeDir = embeddingDir == BidiClass.L ? BidiClass.R : BidiClass.L;

        foreach (BracketPair pair in pairs)
        {
            bool foundEmbedding = false;
            bool foundOpposite = false;

            for (int i = pair.SeqOpenPos + 1; i < pair.SeqClosePos; i++)
            {
                BidiClass strong = NormalizeStrongOrNone(classes[seq[i]]);
                if (strong == embeddingDir)
                {
                    foundEmbedding = true;
                    break;
                }

                if (strong == oppositeDir)
                {
                    foundOpposite = true;
                }
            }

            BidiClass? resolvedBracketDir = null;
            if (foundEmbedding)
            {
                resolvedBracketDir = embeddingDir;
            }
            else if (foundOpposite)
            {
                BidiClass precedingStrong = sos;
                for (int k = pair.SeqOpenPos - 1; k >= 0; k--)
                {
                    BidiClass s = NormalizeStrongOrNone(classes[seq[k]]);
                    if (s is BidiClass.L or BidiClass.R)
                    {
                        precedingStrong = s;
                        break;
                    }
                }

                resolvedBracketDir = precedingStrong == oppositeDir ? oppositeDir : embeddingDir;
            }

            if (resolvedBracketDir is BidiClass dir)
            {
                classes[seq[pair.SeqOpenPos]] = dir;
                classes[seq[pair.SeqClosePos]] = dir;

                for (int m = pair.SeqOpenPos + 1; m < len && initialClasses[seq[m]] == BidiClass.NSM; m++)
                {
                    classes[seq[m]] = dir;
                }

                for (int m = pair.SeqClosePos + 1; m < len && initialClasses[seq[m]] == BidiClass.NSM; m++)
                {
                    classes[seq[m]] = dir;
                }
            }
        }
    }

    private static void ResolveImplicitLevels(BidiClass[] classes, byte[] levels)
    {
        for (int i = 0; i < classes.Length; i++)
        {
            if (IsRemovedByX9(classes[i]))
            {
                continue;
            }

            byte level = levels[i];
            BidiClass c = classes[i];

            if ((level & 1) == 0)
            {
                if (c == BidiClass.R)
                {
                    levels[i] = (byte)(level + 1);
                }
                else if (c is BidiClass.AN or BidiClass.EN)
                {
                    levels[i] = (byte)(level + 2);
                }
            }
            else
            {
                if (c is BidiClass.L or BidiClass.EN or BidiClass.AN)
                {
                    levels[i] = (byte)(level + 1);
                }
            }
        }
    }

    private static bool IsNeutralOrIsolate(BidiClass cls)
    {
        return cls is BidiClass.B
            or BidiClass.S
            or BidiClass.WS
            or BidiClass.ON
            or BidiClass.FSI
            or BidiClass.LRI
            or BidiClass.RLI
            or BidiClass.PDI;
    }

    private static BidiClass NormalizeStrongForNeutral(BidiClass cls)
    {
        return cls is BidiClass.R or BidiClass.EN or BidiClass.AN ? BidiClass.R : BidiClass.L;
    }

    private static BidiClass NormalizeStrongOrNone(BidiClass cls)
    {
        return cls switch
        {
            BidiClass.L => BidiClass.L,
            BidiClass.R or BidiClass.EN or BidiClass.AN => BidiClass.R,
            _ => BidiClass.ON
        };
    }

    private static int NormalizeCanonicalBracket(int cp)
    {
        return cp switch
        {
            0x3009 => 0x232A,
            0x232A => 0x232A,
            _ => cp
        };
    }

    internal static BidiClass ClassifyCluster(string text, TextRange cluster)
    {
        if (!Rune.TryGetRuneAt(text, cluster.Start, out Rune rune))
        {
            return BidiClass.ON;
        }

        return UnicodeDataTables.GetBidiClass(rune.Value);
    }
}
