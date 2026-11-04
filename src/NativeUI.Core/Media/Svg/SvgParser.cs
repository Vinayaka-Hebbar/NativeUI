using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>Validates and parses explicitly supplied runtime SVG documents into immutable typed artwork.</summary>
public static class SvgParser
{
    /// <summary>Parses a self-contained static SVG, rejecting unsafe and unsupported content.</summary>
    public static SvgNode Parse(string source) => Convert(SvgDocumentParser.Parse(source));

    private static SvgNode Convert(SvgNodeData data)
    {
        SvgNode node = new(Enum.Parse<SvgNodeKind>(SvgValueParser.NodeKind(data.Name)))
        {
            Children = [.. data.Children.Select(Convert)],
            Geometry = data.Commands is null ? null : PathGeometry.FromCommands(data.Commands),
            Text = data.Text
        };
        foreach (SvgPropertyData value in data.Properties)
        {
            double[] numbers = value.Numbers;
            node = value.Name switch
            {
                "Id" => node with { Id = value.Text },
                "ViewBox" => node with { ViewBox = new Rect(numbers[0], numbers[1], numbers[2], numbers[3]) },
                "Width" => node with { Width = new SvgLength(numbers[0], numbers[1] == 1) },
                "Height" => node with { Height = new SvgLength(numbers[0], numbers[1] == 1) },
                "X" => node with { X = new SvgLength(numbers[0], numbers[1] == 1) },
                "Y" => node with { Y = new SvgLength(numbers[0], numbers[1] == 1) },
                "X1" => node with { X1 = new SvgLength(numbers[0], numbers[1] == 1) },
                "X2" => node with { X2 = new SvgLength(numbers[0], numbers[1] == 1) },
                "Y1" => node with { Y1 = new SvgLength(numbers[0], numbers[1] == 1) },
                "Y2" => node with { Y2 = new SvgLength(numbers[0], numbers[1] == 1) },
                "Cx" => node with { Cx = new SvgLength(numbers[0], numbers[1] == 1) },
                "Cy" => node with { Cy = new SvgLength(numbers[0], numbers[1] == 1) },
                "Fx" => node with { Fx = new SvgLength(numbers[0], numbers[1] == 1) },
                "Fy" => node with { Fy = new SvgLength(numbers[0], numbers[1] == 1) },
                "Radius" => node with { Radius = new SvgLength(numbers[0], numbers[1] == 1) },
                "Dx" => node with { Dx = new SvgLength(numbers[0], numbers[1] == 1) },
                "Dy" => node with { Dy = new SvgLength(numbers[0], numbers[1] == 1) },
                "Transform" => node with { Transform = new Matrix3x2((float)numbers[0], (float)numbers[1], (float)numbers[2], (float)numbers[3], (float)numbers[4], (float)numbers[5]) },
                "GradientTransform" => node with { GradientTransform = new Matrix3x2((float)numbers[0], (float)numbers[1], (float)numbers[2], (float)numbers[3], (float)numbers[4], (float)numbers[5]) },
                "Fill" => node with { Fill = Paint(value) },
                "Stroke" => node with { Stroke = Paint(value) },
                "Color" => node with { Color = Color.FromSrgb(numbers[0], numbers[1], numbers[2], numbers[3]) },
                "StopColor" => node with { StopColor = Color.FromSrgb(numbers[0], numbers[1], numbers[2], numbers[3]) },
                "StopUsesCurrentColor" => node with { StopUsesCurrentColor = numbers[0] == 1 },
                "FillRule" => node with { FillRule = Enum.Parse<FillRule>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "ClipRule" => node with { ClipRule = Enum.Parse<FillRule>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "Opacity" => node with { Opacity = numbers[0] },
                "FillOpacity" => node with { FillOpacity = numbers[0] },
                "StrokeOpacity" => node with { StrokeOpacity = numbers[0] },
                "StopOpacity" => node with { StopOpacity = numbers[0] },
                "StrokeWidth" => node with { StrokeWidth = new SvgLength(numbers[0], numbers[1] == 1) },
                "StrokeMiterLimit" => node with { StrokeMiterLimit = numbers[0] },
                "StrokeDashArray" => node with { StrokeDashArray = [.. numbers] },
                "StrokeDashOffset" => node with { StrokeDashOffset = numbers[0] },
                "StrokeLineCap" => node with { StrokeLineCap = Enum.Parse<SvgLineCap>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "StrokeLineJoin" => node with { StrokeLineJoin = Enum.Parse<SvgLineJoin>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "GradientUnits" => node with { GradientUnits = Enum.Parse<SvgCoordinateUnits>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "ClipPathUnits" => node with { ClipPathUnits = Enum.Parse<SvgCoordinateUnits>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "MaskUnits" => node with { MaskUnits = Enum.Parse<SvgCoordinateUnits>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "MaskContentUnits" => node with { MaskContentUnits = Enum.Parse<SvgCoordinateUnits>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "SpreadMethod" => node with { SpreadMethod = Enum.Parse<SvgSpreadMethod>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "Offset" => node with { Offset = numbers[0] },
                "ClipPathId" => node with { ClipPathId = value.Text },
                "MaskId" => node with { MaskId = value.Text },
                "Href" => node with { Href = value.Text },
                "FontFamily" => node with { FontFamily = value.Text },
                "FontSize" => node with { FontSize = new SvgLength(numbers[0], numbers[1] == 1) },
                "FontWeight" => node with { FontWeight = (FontWeight)(int)numbers[0] },
                "Italic" => node with { Italic = numbers[0] == 1 },
                "TextAnchor" => node with { TextAnchor = Enum.Parse<SvgTextAnchor>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "DominantBaseline" => node with { DominantBaseline = Enum.Parse<SvgBaseline>(value.Text ?? throw new FormatException("Missing SVG enum value.")) },
                "IsVisible" => node with { IsVisible = numbers[0] == 1 },
                "IsDisplayed" => node with { IsDisplayed = numbers[0] == 1 },
                "LuminanceMask" => node with { LuminanceMask = numbers[0] == 1 },
                "AspectRatio" => node with { AspectRatio = new SvgAspectRatio(Enum.Parse<SvgAlignment>(value.Text ?? "XMidYMid"), numbers[0] == 1 ? Stretch.UniformToFill : Stretch.Uniform) },
                "Role" => node with { Role = value.Text },
                "AccessibleName" => node with { AccessibleName = value.Text },
                "IsAccessibilityHidden" => node with { IsAccessibilityHidden = numbers[0] == 1 },
                _ => throw new FormatException($"Unsupported parsed SVG property '{value.Name}'.")
            };
        }
        return node;
    }

    private static Brush Paint(SvgPropertyData value) => value.Type switch
    {
        "None" => SvgNoBrush.Instance,
        "CurrentColor" => SvgCurrentColorBrush.Instance,
        "Reference" => new SvgReferenceBrush(value.Text ?? throw new FormatException("Missing SVG reference.")),
        _ => new SolidColorBrush(Color.FromSrgb(value.Numbers[0], value.Numbers[1], value.Numbers[2], value.Numbers[3]))
    };
}
