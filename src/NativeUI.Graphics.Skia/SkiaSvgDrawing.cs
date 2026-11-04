using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

internal static class SkiaSvgDrawing
{
    private sealed class PictureResource(SKPicture picture) : IDisposable
    {
        internal SKPicture Picture { get; } = picture;
        public void Dispose() => Picture.Dispose();
    }

    public static void Draw(SkiaDrawingContext context, SvgNode definition, Rect viewport, double opacity)
    {
        PictureResource resource = context.Resources.GetOrCreate(definition, () =>
        {
            using SKPictureRecorder recorder = new();
            SKCanvas canvas = recorder.BeginRecording(ToRect(viewport));
            Dictionary<string, SvgNode> definitions = new(StringComparer.Ordinal);
            void Index(SvgNode node)
            {
                if (node.Id is string id) definitions[id] = node;
                foreach (SvgNode child in node.Children) Index(child);
            }
            Index(definition);
            DrawNode(context, canvas, definition, definitions, new Style(), viewport, 0, false);
            return new PictureResource(recorder.EndRecording());
        });
        using SKPaint paint = new() { Color = SKColors.White.WithAlpha(Alpha(opacity)) };
        context.Canvas.DrawPicture(resource.Picture, paint);
    }

    private static void DrawNode(SkiaDrawingContext context, SKCanvas canvas, SvgNode node, Dictionary<string, SvgNode> definitions, Style inherited, Rect viewport, int depth, bool definitionContent)
    {
        if (depth > 64) throw new InvalidDataException("SVG nesting exceeds the supported limit.");
        if (!definitionContent && node.Kind is SvgNodeKind.Definitions or SvgNodeKind.LinearGradient or SvgNodeKind.RadialGradient or SvgNodeKind.ClipPath or SvgNodeKind.Mask or SvgNodeKind.Stop or SvgNodeKind.Title or SvgNodeKind.Description) return;
        Style style = inherited.Apply(node);
        if (node.IsDisplayed == false || !style.IsVisible) return;
        canvas.Save();
        try
        {
            if (node.Transform is Matrix3x2 transform)
            {
                SKMatrix matrix = ToMatrix(transform);
                canvas.Concat(in matrix);
            }
            SKRect localBounds = node.Geometry is null ? ToRect(viewport) : CachedPath(context, node.Geometry).TightBounds;
            if (node.ClipPathId is string clipId)
            {
                SvgNode clip = definitions[clipId];
                using SKPath clipPath = new();
                void AddClip(SvgNode child, SKMatrix parent)
                {
                    SKMatrix local = child.Transform is Matrix3x2 childTransform ? SKMatrix.Concat(parent, ToMatrix(childTransform)) : parent;
                    if (child.Geometry is PathGeometry geometry)
                    {
                        using SKPath path = new(CachedPath(context, geometry));
                        path.Transform(local);
                        clipPath.AddPath(path);
                    }
                    foreach (SvgNode descendant in child.Children) AddClip(descendant, local);
                }
                SKMatrix clipMatrix = clip.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox
                    ? SKMatrix.CreateScaleTranslation(localBounds.Width, localBounds.Height, localBounds.Left, localBounds.Top) : SKMatrix.Identity;
                AddClip(clip, clipMatrix);
                clipPath.FillType = clip.ClipRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
            }
            double opacity = node.Opacity ?? 1;
            bool layer = opacity < 1 || node.MaskId is not null;
            if (layer)
            {
                if ((long)canvas.DeviceClipBounds.Width * canvas.DeviceClipBounds.Height > 16_000_000) throw new InvalidDataException("SVG offscreen layer exceeds 16 million pixels.");
                using SKPaint opacityPaint = new() { Color = SKColors.White.WithAlpha(Alpha(opacity)) };
                canvas.SaveLayer(opacityPaint);
            }
            try
            {
                if (node.Kind == SvgNodeKind.Use)
                {
                    string reference = node.Href ?? throw new InvalidDataException("SVG use requires a local reference.");
                    canvas.Translate((float)(node.X?.Resolve(viewport.Width) ?? 0), (float)(node.Y?.Resolve(viewport.Height) ?? 0));
                    DrawNode(context, canvas, definitions[reference], definitions, style, viewport, depth + 1, true);
                }
                else if (node.Geometry is PathGeometry geometry)
                {
                    using SKPath path = new(CachedPath(context, geometry)) { FillType = style.FillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
                    using SKPaint? fill = Paint(style.Fill, style, definitions, localBounds, viewport.Size, false);
                    if (fill is not null) canvas.DrawPath(path, fill);
                    using SKPaint? stroke = Paint(style.Stroke, style, definitions, localBounds, viewport.Size, true);
                    if (stroke is not null) canvas.DrawPath(path, stroke);
                }
                else if (node.Kind is SvgNodeKind.Text or SvgNodeKind.TextSpan)
                {
                    DrawText(context, canvas, node, style, definitions, viewport);
                    return;
                }
                foreach (SvgNode child in node.Children) DrawNode(context, canvas, child, definitions, style, viewport, depth + 1, definitionContent);
                if (node.MaskId is string maskId)
                {
                    SvgNode mask = definitions[maskId];
                    using SKPaint composite = new() { BlendMode = SKBlendMode.DstIn };
                    canvas.SaveLayer(composite);
                    try
                    {
                        using SKPaint maskPaint = new();
                        if (mask.LuminanceMask != false) maskPaint.ColorFilter = SKColorFilter.CreateColorMatrix([0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, .2126f,.7152f,.0722f,0,0]);
                        canvas.SaveLayer(maskPaint);
                        try
                        {
                            if (mask.MaskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
                            {
                                canvas.Translate(localBounds.Left, localBounds.Top);
                                canvas.Scale(localBounds.Width, localBounds.Height);
                            }
                            foreach (SvgNode child in mask.Children) DrawNode(context, canvas, child, definitions, new Style(), viewport, depth + 1, true);
                        }
                        finally { canvas.Restore(); }
                    }
                    finally { canvas.Restore(); }
                }
            }
            finally { if (layer) canvas.Restore(); }
        }
        finally { canvas.Restore(); }
    }

    private static void DrawText(SkiaDrawingContext context, SKCanvas canvas, SvgNode node, Style style, Dictionary<string, SvgNode> definitions, Rect viewport)
    {
        double x = node.X?.Resolve(viewport.Width) ?? 0;
        double y = node.Y?.Resolve(viewport.Height) ?? 0;
        DrawRuns(node, style, ref x, ref y, true);
        void DrawRuns(SvgNode run, Style inherited, ref double cursorX, ref double cursorY, bool isRoot)
        {
            Style runStyle = inherited.Apply(run);
            if (!isRoot && run.X is SvgLength absoluteX) cursorX = absoluteX.Resolve(viewport.Width) ?? 0;
            if (!isRoot && run.Y is SvgLength absoluteY) cursorY = absoluteY.Resolve(viewport.Height) ?? 0;
            cursorX += run.Dx?.Resolve(viewport.Width) ?? 0;
            cursorY += run.Dy?.Resolve(viewport.Height) ?? 0;
            if (run.Text.Length > 0)
            {
                double fontSize = runStyle.FontSize.Resolve(viewport.Height) ?? 16;
                ComputedStyle textStyle = ComputedStyle.Default with { FontSize = fontSize, FontFamily = runStyle.FontFamily, FontWeight = runStyle.FontWeight };
                ITextLayout layout = SkiaTextShaper.Shared.CreateTextLayout(run.Text, new Size(double.PositiveInfinity, double.PositiveInfinity), textStyle);
                try
                {
                    using SKPaint? fill = Paint(runStyle.Fill, runStyle, definitions, new SKRect(), viewport.Size, false);
                    if (layout is SkiaTextLayout skia && fill is not null)
                    {
                        SKColor color = fill.Color;
                        skia.Draw(canvas, new Point(cursorX, cursorY - fontSize), Color.FromRgbBytes(color.Red, color.Green, color.Blue, color.Alpha), 1);
                    }
                    cursorX += layout.Size.Width;
                }
                finally { (layout as IDisposable)?.Dispose(); }
            }
            foreach (SvgNode child in run.Children) DrawRuns(child, runStyle, ref cursorX, ref cursorY, false);
        }
    }

    private sealed record Style
    {
        internal Brush? Fill { get; init; } = Brushes.Black;
        internal Brush? Stroke { get; init; }
        internal Color Color { get; init; } = Colors.Black;
        internal FillRule FillRule { get; init; }
        internal double FillOpacity { get; init; } = 1;
        internal double StrokeOpacity { get; init; } = 1;
        internal SvgLength StrokeWidth { get; init; } = new(1);
        internal SvgLineCap LineCap { get; init; }
        internal SvgLineJoin LineJoin { get; init; }
        internal double MiterLimit { get; init; } = 4;
        internal ImmutableArray<double> Dashes { get; init; } = [];
        internal double DashOffset { get; init; }
        internal SvgLength FontSize { get; init; } = new(16);
        internal string? FontFamily { get; init; }
        internal FontWeight FontWeight { get; init; } = FontWeight.Normal;
        internal bool IsVisible { get; init; } = true;

        internal Style Apply(SvgNode node) => this with
        {
            Fill = node.Fill ?? Fill, Stroke = node.Stroke ?? Stroke, Color = node.Color ?? Color,
            FillRule = node.FillRule ?? FillRule, FillOpacity = node.FillOpacity ?? FillOpacity, StrokeOpacity = node.StrokeOpacity ?? StrokeOpacity,
            StrokeWidth = node.StrokeWidth ?? StrokeWidth, LineCap = node.StrokeLineCap ?? LineCap, LineJoin = node.StrokeLineJoin ?? LineJoin,
            MiterLimit = node.StrokeMiterLimit ?? MiterLimit, Dashes = node.StrokeDashArray.IsDefault ? Dashes : node.StrokeDashArray,
            DashOffset = node.StrokeDashOffset ?? DashOffset, FontSize = node.FontSize ?? FontSize, FontFamily = node.FontFamily ?? FontFamily,
            FontWeight = node.FontWeight ?? FontWeight, IsVisible = node.IsVisible ?? IsVisible
        };
    }

    private static SKPaint? Paint(Brush? brush, Style style, Dictionary<string, SvgNode> definitions, SKRect bounds, Size viewport, bool stroke)
    {
        if (brush is null or SvgNoBrush) return null;
        double opacity = stroke ? style.StrokeOpacity : style.FillOpacity;
        SKPaint paint = new() { IsAntialias = true, Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill };
        if (brush is SolidColorBrush solid) paint.Color = solid.Color.ToSKColor(opacity);
        else if (brush is SvgCurrentColorBrush) paint.Color = style.Color.ToSKColor(opacity);
        else if (brush is SvgReferenceBrush reference)
        {
            SvgNode gradient = ResolveGradient(definitions[reference.Id], definitions);
            SvgNode[] stops = [.. gradient.Children.Where(static node => node.Kind == SvgNodeKind.Stop)];
            SKColor[] colors = stops.Select(stop => (stop.StopUsesCurrentColor == true ? style.Color : stop.StopColor ?? Colors.Black).ToSKColor(stop.StopOpacity ?? 1)).ToArray();
            float[] offsets = stops.Select(static stop => (float)(stop.Offset ?? 0)).ToArray();
            if (colors.Length == 0) { paint.Dispose(); return null; }
            if (colors.Length == 1) { paint.Color = colors[0].WithAlpha(Alpha(opacity * colors[0].Alpha / 255)); return paint; }
            for (int index = 0; index < offsets.Length; index++) offsets[index] = Math.Clamp(index == 0 ? offsets[index] : Math.Max(offsets[index - 1], offsets[index]), 0, 1);
            bool objectBounds = gradient.GradientUnits != SvgCoordinateUnits.UserSpaceOnUse;
            double X(SvgLength? length, double fallback) => objectBounds ? bounds.Left + (length?.Value ?? fallback) * bounds.Width : length?.Resolve(viewport.Width) ?? fallback;
            double Y(SvgLength? length, double fallback) => objectBounds ? bounds.Top + (length?.Value ?? fallback) * bounds.Height : length?.Resolve(viewport.Height) ?? fallback;
            SKShaderTileMode mode = gradient.SpreadMethod switch { SvgSpreadMethod.Repeat => SKShaderTileMode.Repeat, SvgSpreadMethod.Reflect => SKShaderTileMode.Mirror, _ => SKShaderTileMode.Clamp };
            SKMatrix transform = gradient.GradientTransform is Matrix3x2 matrix ? ToMatrix(matrix) : SKMatrix.Identity;
            if (gradient.Kind == SvgNodeKind.LinearGradient)
            {
                paint.Shader = SKShader.CreateLinearGradient(new SKPoint((float)X(gradient.X1, 0), (float)Y(gradient.Y1, 0)), new SKPoint((float)X(gradient.X2, 1), (float)Y(gradient.Y2, 0)), colors, offsets, mode, transform);
            }
            else if (gradient.Kind == SvgNodeKind.RadialGradient)
            {
                SKPoint center = new((float)X(gradient.Cx, .5), (float)Y(gradient.Cy, .5));
                SKPoint focus = new((float)X(gradient.Fx ?? gradient.Cx, .5), (float)Y(gradient.Fy ?? gradient.Cy, .5));
                float radius = (float)((gradient.Radius?.Value ?? .5) * (objectBounds ? Math.Max(bounds.Width, bounds.Height) : 1));
                paint.Shader = SKShader.CreateTwoPointConicalGradient(focus, 0, center, radius, colors, offsets, mode, transform);
            }
            else { paint.Dispose(); throw new InvalidDataException("A paint reference must identify a gradient."); }
            paint.Color = SKColors.White.WithAlpha(Alpha(opacity));
        }
        else { paint.Dispose(); throw new NotSupportedException("Unsupported SVG brush."); }
        if (stroke)
        {
            paint.StrokeWidth = (float)(style.StrokeWidth.Resolve(viewport.Width) ?? 1);
            paint.StrokeCap = style.LineCap switch { SvgLineCap.Round => SKStrokeCap.Round, SvgLineCap.Square => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
            paint.StrokeJoin = style.LineJoin switch { SvgLineJoin.Round => SKStrokeJoin.Round, SvgLineJoin.Bevel => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
            paint.StrokeMiter = (float)style.MiterLimit;
            if (!style.Dashes.IsDefaultOrEmpty)
            {
                float[] intervals = style.Dashes.Select(static number => (float)number).ToArray();
                if (intervals.Length % 2 != 0) intervals = [.. intervals, .. intervals];
                if (intervals.Any(static number => number < 0) || intervals.Sum() <= 0) throw new InvalidDataException("Invalid stroke dash pattern.");
                paint.PathEffect = SKPathEffect.CreateDash(intervals, (float)style.DashOffset);
            }
        }
        return paint;
    }

    private static SvgNode ResolveGradient(SvgNode node, Dictionary<string, SvgNode> definitions)
    {
        if (node.Href is not string id) return node;
        SvgNode parent = ResolveGradient(definitions[id], definitions);
        return node with
        {
            Children = node.Children.IsEmpty ? parent.Children : node.Children,
            X1 = node.X1 ?? parent.X1, X2 = node.X2 ?? parent.X2, Y1 = node.Y1 ?? parent.Y1, Y2 = node.Y2 ?? parent.Y2,
            Cx = node.Cx ?? parent.Cx, Cy = node.Cy ?? parent.Cy, Fx = node.Fx ?? parent.Fx, Fy = node.Fy ?? parent.Fy,
            Radius = node.Radius ?? parent.Radius, GradientUnits = node.GradientUnits ?? parent.GradientUnits,
            GradientTransform = node.GradientTransform ?? parent.GradientTransform, SpreadMethod = node.SpreadMethod ?? parent.SpreadMethod
        };
    }

    private static SKPath CachedPath(SkiaDrawingContext context, PathGeometry geometry) => context.Resources.GetOrCreate(geometry, () => SkiaDrawingContext.CreatePath(geometry));
    private static SKRect ToRect(Rect rect) => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
    private static SKMatrix ToMatrix(Matrix3x2 matrix) => new(matrix.M11, matrix.M21, matrix.M31, matrix.M12, matrix.M22, matrix.M32, 0, 0, 1);
    private static byte Alpha(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
