using System.Collections.Immutable;
using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Represents a positioned HarfBuzz-shaped <see cref="SKTextBlob"/> run ready to be drawn onto an <see cref="SKCanvas"/>.
/// </summary>
/// <param name="Blob">The positioned SkiaSharp text blob containing HarfBuzz glyph indices and coordinates.</param>
/// <param name="OriginX">The horizontal offset relative to the layout's top-left origin.</param>
/// <param name="BaselineY">The vertical baseline offset relative to the layout's top-left origin.</param>
public readonly record struct ShapedTextBlobRun(SKTextBlob Blob, float OriginX, float BaselineY);

/// <summary>
/// Represents an immutable, HarfBuzz-shaped multi-line text layout implementing <see cref="ITextLayout"/>
/// for SkiaSharp rendering, character/caret hit-testing, word selection, and multi-line BiDi highlight geometry.
/// </summary>
public sealed class SkiaTextLayout : ITextLayout, IDisposable
{
    private readonly int[] _characterBoundaries;
    private readonly ImmutableArray<TextRange> _clusters;
    private readonly ImmutableArray<ShapedTextBlobRun> _blobRuns;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaTextLayout"/> class.
    /// </summary>
    public SkiaTextLayout(
        string text,
        Size size,
        ImmutableArray<TextLine> lines,
        ImmutableArray<ShapedTextBlobRun> blobRuns,
        int[]? characterBoundaries = null,
        ImmutableArray<TextRange> clusters = default)
    {
        Text = text ?? string.Empty;
        Size = size;
        Lines = lines.IsDefault ? ImmutableArray<TextLine>.Empty : lines;
        _blobRuns = blobRuns.IsDefault ? ImmutableArray<ShapedTextBlobRun>.Empty : blobRuns;
        _characterBoundaries = characterBoundaries ?? UnicodeTextSegmentation.GetCharacterBoundaries(Text);
        _clusters = clusters.IsDefault
            ? UnicodeTextSegmentation.GetGraphemeClusters(Text, _characterBoundaries, 0, Text.Length)
            : clusters;
    }

    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public Size Size { get; }

    /// <inheritdoc />
    public ImmutableArray<TextLine> Lines { get; }

    /// <summary>
    /// Gets the positioned HarfBuzz <see cref="SKTextBlob"/> runs for this layout.
    /// </summary>
    public ImmutableArray<ShapedTextBlobRun> BlobRuns => _blobRuns;

    /// <inheritdoc />
    public TextHitTestResult HitTestPoint(Point localPoint)
    {
        if (string.IsNullOrEmpty(Text) || Lines.IsEmpty)
        {
            return new TextHitTestResult(0, TextRange.Empty, IsInside: false, IsTrailingEdge: false);
        }

        TextLine targetLine = Lines[0];
        bool verticallyInside = false;

        if (localPoint.Y < Lines[0].Bounds.Y)
        {
            targetLine = Lines[0];
        }
        else if (localPoint.Y > Lines[^1].Bounds.Bottom)
        {
            targetLine = Lines[^1];
        }
        else
        {
            verticallyInside = true;
            foreach (TextLine line in Lines)
            {
                if (localPoint.Y >= line.Bounds.Y && localPoint.Y <= line.Bounds.Bottom)
                {
                    targetLine = line;
                    break;
                }
            }
        }

        ImmutableArray<TextClusterMetrics> clusters = targetLine.Clusters;
        if (clusters.IsEmpty)
        {
            TextRange emptyRange = new(targetLine.StartTextIndex, 0);
            return new TextHitTestResult(targetLine.StartTextIndex, emptyRange, verticallyInside, IsTrailingEdge: false);
        }

        TextClusterMetrics firstVisual = clusters[0];
        TextClusterMetrics lastVisual = clusters[^1];

        if (localPoint.X < firstVisual.Left)
        {
            bool trailing = firstVisual.IsRightToLeft;
            int caret = trailing ? firstVisual.EndIndex : firstVisual.StartIndex;
            return new TextHitTestResult(caret, firstVisual.Range, IsInside: false, IsTrailingEdge: trailing);
        }

        if (localPoint.X > lastVisual.Right)
        {
            bool trailing = !lastVisual.IsRightToLeft;
            int caret = trailing ? lastVisual.EndIndex : lastVisual.StartIndex;
            return new TextHitTestResult(caret, lastVisual.Range, IsInside: false, IsTrailingEdge: trailing);
        }

        TextClusterMetrics hitCluster = firstVisual;
        double minDistance = double.MaxValue;
        bool horizontallyInside = false;

        foreach (TextClusterMetrics cluster in clusters)
        {
            if (localPoint.X >= cluster.Left && localPoint.X <= cluster.Right)
            {
                hitCluster = cluster;
                horizontallyInside = true;
                break;
            }

            double distance = localPoint.X < cluster.Left
                ? cluster.Left - localPoint.X
                : localPoint.X - cluster.Right;
            if (distance < minDistance)
            {
                minDistance = distance;
                hitCluster = cluster;
            }
        }

        bool isVisualRightHalf = localPoint.X >= hitCluster.Midpoint;
        bool isTrailingEdge = hitCluster.IsRightToLeft ? !isVisualRightHalf : isVisualRightHalf;
        int resolvedPosition = isTrailingEdge ? hitCluster.EndIndex : hitCluster.StartIndex;

        return new TextHitTestResult(
            resolvedPosition,
            hitCluster.Range,
            verticallyInside && horizontallyInside,
            isTrailingEdge);
    }

    /// <inheritdoc />
    public ImmutableArray<Rect> HitTestTextRange(int startIndex, int length)
    {
        if (length <= 0 || string.IsNullOrEmpty(Text) || Lines.IsEmpty)
        {
            return ImmutableArray<Rect>.Empty;
        }

        TextRange normalized = UnicodeTextSegmentation.NormalizeRangeToGraphemeBoundaries(
            Text,
            _characterBoundaries,
            startIndex,
            length);
        if (normalized.IsEmpty)
        {
            return ImmutableArray<Rect>.Empty;
        }

        int rangeStart = normalized.Start;
        int rangeEnd = normalized.End;
        ImmutableArray<Rect>.Builder rects = ImmutableArray.CreateBuilder<Rect>();

        foreach (TextLine line in Lines)
        {
            if (line.EndTextIndex <= rangeStart || line.StartTextIndex >= rangeEnd)
            {
                continue;
            }

            double? activeLeft = null;
            double? activeRight = null;

            foreach (TextClusterMetrics cluster in line.Clusters)
            {
                bool isSelected = cluster.EndIndex > rangeStart && cluster.StartIndex < rangeEnd;
                if (isSelected)
                {
                    if (activeLeft is null)
                    {
                        activeLeft = cluster.Left;
                        activeRight = cluster.Right;
                    }
                    else if (Math.Abs(cluster.Left - activeRight!.Value) <= 1.5)
                    {
                        activeRight = Math.Max(activeRight.Value, cluster.Right);
                    }
                    else
                    {
                        rects.Add(new Rect(
                            activeLeft.Value,
                            line.SelectionBounds.Y,
                            Math.Max(1.0, activeRight.Value - activeLeft.Value),
                            line.SelectionBounds.Height));
                        activeLeft = cluster.Left;
                        activeRight = cluster.Right;
                    }
                }
                else if (activeLeft is not null && activeRight is not null)
                {
                    rects.Add(new Rect(
                        activeLeft.Value,
                        line.SelectionBounds.Y,
                        Math.Max(1.0, activeRight.Value - activeLeft.Value),
                        line.SelectionBounds.Height));
                    activeLeft = null;
                    activeRight = null;
                }
            }

            if (activeLeft is not null && activeRight is not null)
            {
                rects.Add(new Rect(
                    activeLeft.Value,
                    line.SelectionBounds.Y,
                    Math.Max(1.0, activeRight.Value - activeLeft.Value),
                    line.SelectionBounds.Height));
            }
        }

        return rects.ToImmutable();
    }

    /// <inheritdoc />
    public TextRange GetWordRangeAt(int textIndex)
    {
        return UnicodeTextSegmentation.GetWordRangeAt(Text, _clusters, textIndex);
    }

    /// <summary>
    /// Draws the HarfBuzz-shaped <see cref="SKTextBlob"/> runs onto <paramref name="canvas"/> at <paramref name="origin"/>.
    /// </summary>
    public void Draw(SKCanvas canvas, Point origin, Color color, double opacity = 1.0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);

        if (_blobRuns.IsEmpty)
        {
            return;
        }

        using SKPaint paint = new()
        {
            Color = color.ToSKColor(opacity),
            IsAntialias = true
        };

        foreach (ShapedTextBlobRun run in _blobRuns)
        {
            canvas.DrawText(
                run.Blob,
                (float)(origin.X + run.OriginX),
                (float)(origin.Y + run.BaselineY),
                paint);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (ShapedTextBlobRun run in _blobRuns)
        {
            run.Blob.Dispose();
        }
    }
}
