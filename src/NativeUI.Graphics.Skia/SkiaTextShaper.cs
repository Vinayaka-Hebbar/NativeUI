using System.Collections.Immutable;
using System.Text;
using HarfBuzzSharp;
using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Shapes Unicode text runs using <see cref="HarfBuzzSharp"/> and constructs <see cref="SkiaTextLayout"/> instances
/// with grapheme cluster metrics, font fallback, bidirectional visual run ordering, word wrapping, and positioned <see cref="SKTextBlob"/> runs.
/// </summary>
public sealed class SkiaTextShaper : ITextMeasurer
{
    private const float HarfBuzzSubpixelScale = 64.0f;
    private readonly SkiaResourceCache _resourceCache;

    /// <summary>
    /// Gets the shared singleton instance of <see cref="SkiaTextShaper"/>.
    /// </summary>
    public static SkiaTextShaper Shared { get; } = new(SkiaResourceCache.Shared);

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaTextShaper"/> class.
    /// </summary>
    public SkiaTextShaper(SkiaResourceCache resourceCache)
    {
        ArgumentNullException.ThrowIfNull(resourceCache);
        _resourceCache = resourceCache;
    }

    /// <inheritdoc />
    public Size MeasureText(string text, Size availableSize, ComputedStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        using SkiaTextLayout layout = ShapeInternal(text, availableSize, style, createBlobs: false);
        return layout.Size;
    }

    /// <inheritdoc />
    public ITextLayout CreateTextLayout(string text, Size availableSize, ComputedStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ShapeInternal(text, availableSize, style, createBlobs: true);
    }

    /// <summary>
    /// Shapes and wraps <paramref name="text"/> within <paramref name="availableSize"/> using <paramref name="style"/>.
    /// </summary>
    public SkiaTextLayout ShapeText(string text, Size availableSize, ComputedStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ShapeInternal(text, availableSize, style, createBlobs: true);
    }

    private SkiaTextLayout ShapeInternal(
        string text,
        Size availableSize,
        ComputedStyle style,
        bool createBlobs)
    {
        double fontSize = style.FontSize > 0 ? style.FontSize : 14.0;
        SKTypeface primaryTypeface = _resourceCache.GetTypeface(style.FontFamily, style.FontWeight);
        using SKFont primarySkFont = CreateSkiaFont(primaryTypeface, fontSize);

        primarySkFont.GetFontMetrics(out SKFontMetrics metrics);
        double ascent = -metrics.Ascent;
        double descent = metrics.Descent;
        double lineHeight = Math.Max(fontSize * 1.25, ascent + descent + Math.Max(0, metrics.Leading));
        double halfLeading = (lineHeight - ascent - descent) / 2;

        if (string.IsNullOrEmpty(text))
        {
            return new SkiaTextLayout(
                string.Empty,
                new Size(0, lineHeight),
                ImmutableArray<TextLine>.Empty,
                ImmutableArray<ShapedTextBlobRun>.Empty,
                [],
                ImmutableArray<TextRange>.Empty);
        }

        int[] allBoundaries = UnicodeTextSegmentation.GetCharacterBoundaries(text);
        ImmutableArray<TextRange> allClusters = UnicodeTextSegmentation.GetGraphemeClusters(
            text,
            allBoundaries,
            0,
            text.Length);

        double maxWidth = double.IsNaN(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0.0, availableSize.Width);

        ImmutableArray<TextLine>.Builder linesBuilder = ImmutableArray.CreateBuilder<TextLine>();
        ImmutableArray<ShapedTextBlobRun>.Builder blobsBuilder = ImmutableArray.CreateBuilder<ShapedTextBlobRun>();

        double currentY = 0;
        double maxLineWidth = 0;
        int lineIndex = 0;

        List<TextRange> paragraphs = SplitParagraphs(text);
        foreach (TextRange paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                double emptyBaselineY = currentY + halfLeading + ascent;
                linesBuilder.Add(new TextLine(
                    lineIndex++,
                    paragraph.Start,
                    0,
                    new Rect(0, currentY, 0, lineHeight),
                    emptyBaselineY,
                    isRightToLeft: false,
                    ImmutableArray<TextClusterMetrics>.Empty,
                    new Rect(0, currentY + halfLeading, 0, ascent + descent)));
                currentY += lineHeight;
                continue;
            }

            ImmutableArray<TextRange> paraClusters = SliceClusters(allClusters, paragraph);
            BidiAlgorithm.ParagraphBidiData bidiData = BidiAlgorithm.AnalyzeParagraph(text, paragraph.Start, paraClusters);

            List<TextRange> wrappedLineRanges = WrapParagraphLines(
                text,
                paragraph,
                paraClusters,
                in bidiData,
                primaryTypeface,
                style.FontWeight,
                fontSize,
                maxWidth);

            foreach (TextRange lineRange in wrappedLineRanges)
            {
                double baselineY = currentY + halfLeading + ascent;
                TextLine shapedLine = BuildShapedLine(
                    text,
                    allBoundaries,
                    in bidiData,
                    lineRange,
                    lineIndex++,
                    currentY,
                    baselineY,
                    lineHeight,
                    ascent,
                    descent,
                    maxWidth,
                    style.TextAlign,
                    primaryTypeface,
                    style.FontWeight,
                    fontSize,
                    createBlobs,
                    blobsBuilder);

                linesBuilder.Add(shapedLine);
                maxLineWidth = Math.Max(maxLineWidth, shapedLine.Bounds.Width);
                currentY += lineHeight;
            }
        }

        return new SkiaTextLayout(
            text,
            new Size(maxLineWidth, Math.Max(lineHeight, currentY)),
            linesBuilder.ToImmutable(),
            blobsBuilder.ToImmutable(),
            allBoundaries,
            allClusters);
    }

    private static ImmutableArray<TextRange> SliceClusters(ImmutableArray<TextRange> allClusters, TextRange slice)
    {
        if (allClusters.IsEmpty || slice.IsEmpty)
        {
            return ImmutableArray<TextRange>.Empty;
        }

        ImmutableArray<TextRange>.Builder builder = ImmutableArray.CreateBuilder<TextRange>();
        foreach (TextRange c in allClusters)
        {
            if (c.End <= slice.Start)
            {
                continue;
            }

            if (c.Start >= slice.End)
            {
                break;
            }

            builder.Add(c);
        }

        return builder.ToImmutable();
    }

    private static List<TextRange> SplitParagraphs(string text)
    {
        List<TextRange> paragraphs = [];
        int start = 0;
        int i = 0;

        while (i < text.Length)
        {
            char ch = text[i];
            if (ch is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
            {
                paragraphs.Add(new TextRange(start, i - start));
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i += 2;
                }
                else
                {
                    i++;
                }

                start = i;
            }
            else
            {
                i++;
            }
        }

        if (start <= text.Length)
        {
            if (start < text.Length || paragraphs.Count == 0 || text[^1] is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
            {
                paragraphs.Add(new TextRange(start, text.Length - start));
            }
        }

        return paragraphs;
    }

    private List<TextRange> WrapParagraphLines(
        string text,
        TextRange paragraph,
        ImmutableArray<TextRange> clusters,
        in BidiAlgorithm.ParagraphBidiData bidiData,
        SKTypeface primaryTypeface,
        FontWeight fontWeight,
        double fontSize,
        double maxWidth)
    {
        if (!double.IsFinite(maxWidth) || clusters.Length <= 1)
        {
            return [paragraph];
        }

        double[] clusterAdvances = MeasureClusterAdvances(
            text,
            clusters,
            in bidiData,
            primaryTypeface,
            fontWeight,
            fontSize);

        ImmutableArray<LineBreakOpportunity> opportunities = LineBreakEnumerator.GetBreakOpportunities(text, clusters);
        HashSet<int> validBreakPositions = new(opportunities.Length);
        foreach (LineBreakOpportunity opp in opportunities)
        {
            validBreakPositions.Add(opp.TextPosition);
        }

        List<TextRange> lines = [];
        int lineStartCluster = 0;

        while (lineStartCluster < clusters.Length)
        {
            double accumulatedWidth = 0;
            int lastCandidateEndCluster = -1;
            int currentCluster = lineStartCluster;

            while (currentCluster < clusters.Length)
            {
                double nextWidth = accumulatedWidth + clusterAdvances[currentCluster];
                bool isTrailingSpace = char.IsWhiteSpace(text, clusters[currentCluster].Start);

                if (currentCluster > lineStartCluster && !isTrailingSpace && nextWidth > maxWidth)
                {
                    break;
                }

                accumulatedWidth = nextWidth;
                int clusterEndTextIdx = clusters[currentCluster].End;
                if (validBreakPositions.Contains(clusterEndTextIdx))
                {
                    lastCandidateEndCluster = currentCluster + 1;
                }

                currentCluster++;
            }

            if (currentCluster >= clusters.Length)
            {
                int startIdx = clusters[lineStartCluster].Start;
                int endIdx = clusters[^1].End;
                lines.Add(new TextRange(startIdx, endIdx - startIdx));
                break;
            }

            int breakEndCluster = lastCandidateEndCluster > lineStartCluster
                ? lastCandidateEndCluster
                : Math.Max(lineStartCluster + 1, currentCluster);

            int lineStartText = clusters[lineStartCluster].Start;
            int lineEndText = clusters[breakEndCluster - 1].End;
            lines.Add(new TextRange(lineStartText, lineEndText - lineStartText));

            lineStartCluster = breakEndCluster;
        }

        return lines;
    }

    private double[] MeasureClusterAdvances(
        string text,
        ImmutableArray<TextRange> clusters,
        in BidiAlgorithm.ParagraphBidiData bidiData,
        SKTypeface primaryTypeface,
        FontWeight fontWeight,
        double fontSize)
    {
        double[] advances = new double[clusters.Length];
        ImmutableArray<BiDiTextRun> runs = BidiAlgorithm.BuildLogicalRuns(in bidiData);

        int clusterCursor = 0;
        foreach (BiDiTextRun run in runs)
        {
            while (clusterCursor < clusters.Length && clusters[clusterCursor].Start < run.StartIndex)
            {
                clusterCursor++;
            }

            int runStartCluster = clusterCursor;
            while (clusterCursor < clusters.Length && clusters[clusterCursor].End <= run.EndIndex)
            {
                clusterCursor++;
            }

            int runClusterCount = clusterCursor - runStartCluster;
            if (runClusterCount <= 0)
            {
                continue;
            }

            ReadOnlySpan<TextRange> runClusters = clusters.AsSpan(runStartCluster, runClusterCount);
            List<FontSubRun> fontSubRuns = PartitionIntoFontSubRuns(text, run, runClusters, primaryTypeface, fontWeight);

            int localOffset = 0;
            foreach (FontSubRun subRun in fontSubRuns)
            {
                ReadOnlySpan<TextRange> subClusters = runClusters.Slice(localOffset, subRun.ClusterCount);
                ShapedSubRunResult shaped = ShapeFontSubRun(
                    text,
                    subRun,
                    subClusters,
                    fontSize,
                    createBlob: false);

                for (int i = 0; i < subRun.ClusterCount; i++)
                {
                    advances[runStartCluster + localOffset + i] = shaped.LogicalClusterWidths[i];
                }

                localOffset += subRun.ClusterCount;
            }
        }

        return advances;
    }

    private TextLine BuildShapedLine(
        string text,
        int[] allBoundaries,
        in BidiAlgorithm.ParagraphBidiData bidiData,
        TextRange lineRange,
        int lineIndex,
        double lineY,
        double baselineY,
        double lineHeight,
        double ascent,
        double descent,
        double maxWidth,
        TextAlign textAlign,
        SKTypeface primaryTypeface,
        FontWeight fontWeight,
        double fontSize,
        bool createBlobs,
        ImmutableArray<ShapedTextBlobRun>.Builder blobsBuilder)
    {
        ImmutableArray<BiDiTextRun> visualRuns = BidiAlgorithm.OrderLineVisualRuns(
            in bidiData,
            lineRange.Start,
            lineRange.Length);

        bool baseIsRtl = bidiData.BaseIsRightToLeft;
        List<ShapedSubRunResult> shapedVisualSubRuns = [];
        double totalVisualWidth = 0;

        foreach (BiDiTextRun run in visualRuns)
        {
            ImmutableArray<TextRange> runClusters = UnicodeTextSegmentation.GetGraphemeClusters(
                text,
                allBoundaries,
                run.StartIndex,
                run.Length);

            List<FontSubRun> fontSubRuns = PartitionIntoFontSubRuns(
                text,
                run,
                runClusters.AsSpan(),
                primaryTypeface,
                fontWeight);

            if (run.IsRightToLeft && fontSubRuns.Count > 1)
            {
                fontSubRuns.Reverse();
            }

            foreach (FontSubRun subRun in fontSubRuns)
            {
                ReadOnlySpan<TextRange> subClusters = runClusters.AsSpan(subRun.StartClusterOffset, subRun.ClusterCount);
                ShapedSubRunResult shaped = ShapeFontSubRun(
                    text,
                    subRun,
                    subClusters,
                    fontSize,
                    createBlobs);

                shapedVisualSubRuns.Add(shaped);
                totalVisualWidth += shaped.TotalAdvance;
            }
        }

        double alignmentOffsetX = 0;
        if (double.IsFinite(maxWidth) && maxWidth > totalVisualWidth)
        {
            TextAlign effectiveAlign = textAlign == TextAlign.Left && baseIsRtl ? TextAlign.Right : textAlign;
            alignmentOffsetX = effectiveAlign switch
            {
                TextAlign.Center => (maxWidth - totalVisualWidth) * 0.5,
                TextAlign.Right => maxWidth - totalVisualWidth,
                _ => 0
            };
        }

        ImmutableArray<TextClusterMetrics>.Builder lineClusters = ImmutableArray.CreateBuilder<TextClusterMetrics>();
        double currentVisualX = alignmentOffsetX;

        foreach (ShapedSubRunResult shaped in shapedVisualSubRuns)
        {
            if (shaped.Blob is not null)
            {
                blobsBuilder.Add(new ShapedTextBlobRun(shaped.Blob, (float)currentVisualX, (float)baselineY));
            }

            foreach (TextClusterMetrics cluster in shaped.VisualClusters)
            {
                lineClusters.Add(new TextClusterMetrics(
                    cluster.StartIndex,
                    cluster.Length,
                    currentVisualX + cluster.Left,
                    currentVisualX + cluster.Right,
                    cluster.IsRightToLeft));
            }

            currentVisualX += shaped.TotalAdvance;
        }

        Rect lineBounds = new(alignmentOffsetX, lineY, totalVisualWidth, lineHeight);
        return new TextLine(
            lineIndex,
            lineRange.Start,
            lineRange.Length,
            lineBounds,
            baselineY,
            baseIsRtl,
            lineClusters.ToImmutable(),
            new Rect(alignmentOffsetX, baselineY - ascent, totalVisualWidth, ascent + descent));
    }

    private readonly record struct FontSubRun(
        int StartIndex,
        int Length,
        int StartClusterOffset,
        int ClusterCount,
        bool IsRightToLeft,
        SKTypeface Typeface);

    private List<FontSubRun> PartitionIntoFontSubRuns(
        string text,
        BiDiTextRun run,
        ReadOnlySpan<TextRange> runClusters,
        SKTypeface primaryTypeface,
        FontWeight fontWeight)
    {
        List<FontSubRun> subRuns = [];
        if (runClusters.IsEmpty)
        {
            return subRuns;
        }

        SKTypeface currentTypeface = ResolveClusterTypeface(text, runClusters[0], primaryTypeface, fontWeight);
        int subStartCluster = 0;

        for (int i = 1; i < runClusters.Length; i++)
        {
            SKTypeface tf = ResolveClusterTypeface(text, runClusters[i], currentTypeface, fontWeight);
            if (!ReferenceEquals(tf, currentTypeface))
            {
                int textStart = runClusters[subStartCluster].Start;
                int textEnd = runClusters[i - 1].End;
                subRuns.Add(new FontSubRun(
                    textStart,
                    textEnd - textStart,
                    subStartCluster,
                    i - subStartCluster,
                    run.IsRightToLeft,
                    currentTypeface));

                currentTypeface = tf;
                subStartCluster = i;
            }
        }

        int finalStart = runClusters[subStartCluster].Start;
        int finalEnd = runClusters[^1].End;
        subRuns.Add(new FontSubRun(
            finalStart,
            finalEnd - finalStart,
            subStartCluster,
            runClusters.Length - subStartCluster,
            run.IsRightToLeft,
            currentTypeface));

        return subRuns;
    }

    private SKTypeface ResolveClusterTypeface(
        string text,
        TextRange cluster,
        SKTypeface currentOrPrimaryTypeface,
        FontWeight fontWeight)
    {
        if (!Rune.TryGetRuneAt(text, cluster.Start, out Rune rune) || Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
        {
            return currentOrPrimaryTypeface;
        }

        return _resourceCache.ResolveTypefaceForCodePoint(currentOrPrimaryTypeface, rune.Value, fontWeight);
    }

    private ShapedSubRunResult ShapeFontSubRun(
        string text,
        FontSubRun subRun,
        ReadOnlySpan<TextRange> logicalClusters,
        double fontSize,
        bool createBlob)
    {
        if (logicalClusters.IsEmpty || subRun.Length <= 0)
        {
            return new ShapedSubRunResult(null, [], [], 0);
        }

        using SKFont skFont = CreateSkiaFont(subRun.Typeface, fontSize);
        Face? hbFace = _resourceCache.GetHarfBuzzFace(subRun.Typeface);

        if (hbFace is not null)
        {
            using Font hbFont = CreateHarfBuzzFont(hbFace, fontSize);
            using Buffer buffer = new();
            buffer.AddUtf16(text.AsSpan(subRun.StartIndex, subRun.Length));
            buffer.GuessSegmentProperties();
            buffer.Direction = subRun.IsRightToLeft ? Direction.RightToLeft : Direction.LeftToRight;

            hbFont.Shape(buffer);

            int glyphCount = buffer.Length;
            if (glyphCount > 0)
            {
                GlyphInfo[] infos = buffer.GlyphInfos;
                GlyphPosition[] positions = buffer.GlyphPositions;

                SKTextBlob? blob = null;
                double penX = 0;

                if (createBlob)
                {
                    using SKTextBlobBuilder blobBuilder = new();
                    SKPositionedRunBuffer runBuffer = blobBuilder.AllocatePositionedRun(skFont, glyphCount);
                    Span<ushort> glyphSpan = runBuffer.Glyphs;
                    Span<SKPoint> posSpan = runBuffer.Positions;

                    for (int i = 0; i < glyphCount; i++)
                    {
                        glyphSpan[i] = (ushort)infos[i].Codepoint;
                        float xOff = positions[i].XOffset / HarfBuzzSubpixelScale;
                        float yOff = -(positions[i].YOffset / HarfBuzzSubpixelScale);
                        float xAdv = positions[i].XAdvance / HarfBuzzSubpixelScale;

                        posSpan[i] = new SKPoint((float)penX + xOff, yOff);
                        penX += xAdv;
                    }

                    blob = blobBuilder.Build();
                }
                else
                {
                    for (int i = 0; i < glyphCount; i++)
                    {
                        penX += positions[i].XAdvance / HarfBuzzSubpixelScale;
                    }
                }

                double[] logicalWidths = ComputeLogicalClusterWidthsFromHarfBuzz(
                    text,
                    subRun,
                    logicalClusters,
                    skFont,
                    infos,
                    positions);

                TextClusterMetrics[] visualClusters = BuildVisualClusterMetrics(
                    logicalClusters,
                    logicalWidths,
                    subRun.IsRightToLeft);

                return new ShapedSubRunResult(blob, logicalWidths, visualClusters, penX);
            }
        }

        return ShapeFallbackSubRun(text, subRun, logicalClusters, skFont, createBlob);
    }

    private static double[] ComputeLogicalClusterWidthsFromHarfBuzz(
        string text,
        FontSubRun subRun,
        ReadOnlySpan<TextRange> logicalClusters,
        SKFont skFont,
        GlyphInfo[] infos,
        GlyphPosition[] positions)
    {
        int clusterCount = logicalClusters.Length;
        double[] clusterWidths = new double[clusterCount];

        int[] glyphToClusterIdx = new int[infos.Length];
        for (int g = 0; g < infos.Length; g++)
        {
            int absoluteIndex = subRun.StartIndex + (int)infos[g].Cluster;
            int foundIdx = 0;
            for (int c = 0; c < clusterCount; c++)
            {
                if (absoluteIndex >= logicalClusters[c].Start && absoluteIndex < logicalClusters[c].End)
                {
                    foundIdx = c;
                    break;
                }

                if (absoluteIndex >= logicalClusters[c].End)
                {
                    foundIdx = c;
                }
            }

            glyphToClusterIdx[g] = foundIdx;
        }

        int i = 0;
        while (i < infos.Length)
        {
            int groupClusterIdx = glyphToClusterIdx[i];
            double groupAdvance = positions[i].XAdvance / HarfBuzzSubpixelScale;
            int j = i + 1;
            while (j < infos.Length && glyphToClusterIdx[j] == groupClusterIdx)
            {
                groupAdvance += positions[j].XAdvance / HarfBuzzSubpixelScale;
                j++;
            }

            int nextClusterIdx = !subRun.IsRightToLeft
                ? (j < infos.Length ? Math.Max(groupClusterIdx + 1, glyphToClusterIdx[j]) : clusterCount)
                : (i > 0 ? Math.Max(groupClusterIdx + 1, glyphToClusterIdx[i - 1]) : clusterCount);

            int endClusterIdx = Math.Min(clusterCount, nextClusterIdx);
            int coveredCount = Math.Max(1, endClusterIdx - groupClusterIdx);

            if (coveredCount == 1)
            {
                clusterWidths[groupClusterIdx] += groupAdvance;
            }
            else
            {
                // Weight multi-cluster ligature advances proportionally by each cluster's unligated glyph width
                double totalUnligated = 0;
                double[] unligated = new double[coveredCount];
                for (int k = 0; k < coveredCount; k++)
                {
                    TextRange cr = logicalClusters[groupClusterIdx + k];
                    double w = Math.Max(0.1, skFont.MeasureText(text.AsSpan(cr.Start, cr.Length)));
                    unligated[k] = w;
                    totalUnligated += w;
                }

                for (int k = 0; k < coveredCount; k++)
                {
                    clusterWidths[groupClusterIdx + k] += groupAdvance * (unligated[k] / totalUnligated);
                }
            }

            i = j;
        }

        return clusterWidths;
    }

    private static TextClusterMetrics[] BuildVisualClusterMetrics(
        ReadOnlySpan<TextRange> logicalClusters,
        double[] logicalWidths,
        bool isRightToLeft)
    {
        int count = logicalClusters.Length;
        TextClusterMetrics[] visual = new TextClusterMetrics[count];
        double x = 0;

        if (!isRightToLeft)
        {
            for (int i = 0; i < count; i++)
            {
                double w = logicalWidths[i];
                visual[i] = new TextClusterMetrics(
                    logicalClusters[i].Start,
                    logicalClusters[i].Length,
                    x,
                    x + w,
                    IsRightToLeft: false);
                x += w;
            }
        }
        else
        {
            for (int i = count - 1; i >= 0; i--)
            {
                double w = logicalWidths[i];
                int visualIdx = count - 1 - i;
                visual[visualIdx] = new TextClusterMetrics(
                    logicalClusters[i].Start,
                    logicalClusters[i].Length,
                    x,
                    x + w,
                    IsRightToLeft: true);
                x += w;
            }
        }

        return visual;
    }

    private static ShapedSubRunResult ShapeFallbackSubRun(
        string text,
        FontSubRun subRun,
        ReadOnlySpan<TextRange> logicalClusters,
        SKFont skFont,
        bool createBlob)
    {
        int count = logicalClusters.Length;
        double[] logicalWidths = new double[count];

        for (int i = 0; i < count; i++)
        {
            logicalWidths[i] = skFont.MeasureText(text.AsSpan(logicalClusters[i].Start, logicalClusters[i].Length));
        }

        TextClusterMetrics[] visualClusters = BuildVisualClusterMetrics(logicalClusters, logicalWidths, subRun.IsRightToLeft);
        double penX = 0;
        SKTextBlob? blob = null;

        if (createBlob)
        {
            List<ushort> allGlyphs = [];
            List<SKPoint> allPositions = [];

            for (int v = 0; v < count; v++)
            {
                TextClusterMetrics vc = visualClusters[v];
                ushort[] glyphs = skFont.GetGlyphs(text.AsSpan(vc.StartIndex, vc.Length));
                if (glyphs.Length > 0)
                {
                    allGlyphs.Add(glyphs[0]);
                    allPositions.Add(new SKPoint((float)penX, 0));
                }

                penX += vc.Width;
            }

            if (allGlyphs.Count > 0)
            {
                using SKTextBlobBuilder blobBuilder = new();
                SKPositionedRunBuffer runBuffer = blobBuilder.AllocatePositionedRun(skFont, allGlyphs.Count);
                allGlyphs.CopyTo(runBuffer.Glyphs);
                allPositions.CopyTo(runBuffer.Positions);
                blob = blobBuilder.Build();
            }
        }
        else
        {
            for (int v = 0; v < count; v++)
            {
                penX += visualClusters[v].Width;
            }
        }

        return new ShapedSubRunResult(blob, logicalWidths, visualClusters, penX);
    }

    private static SKFont CreateSkiaFont(SKTypeface typeface, double fontSize)
    {
        return new SKFont(typeface, (float)fontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true
        };
    }

    private static Font CreateHarfBuzzFont(Face face, double fontSize)
    {
        int scaledSize = Math.Max(64, (int)Math.Round(fontSize * HarfBuzzSubpixelScale));
        Font font = new(face);
        font.SetScale(scaledSize, scaledSize);
        font.SetFunctionsOpenType();
        return font;
    }

    private readonly record struct ShapedSubRunResult(
        SKTextBlob? Blob,
        double[] LogicalClusterWidths,
        TextClusterMetrics[] VisualClusters,
        double TotalAdvance);
}
