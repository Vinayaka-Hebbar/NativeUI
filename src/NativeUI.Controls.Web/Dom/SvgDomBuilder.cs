using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>Applies immutable vector snapshots to native SVG nodes without replacing unchanged descendants.</summary>
[SupportedOSPlatform("browser")]
internal static class SvgDomBuilder
{
    /// <summary>Creates DOM elements directly from a typed SVG definition.</summary>
    public static JSObject Build(SvgNode node, string prefix)
    {
        if (node.Kind == SvgNodeKind.TextRun)
        {
            return ImageInterop.CreateTextNode(node.Text);
        }

        JSObject element = ImageInterop.CreateSvgElement(Tag(node));
        try
        {
            BuildContent(element, node, prefix, false);
            return element;
        }
        catch
        {
            element.Dispose();
            throw;
        }
    }

    /// <summary>Applies definition changes to an existing control root and its positional children.</summary>
    public static void Update(JSObject element, SvgNode? previous, SvgNode current, string prefix) => UpdateCore(element, previous, current, prefix, true);

    private static void BuildContent(JSObject element, SvgNode node, string prefix, bool isControlRoot)
    {
        ApplyAttributes(element, null, node, prefix, isControlRoot);
        if (node.Text.Length > 0)
        {
            using JSObject text = ImageInterop.CreateTextNode(node.Text);
            DomInterop.AppendChild(element, text);
        }
        foreach (SvgNode child in node.Children)
        {
            using JSObject childElement = Build(child, prefix);
            DomInterop.AppendChild(element, childElement);
        }
    }

    private static void UpdateCore(JSObject element, SvgNode? previous, SvgNode current, string prefix, bool isControlRoot)
    {
        if (previous == current)
        {
            return;
        }
        if (current.Kind == SvgNodeKind.TextRun)
        {
            UpdateTextRun(element, previous, current);
            return;
        }
        if (previous is null)
        {
            BuildContent(element, current, prefix, isControlRoot);
            return;
        }
        ApplyAttributes(element, previous, current, prefix, isControlRoot);
        UpdateText(element, previous, current);
        if (previous.Children == current.Children)
        {
            return;
        }
        int offset = current.Text.Length == 0 ? 0 : 1;
        UpdateChildrenByPosition(element, previous, current, prefix, offset);
    }

    private static void UpdateTextRun(JSObject element, SvgNode? previous, SvgNode current)
    {
        if (previous?.Text == current.Text)
        {
            return;
        }

        DomInterop.SetTextContent(element, current.Text);
    }

    private static void UpdateText(JSObject element, SvgNode previous, SvgNode current)
    {
        if (previous.Text == current.Text)
        {
            return;
        }
        if (previous.Text.Length > 0)
        {
            using JSObject existing = ImageInterop.GetChildNode(element, 0);
            if (current.Text.Length == 0)
            {
                DomInterop.RemoveChild(element, existing);
            }
            else
            {
                DomInterop.SetTextContent(existing, current.Text);
            }
            return;
        }
        using JSObject text = ImageInterop.CreateTextNode(current.Text);
        if (previous.Children.IsEmpty)
        {
            DomInterop.AppendChild(element, text);
        }
        else
        {
            using JSObject first = ImageInterop.GetChildNode(element, 0);
            DomInterop.InsertBefore(element, text, first);
        }
    }

    private static void UpdateChildrenByPosition(JSObject element, SvgNode previous, SvgNode current, string prefix, int offset)
    {
        for (int index = 0; index < current.Children.Length; index++)
        {
            SvgNode child = current.Children[index];
            if (index >= previous.Children.Length)
            {
                using JSObject added = Build(child, prefix);
                DomInterop.AppendChild(element, added);
            }
            else if (previous.Children[index] != child)
            {
                UpdateChild(element, previous.Children[index], child, prefix, index + offset);
            }
        }
        for (int index = previous.Children.Length - 1; index >= current.Children.Length; index--)
        {
            using JSObject removed = ImageInterop.GetChildNode(element, index + offset);
            DomInterop.RemoveChild(element, removed);
        }
    }

    private static void UpdateChild(JSObject parent, SvgNode previous, SvgNode current, string prefix, int index)
    {
        using JSObject existing = ImageInterop.GetChildNode(parent, index);
        if (previous.Kind == current.Kind)
        {
            UpdateCore(existing, previous, current, prefix, false);
        }
        else
        {
            using JSObject replacement = Build(current, prefix);
            DomInterop.InsertBefore(parent, replacement, existing);
            DomInterop.RemoveChild(parent, existing);
        }
    }

    /// <summary>Gets the control definition, or an empty SVG viewport.</summary>
    public static SvgNode Definition(Svg svg) => svg.Definition ?? new SvgNode(SvgNodeKind.Svg) { ViewBox = svg.ViewBox };

    /// <summary>Converts the current shape state into an SVG definition using its retained geometry.</summary>
    public static SvgNode CreateShapeDefinition(Shapes.Shape shape, Size size)
    {
        PathGeometry geometry = shape.GetGeometry(size);
        List<SvgNode> definitions = [];
        SvgNode path = new(SvgNodeKind.Path)
        {
            Geometry = geometry,
            Fill = ConvertBrush(shape.Fill, "shapeFill", definitions),
            Stroke = ConvertBrush(shape.Stroke, "shapeStroke", definitions),
            StrokeWidth = new SvgLength(shape.StrokeThickness),
            FillRule = geometry.FillRule,
            IsDisplayed = shape.IsVisible
        };
        Rect bounds = shape.GetGeometryBounds(size);
        if (shape.Stretch == Stretch.None)
        {
            Rect target = StretchLayout.Fit(bounds.Size, new Rect(0, 0, size.Width, size.Height), Stretch.None);
            bounds = new Rect(bounds.X - target.X, bounds.Y - target.Y, size.Width, size.Height);
        }

        bounds = bounds with { Width = Math.Max(1, bounds.Width), Height = Math.Max(1, bounds.Height) };
        return new SvgNode(SvgNodeKind.Svg)
        {
            ViewBox = bounds,
            Children = definitions.Count == 0 ? [path] : [new SvgNode(SvgNodeKind.Definitions) { Children = [.. definitions] }, path]
        };
    }

    private static Brush ConvertBrush(Brush? brush, string id, List<SvgNode> definitions)
    {
        if (brush is null)
        {
            return SvgNoBrush.Instance;
        }
        if (brush is not GradientBrush gradient)
        {
            return brush;
        }
        SvgNode gradientNode = gradient switch
        {
            LinearGradientBrush linear => new(SvgNodeKind.LinearGradient)
            {
                X1 = new SvgLength(linear.Start.X),
                Y1 = new SvgLength(linear.Start.Y),
                X2 = new SvgLength(linear.End.X),
                Y2 = new SvgLength(linear.End.Y)
            },
            RadialGradientBrush radial => new(SvgNodeKind.RadialGradient)
            {
                Cx = new SvgLength(radial.Center.X),
                Cy = new SvgLength(radial.Center.Y),
                Radius = new SvgLength(radial.Radius)
            },
            _ => throw new NotSupportedException("Unsupported gradient brush.")
        };
        definitions.Add(gradientNode with
        {
            Id = id,
            GradientUnits = SvgCoordinateUnits.UserSpaceOnUse,
            Children = [.. gradient.Stops.Select(static stop => new SvgNode(SvgNodeKind.Stop) { Offset = stop.Offset, StopColor = stop.Color })]
        });
        return new SvgReferenceBrush(id);
    }

    private static void ApplyAttributes(JSObject element, SvgNode? previous, SvgNode current, string prefix, bool isControlRoot)
    {
        VisitAttributeChanges(previous, current, prefix, isControlRoot, (name, value) =>
        {
            if (value is null)
            {
                DomInterop.RemoveAttribute(element, name);
            }
            else
            {
                DomInterop.SetAttribute(element, name, value);
            }
        });
    }

    /// <summary>Emits only changed browser attributes from immutable snapshots.</summary>
    internal static void VisitAttributeChanges(SvgNode? previous, SvgNode node, string prefix, bool isControlRoot, Action<string, string?> set)
    {
        // The outer element's id belongs to the NativeUI control.
        if (!isControlRoot && previous?.Id != node.Id)
        {
            set("id", node.Id is string id ? prefix + id : null);
        }
        ApplyGeometryAttributes(previous, node, set);
        ApplyPaintAttributes(previous, node, prefix, set);
        ApplyTransformAttributes(previous, node, set);
        ApplyDefinitionAttributes(previous, node, prefix, set);
        ApplyTextAttributes(previous, node, set);
        ApplyAccessibilityAttributes(previous, node, set);
    }

    private static void ApplyGeometryAttributes(SvgNode? previous, SvgNode node, Action<string, string?> set)
    {
        SetChangedAttribute("viewBox", previous?.ViewBox, node.ViewBox, static value => value is Rect box ? Box(box) : null, set);
        SetChangedAttribute("d", previous?.Geometry, node.Geometry,
            static geometry => geometry is null ? null : GeometryFormatting.PathData(geometry), set);
        SetChangedAttribute("width", previous?.Width, node.Width, Length, set);
        SetChangedAttribute("height", previous?.Height, node.Height, Length, set);
        SetChangedAttribute("x", previous?.X, node.X, Length, set);
        SetChangedAttribute("y", previous?.Y, node.Y, Length, set);
        SetChangedAttribute("x1", previous?.X1, node.X1, Length, set);
        SetChangedAttribute("x2", previous?.X2, node.X2, Length, set);
        SetChangedAttribute("y1", previous?.Y1, node.Y1, Length, set);
        SetChangedAttribute("y2", previous?.Y2, node.Y2, Length, set);
        SetChangedAttribute("cx", previous?.Cx, node.Cx, Length, set);
        SetChangedAttribute("cy", previous?.Cy, node.Cy, Length, set);
        SetChangedAttribute("fx", previous?.Fx, node.Fx, Length, set);
        SetChangedAttribute("fy", previous?.Fy, node.Fy, Length, set);
        SetChangedAttribute("r", previous?.Radius, node.Radius, Length, set);
        SetChangedAttribute("dx", previous?.Dx, node.Dx, Length, set);
        SetChangedAttribute("dy", previous?.Dy, node.Dy, Length, set);
    }

    private static void ApplyPaintAttributes(SvgNode? previous, SvgNode node, string prefix, Action<string, string?> set)
    {
        SetChangedAttribute("fill", previous?.Fill, node.Fill, value => Paint(value, prefix), set);
        SetChangedAttribute("stroke", previous?.Stroke, node.Stroke, value => Paint(value, prefix), set);
        SetChangedAttribute("color", previous?.Color, node.Color, static value => value?.ToCssString(), set);
        if (previous?.StopColor != node.StopColor || (previous?.StopUsesCurrentColor == true) != (node.StopUsesCurrentColor == true))
        {
            set("stop-color", node.StopUsesCurrentColor == true ? "currentColor" : node.StopColor?.ToCssString());
        }
        SetChangedAttribute("fill-rule", previous?.FillRule, node.FillRule, FormatFillRule, set);
        SetChangedAttribute("clip-rule", previous?.ClipRule, node.ClipRule, FormatFillRule, set);
        SetChangedAttribute("opacity", previous?.Opacity, node.Opacity, Numeric, set);
        SetChangedAttribute("fill-opacity", previous?.FillOpacity, node.FillOpacity, Numeric, set);
        SetChangedAttribute("stroke-opacity", previous?.StrokeOpacity, node.StrokeOpacity, Numeric, set);
        SetChangedAttribute("stop-opacity", previous?.StopOpacity, node.StopOpacity, Numeric, set);
        SetChangedAttribute("stroke-width", previous?.StrokeWidth, node.StrokeWidth, Length, set);
        SetChangedAttribute("stroke-miterlimit", previous?.StrokeMiterLimit, node.StrokeMiterLimit, Numeric, set);
        if (!DashesEqual(previous?.StrokeDashArray ?? default, node.StrokeDashArray))
        {
            set("stroke-dasharray", FormatDashes(node.StrokeDashArray));
        }
        SetChangedAttribute("stroke-dashoffset", previous?.StrokeDashOffset, node.StrokeDashOffset, Numeric, set);
        SetChangedAttribute("stroke-linecap", previous?.StrokeLineCap, node.StrokeLineCap, static value => value?.ToString().ToLowerInvariant(), set);
        SetChangedAttribute("stroke-linejoin", previous?.StrokeLineJoin, node.StrokeLineJoin, static value => value?.ToString().ToLowerInvariant(), set);
        SetChangedAttribute("visibility", previous?.IsVisible, node.IsVisible, static value => value switch
        {
            true => "visible",
            false => "hidden",
            null => null
        }, set);
        SetChangedAttribute("display", previous?.IsDisplayed, node.IsDisplayed, static value => value switch
        {
            true => "inline",
            false => "none",
            null => null
        }, set);
    }

    private static void ApplyTransformAttributes(SvgNode? previous, SvgNode node, Action<string, string?> set)
    {
        SetChangedAttribute("transform", previous?.Transform, node.Transform, Matrix, set);
        SetChangedAttribute("gradientTransform", previous?.GradientTransform, node.GradientTransform, Matrix, set);
        SetChangedAttribute("preserveAspectRatio", previous?.AspectRatio, node.AspectRatio, static value => value is SvgAspectRatio aspect ? FormatAspect(aspect) : null, set);
    }

    private static void ApplyDefinitionAttributes(SvgNode? previous, SvgNode node, string prefix, Action<string, string?> set)
    {
        SetChangedAttribute("gradientUnits", previous?.GradientUnits, node.GradientUnits, Units, set);
        SetChangedAttribute("clipPathUnits", previous?.ClipPathUnits, node.ClipPathUnits, Units, set);
        SetChangedAttribute("maskUnits", previous?.MaskUnits, node.MaskUnits, Units, set);
        SetChangedAttribute("maskContentUnits", previous?.MaskContentUnits, node.MaskContentUnits, Units, set);
        SetChangedAttribute("spreadMethod", previous?.SpreadMethod, node.SpreadMethod, static value => value?.ToString().ToLowerInvariant(), set);
        SetChangedAttribute("offset", previous?.Offset, node.Offset, Numeric, set);
        SetChangedAttribute("clip-path", previous?.ClipPathId, node.ClipPathId, value => value is null ? null : $"url(#{prefix}{value})", set);
        SetChangedAttribute("mask", previous?.MaskId, node.MaskId, value => value is null ? null : $"url(#{prefix}{value})", set);
        SetChangedAttribute("href", previous?.Href, node.Href, value => value is null ? null : "#" + prefix + value, set);
        SetChangedAttribute("mask-type", previous?.LuminanceMask, node.LuminanceMask, static value => value switch
        {
            true => "luminance",
            false => "alpha",
            null => null
        }, set);
    }

    private static void ApplyTextAttributes(SvgNode? previous, SvgNode node, Action<string, string?> set)
    {
        SetChangedAttribute("font-family", previous?.FontFamily, node.FontFamily, static value => value, set);
        SetChangedAttribute("font-size", previous?.FontSize, node.FontSize, Length, set);
        SetChangedAttribute("font-weight", previous?.FontWeight, node.FontWeight, static value => value is FontWeight weight ? ((int)weight).ToString(CultureInfo.InvariantCulture) : null, set);
        SetChangedAttribute("font-style", previous?.Italic, node.Italic, static value => value switch
        {
            true => "italic",
            false => "normal",
            null => null
        }, set);
        SetChangedAttribute("text-anchor", previous?.TextAnchor, node.TextAnchor, static value => value?.ToString().ToLowerInvariant(), set);
        SetChangedAttribute("dominant-baseline", previous?.DominantBaseline, node.DominantBaseline, static value => value switch
        {
            SvgBaseline.TextBeforeEdge => "text-before-edge",
            SvgBaseline.TextAfterEdge => "text-after-edge",
            null => null,
            _ => value.ToString()?.ToLowerInvariant()
        }, set);
    }

    private static void ApplyAccessibilityAttributes(SvgNode? previous, SvgNode node, Action<string, string?> set)
    {
        SetChangedAttribute("role", previous?.Role, node.Role, static value => value, set);
        SetChangedAttribute("aria-label", previous?.AccessibleName, node.AccessibleName, static value => value, set);
        SetChangedAttribute("aria-hidden", previous?.IsAccessibilityHidden, node.IsAccessibilityHidden, static value => value switch
        {
            true => "true",
            false => "false",
            null => null
        }, set);
    }

    private static void SetChangedAttribute<T>(string name, T previous, T current, Func<T, string?> format, Action<string, string?> set)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, current))
        {
            set(name, format(current));
        }
    }

    private static string? FormatFillRule(FillRule? rule) => rule switch
    {
        FillRule.EvenOdd => "evenodd",
        FillRule.Nonzero => "nonzero",
        _ => null
    };

    private static string? FormatDashes(ImmutableArray<double> dashes)
    {
        if (dashes.IsDefault)
        {
            return null;
        }

        return dashes.IsEmpty ? "none" : string.Join(" ", dashes.Select(Number));
    }

    private static bool DashesEqual(ImmutableArray<double> previous, ImmutableArray<double> current) =>
        previous.IsDefault == current.IsDefault && previous.AsSpan().SequenceEqual(current.AsSpan());

    internal static string FormatAspect(SvgAspectRatio aspect)
    {
        string alignment = aspect.Alignment switch
        {
            SvgAlignment.None => "none",
            SvgAlignment.XMinYMin => "xMinYMin",
            SvgAlignment.XMidYMin => "xMidYMin",
            SvgAlignment.XMaxYMin => "xMaxYMin",
            SvgAlignment.XMinYMid => "xMinYMid",
            SvgAlignment.XMidYMid => "xMidYMid",
            SvgAlignment.XMaxYMid => "xMaxYMid",
            SvgAlignment.XMinYMax => "xMinYMax",
            SvgAlignment.XMidYMax => "xMidYMax",
            SvgAlignment.XMaxYMax => "xMaxYMax",
            _ => throw new ArgumentOutOfRangeException(nameof(aspect))
        };
        if (aspect.Alignment == SvgAlignment.None || aspect.Stretch == Stretch.Fill)
        {
            return "none";
        }

        return alignment + (aspect.Stretch == Stretch.UniformToFill ? " slice" : " meet");
    }

    private static string Tag(SvgNode node) => node.Kind switch
    {
        SvgNodeKind.Svg => "svg",
        SvgNodeKind.Group => "g",
        SvgNodeKind.Path => "path",
        SvgNodeKind.Definitions => "defs",
        SvgNodeKind.LinearGradient => "linearGradient",
        SvgNodeKind.RadialGradient => "radialGradient",
        SvgNodeKind.Stop => "stop",
        SvgNodeKind.ClipPath => "clipPath",
        SvgNodeKind.Mask => "mask",
        SvgNodeKind.Use => "use",
        SvgNodeKind.Text => "text",
        SvgNodeKind.TextSpan => "tspan",
        SvgNodeKind.TextRun => "#text",
        SvgNodeKind.Title => "title",
        SvgNodeKind.Description => "desc",
        _ => throw new ArgumentOutOfRangeException(nameof(node))
    };

    private static string? Paint(Brush? brush, string prefix) => brush switch
    {
        null => null,
        SolidColorBrush solid => solid.Color.ToCssString(),
        SvgNoBrush => "none",
        SvgCurrentColorBrush => "currentColor",
        SvgReferenceBrush reference => "url(#" + prefix + reference.Id + ")",
        _ => throw new NotSupportedException("Unsupported SVG brush.")
    };
    private static string? Length(SvgLength? length) => length is SvgLength value ? Number(value.Value * (value.IsPercentage ? 100 : 1)) + (value.IsPercentage ? "%" : "") : null;
    private static string? Numeric(double? value) => value is double number ? Number(number) : null;
    private static string? Units(SvgCoordinateUnits? units) => units switch { SvgCoordinateUnits.ObjectBoundingBox => "objectBoundingBox", SvgCoordinateUnits.UserSpaceOnUse => "userSpaceOnUse", _ => null };
    private static string? Matrix(Matrix3x2? matrix) => matrix is Matrix3x2 value
        ? $"matrix({Number(value.M11)} {Number(value.M12)} {Number(value.M21)} {Number(value.M22)} {Number(value.M31)} {Number(value.M32)})" : null;

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    public static string Box(Rect box) => $"{Number(box.X)} {Number(box.Y)} {Number(box.Width)} {Number(box.Height)}";
    public static string Aspect(Stretch stretch) => stretch switch
    {
        Stretch.Fill => "none",
        Stretch.UniformToFill => "xMidYMid slice",
        _ => "xMidYMid meet"
    };
}
