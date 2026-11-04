using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>Immutable compiled SVG artwork, independent of parsing, serialization, and visual ownership.</summary>
public sealed record SvgNode
{
    /// <summary>Creates a node with a typed SVG kind.</summary>
    public SvgNode(SvgNodeKind kind) => Kind = kind;

    /// <summary>Gets the node's typed drawing semantics.</summary>
    public SvgNodeKind Kind { get; }
    /// <summary>Gets shared immutable shape geometry.</summary>
    public PathGeometry? Geometry { get; init; }
    /// <summary>Gets ordered immutable child definitions.</summary>
    public ImmutableArray<SvgNode> Children { get; init; } = [];
    /// <summary>Gets literal text content.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the parsed Id value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? Id { get; init; }
    /// <summary>Gets the parsed ViewBox value; unspecified values retain SVG defaults or inheritance.</summary>
    public Rect? ViewBox { get; init; }
    /// <summary>Gets the parsed Width value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Width { get; init; }
    /// <summary>Gets the parsed Height value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Height { get; init; }
    /// <summary>Gets the parsed X value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? X { get; init; }
    /// <summary>Gets the parsed Y value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Y { get; init; }
    /// <summary>Gets the parsed X1 value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? X1 { get; init; }
    /// <summary>Gets the parsed X2 value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? X2 { get; init; }
    /// <summary>Gets the parsed Y1 value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Y1 { get; init; }
    /// <summary>Gets the parsed Y2 value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Y2 { get; init; }
    /// <summary>Gets the parsed Cx value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Cx { get; init; }
    /// <summary>Gets the parsed Cy value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Cy { get; init; }
    /// <summary>Gets the parsed Fx value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Fx { get; init; }
    /// <summary>Gets the parsed Fy value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Fy { get; init; }
    /// <summary>Gets the parsed Radius value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Radius { get; init; }
    /// <summary>Gets the parsed Dx value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Dx { get; init; }
    /// <summary>Gets the parsed Dy value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? Dy { get; init; }
    /// <summary>Gets the parsed Transform value; unspecified values retain SVG defaults or inheritance.</summary>
    public Matrix3x2? Transform { get; init; }
    /// <summary>Gets the parsed GradientTransform value; unspecified values retain SVG defaults or inheritance.</summary>
    public Matrix3x2? GradientTransform { get; init; }
    /// <summary>Gets the parsed Fill value; unspecified values retain SVG defaults or inheritance.</summary>
    public Brush? Fill { get; init; }
    /// <summary>Gets the parsed Stroke value; unspecified values retain SVG defaults or inheritance.</summary>
    public Brush? Stroke { get; init; }
    /// <summary>Gets the parsed Color value; unspecified values retain SVG defaults or inheritance.</summary>
    public Color? Color { get; init; }
    /// <summary>Gets the parsed StopColor value; unspecified values retain SVG defaults or inheritance.</summary>
    public Color? StopColor { get; init; }
    /// <summary>Gets the parsed StopUsesCurrentColor value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? StopUsesCurrentColor { get; init; }
    /// <summary>Gets the parsed FillRule value; unspecified values retain SVG defaults or inheritance.</summary>
    public FillRule? FillRule { get; init; }
    /// <summary>Gets the parsed ClipRule value; unspecified values retain SVG defaults or inheritance.</summary>
    public FillRule? ClipRule { get; init; }
    /// <summary>Gets the parsed Opacity value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? Opacity { get; init; }
    /// <summary>Gets the parsed FillOpacity value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? FillOpacity { get; init; }
    /// <summary>Gets the parsed StrokeOpacity value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? StrokeOpacity { get; init; }
    /// <summary>Gets the parsed StopOpacity value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? StopOpacity { get; init; }
    /// <summary>Gets the parsed StrokeWidth value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? StrokeWidth { get; init; }
    /// <summary>Gets the parsed StrokeMiterLimit value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? StrokeMiterLimit { get; init; }
    /// <summary>Gets the parsed StrokeDashArray value; unspecified values retain SVG defaults or inheritance.</summary>
    public ImmutableArray<double> StrokeDashArray { get; init; }
    /// <summary>Gets the parsed StrokeDashOffset value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? StrokeDashOffset { get; init; }
    /// <summary>Gets the parsed StrokeLineCap value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLineCap? StrokeLineCap { get; init; }
    /// <summary>Gets the parsed StrokeLineJoin value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLineJoin? StrokeLineJoin { get; init; }
    /// <summary>Gets the parsed GradientUnits value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgCoordinateUnits? GradientUnits { get; init; }
    /// <summary>Gets the parsed ClipPathUnits value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgCoordinateUnits? ClipPathUnits { get; init; }
    /// <summary>Gets the parsed MaskUnits value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgCoordinateUnits? MaskUnits { get; init; }
    /// <summary>Gets the parsed MaskContentUnits value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgCoordinateUnits? MaskContentUnits { get; init; }
    /// <summary>Gets the parsed SpreadMethod value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgSpreadMethod? SpreadMethod { get; init; }
    /// <summary>Gets the parsed Offset value; unspecified values retain SVG defaults or inheritance.</summary>
    public double? Offset { get; init; }
    /// <summary>Gets the parsed ClipPathId value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? ClipPathId { get; init; }
    /// <summary>Gets the parsed MaskId value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? MaskId { get; init; }
    /// <summary>Gets the parsed Href value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? Href { get; init; }
    /// <summary>Gets the parsed FontFamily value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? FontFamily { get; init; }
    /// <summary>Gets the parsed FontSize value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgLength? FontSize { get; init; }
    /// <summary>Gets the parsed FontWeight value; unspecified values retain SVG defaults or inheritance.</summary>
    public FontWeight? FontWeight { get; init; }
    /// <summary>Gets the parsed Italic value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? Italic { get; init; }
    /// <summary>Gets the parsed TextAnchor value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgTextAnchor? TextAnchor { get; init; }
    /// <summary>Gets the parsed DominantBaseline value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgBaseline? DominantBaseline { get; init; }
    /// <summary>Gets the parsed IsVisible value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? IsVisible { get; init; }
    /// <summary>Gets the parsed IsDisplayed value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? IsDisplayed { get; init; }
    /// <summary>Gets the parsed LuminanceMask value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? LuminanceMask { get; init; }
    /// <summary>Gets the parsed AspectRatio value; unspecified values retain SVG defaults or inheritance.</summary>
    public SvgAspectRatio? AspectRatio { get; init; }
    /// <summary>Gets the parsed Role value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? Role { get; init; }
    /// <summary>Gets the parsed AccessibleName value; unspecified values retain SVG defaults or inheritance.</summary>
    public string? AccessibleName { get; init; }
    /// <summary>Gets the parsed IsAccessibilityHidden value; unspecified values retain SVG defaults or inheritance.</summary>
    public bool? IsAccessibilityHidden { get; init; }

    /// <summary>Resolves logical intrinsic dimensions using typed lengths and viewBox data.</summary>
    public Size GetIntrinsicSize(Size? containingSize = null)
    {
        double? width = Width?.Resolve(containingSize?.Width);
        double? height = Height?.Resolve(containingSize?.Height);
        double ratio = ViewBox is Rect box ? box.Width / box.Height : 2;
        return new Size(width ?? (height.HasValue ? height.Value * ratio : ViewBox?.Width ?? 300),
            height ?? (width.HasValue ? width.Value / ratio : ViewBox?.Height ?? 150));
    }

    /// <summary>Gets the typed viewBox, or an intrinsic viewport when absent.</summary>
    public Rect GetViewBox(Size? containingSize = null)
    {
        if (ViewBox is Rect box) return box;
        Size size = GetIntrinsicSize(containingSize);
        return new Rect(0, 0, size.Width, size.Height);
    }
}
