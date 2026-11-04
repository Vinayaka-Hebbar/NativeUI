using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace NativeUI.Media;

internal enum SvgValueKind { Text, Number, Boolean, Length, Rect, Matrix, Color, Paint, Enum, Numbers, Aspect }
internal sealed record SvgPropertyData(string Name, SvgValueKind Kind, double[] Numbers, string? Text = null, string? Type = null);

/// <summary>Parses SVG attribute text once at the compiler or explicit runtime document boundary.</summary>
internal static class SvgValueParser
{
    internal static string NodeKind(string name) => name switch
    {
        "svg" => "Svg", "g" => "Group", "defs" => "Definitions", "linearGradient" => "LinearGradient",
        "radialGradient" => "RadialGradient", "stop" => "Stop", "clipPath" => "ClipPath", "mask" => "Mask",
        "use" => "Use", "text" => "Text", "tspan" => "TextSpan", "#text" => "TextRun", "title" => "Title", "desc" => "Description",
        _ => "Path"
    };

    internal static IReadOnlyList<SvgPropertyData> Parse(Dictionary<string, string> attributes)
    {
        List<SvgPropertyData> values = [];
        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            string text = attribute.Value;
            if (text == "inherit") continue;
            SvgPropertyData? value = attribute.Key switch
            {
                "id" => new("Id", SvgValueKind.Text, [], text),
                "viewBox" => new("ViewBox", SvgValueKind.Rect, Numbers(text)),
                "width" => Length("Width", text),
                "height" => Length("Height", text),
                "x" => Length("X", text),
                "y" => Length("Y", text),
                "x1" => Length("X1", text),
                "x2" => Length("X2", text),
                "y1" => Length("Y1", text),
                "y2" => Length("Y2", text),
                "cx" => Length("Cx", text),
                "cy" => Length("Cy", text),
                "fx" => Length("Fx", text),
                "fy" => Length("Fy", text),
                "r" => Length("Radius", text),
                "dx" => Length("Dx", text),
                "dy" => Length("Dy", text),
                "transform" => Matrix("Transform", text),
                "gradientTransform" => Matrix("GradientTransform", text),
                "fill" => Paint("Fill", text),
                "stroke" => Paint("Stroke", text),
                "color" => text == "currentColor" ? null : Color("Color", text),
                "stop-color" => Color("StopColor", text),
                "fill-rule" => EnumValue("FillRule", "FillRule", text),
                "clip-rule" => EnumValue("ClipRule", "FillRule", text),
                "opacity" => new("Opacity", SvgValueKind.Number, [Fraction(text)]),
                "fill-opacity" => new("FillOpacity", SvgValueKind.Number, [Fraction(text)]),
                "stroke-opacity" => new("StrokeOpacity", SvgValueKind.Number, [Fraction(text)]),
                "stop-opacity" => new("StopOpacity", SvgValueKind.Number, [Fraction(text)]),
                "stroke-width" => Length("StrokeWidth", text),
                "stroke-miterlimit" => new("StrokeMiterLimit", SvgValueKind.Number, [Fraction(text)]),
                "stroke-dasharray" => new("StrokeDashArray", SvgValueKind.Numbers, text == "none" ? [] : Numbers(text)),
                "stroke-dashoffset" => new("StrokeDashOffset", SvgValueKind.Number, [Fraction(text)]),
                "stroke-linecap" => EnumValue("StrokeLineCap", "SvgLineCap", text),
                "stroke-linejoin" => EnumValue("StrokeLineJoin", "SvgLineJoin", text),
                "gradientUnits" => EnumValue("GradientUnits", "SvgCoordinateUnits", text),
                "clipPathUnits" => EnumValue("ClipPathUnits", "SvgCoordinateUnits", text),
                "maskUnits" => EnumValue("MaskUnits", "SvgCoordinateUnits", text),
                "maskContentUnits" => EnumValue("MaskContentUnits", "SvgCoordinateUnits", text),
                "spreadMethod" => EnumValue("SpreadMethod", "SvgSpreadMethod", text),
                "offset" => new("Offset", SvgValueKind.Number, [Fraction(text)]),
                "clip-path" => text == "none" ? null : new("ClipPathId", SvgValueKind.Text, [], Reference(text)),
                "mask" => text == "none" ? null : new("MaskId", SvgValueKind.Text, [], Reference(text)),
                "href" => new("Href", SvgValueKind.Text, [], text[1..]),
                "font-family" => new("FontFamily", SvgValueKind.Text, [], text),
                "font-size" => Length("FontSize", text),
                "font-weight" => new("FontWeight", SvgValueKind.Number, [text == "bold" ? 700 : text == "normal" ? 400 : SvgDocumentParser.Number(text)]),
                "font-style" => new("Italic", SvgValueKind.Boolean, [text != "normal" ? 1 : 0]),
                "text-anchor" => EnumValue("TextAnchor", "SvgTextAnchor", text),
                "dominant-baseline" => EnumValue("DominantBaseline", "SvgBaseline", text),
                "visibility" => new("IsVisible", SvgValueKind.Boolean, [text == "visible" ? 1 : 0]),
                "display" => new("IsDisplayed", SvgValueKind.Boolean, [text != "none" ? 1 : 0]),
                "mask-type" => new("LuminanceMask", SvgValueKind.Boolean, [text != "alpha" ? 1 : 0]),
                "preserveAspectRatio" => Aspect(text),
                "role" => new("Role", SvgValueKind.Text, [], text),
                "aria-label" => new("AccessibleName", SvgValueKind.Text, [], text),
                "aria-hidden" => new("IsAccessibilityHidden", SvgValueKind.Boolean, [text == "true" ? 1 : 0]),
                _ => null
            };
            if (value is not null) values.Add(value);
        }
        return values;
    }

    private static SvgPropertyData Length(string name, string text)
    {
        bool percentage = text.EndsWith('%');
        double factor = 1;
        string number = text;
        if (percentage) { number = text[..^1]; factor = .01; }
        else if (text.EndsWith("px", StringComparison.Ordinal)) number = text[..^2];
        else if (text.EndsWith("in", StringComparison.Ordinal)) { number = text[..^2]; factor = 96; }
        else if (text.EndsWith("cm", StringComparison.Ordinal)) { number = text[..^2]; factor = 96 / 2.54; }
        else if (text.EndsWith("mm", StringComparison.Ordinal)) { number = text[..^2]; factor = 96 / 25.4; }
        else if (text.EndsWith("pt", StringComparison.Ordinal)) { number = text[..^2]; factor = 96 / 72.0; }
        else if (text.EndsWith("pc", StringComparison.Ordinal)) { number = text[..^2]; factor = 16; }
        return new(name, SvgValueKind.Length, [SvgDocumentParser.Number(number) * factor, percentage ? 1 : 0]);
    }

    private static SvgPropertyData EnumValue(string name, string type, string text)
    {
        bool valid = type switch
        {
            "FillRule" => text is "nonzero" or "evenodd",
            "SvgCoordinateUnits" => text is "objectBoundingBox" or "userSpaceOnUse",
            "SvgLineCap" => text is "butt" or "round" or "square",
            "SvgLineJoin" => text is "miter" or "round" or "bevel",
            "SvgSpreadMethod" => text is "pad" or "reflect" or "repeat",
            "SvgTextAnchor" => text is "start" or "middle" or "end",
            "SvgBaseline" => text is "auto" or "alphabetic" or "middle" or "central" or "hanging" or "text-before-edge" or "text-after-edge" or "ideographic" or "mathematical",
            _ => false
        };
        if (!valid) throw new FormatException($"Unsupported SVG {name} value '{text}'.");
        string member = text switch
        {
            "nonzero" => "Nonzero", "evenodd" => "EvenOdd", "objectBoundingBox" => "ObjectBoundingBox", "userSpaceOnUse" => "UserSpaceOnUse",
            "butt" => "Butt", "round" => "Round", "square" => "Square", "miter" => "Miter", "bevel" => "Bevel",
            "pad" => "Pad", "reflect" => "Reflect", "repeat" => "Repeat", "start" => "Start", "middle" => "Middle", "end" => "End",
            "auto" => "Auto", "alphabetic" => "Alphabetic", "central" => "Central", "hanging" => "Hanging",
            "text-before-edge" => "TextBeforeEdge", "text-after-edge" => "TextAfterEdge", "ideographic" => "Ideographic", "mathematical" => "Mathematical",
            _ => throw new FormatException($"Unsupported SVG {name} value '{text}'.")
        };
        return new(name, SvgValueKind.Enum, [], member, type);
    }

    private static SvgPropertyData Paint(string name, string text) => text switch
    {
        "none" => new(name, SvgValueKind.Paint, [], null, "None"),
        "currentColor" => new(name, SvgValueKind.Paint, [], null, "CurrentColor"),
        _ when text.StartsWith("url(", StringComparison.Ordinal) => new(name, SvgValueKind.Paint, [], Reference(text), "Reference"),
        _ => Color(name, text) with { Kind = SvgValueKind.Paint, Type = "Solid" }
    };

    private static SvgPropertyData Color(string name, string text)
    {
        if (text == "currentColor") return new("StopUsesCurrentColor", SvgValueKind.Boolean, [1]);
        string literal = text.ToLowerInvariant() switch
        {
            "transparent" => "#00000000", "black" => "#000000", "white" => "#ffffff", "red" => "#ff0000", "green" => "#008000", "blue" => "#0000ff", "yellow" => "#ffff00",
            "gray" or "grey" => "#808080", "orange" => "#ffa500", "purple" => "#800080", "pink" => "#ffc0cb", "cyan" or "aqua" => "#00ffff", "magenta" or "fuchsia" => "#ff00ff",
            "navy" => "#000080", "lime" => "#00ff00", "silver" => "#c0c0c0", "maroon" => "#800000", _ => text
        };
        double[] channels = [0, 0, 0, 1];
        if (literal.StartsWith('#') && literal.Length is 4 or 5 or 7 or 9)
        {
            int length = literal.Length <= 5 ? 1 : 2;
            int count = (literal.Length - 1) / length;
            for (int index = 0; index < count; index++)
            {
                string channel = literal.Substring(1 + index * length, length);
                if (length == 1) channel += channel;
                if (!byte.TryParse(channel, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte component)) throw new FormatException("Invalid SVG hex color.");
                channels[index] = component / 255.0;
            }
            return new(name, SvgValueKind.Color, channels, literal);
        }
        if ((literal.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) || literal.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase)) && literal.EndsWith(')'))
        {
            string[] parts = literal[(literal.IndexOf('(') + 1)..^1].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is not (3 or 4)) throw new FormatException("Invalid SVG RGB color.");
            for (int index = 0; index < parts.Length; index++)
            {
                bool percent = parts[index].EndsWith('%');
                channels[index] = Math.Clamp(SvgDocumentParser.Number(percent ? parts[index][..^1] : parts[index]) / (percent ? 100 : index == 3 ? 1 : 255), 0, 1);
            }
            return new(name, SvgValueKind.Color, channels, literal);
        }
        throw new FormatException($"Unsupported SVG color '{text}'.");
    }

    private static SvgPropertyData Matrix(string name, string text)
    {
        Matrix3x2 matrix = Matrix3x2.Identity;
        foreach (Match match in Regex.Matches(text, @"([A-Za-z]+)\s*\(([^)]*)\)"))
        {
            double[] values = Numbers(match.Groups[2].Value);
            float radians = (float)(values[0] * Math.PI / 180);
            Matrix3x2 next = match.Groups[1].Value switch
            {
                "matrix" => new((float)values[0], (float)values[1], (float)values[2], (float)values[3], (float)values[4], (float)values[5]),
                "translate" => Matrix3x2.CreateTranslation((float)values[0], values.Length == 2 ? (float)values[1] : 0),
                "scale" => Matrix3x2.CreateScale((float)values[0], values.Length == 2 ? (float)values[1] : (float)values[0]),
                "rotate" => values.Length == 3 ? Matrix3x2.CreateRotation(radians, new Vector2((float)values[1], (float)values[2])) : Matrix3x2.CreateRotation(radians),
                "skewX" => new(1, 0, MathF.Tan(radians), 1, 0, 0),
                "skewY" => new(1, MathF.Tan(radians), 0, 1, 0, 0),
                _ => throw new FormatException("Invalid SVG transform.")
            };
            matrix = next * matrix;
        }
        float[] components = [matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32];
        if (components.Any(static component => !float.IsFinite(component))) throw new FormatException("SVG transform exceeds finite matrix limits.");
        return new(name, SvgValueKind.Matrix, components.Select(static component => (double)component).ToArray());
    }

    private static SvgPropertyData Aspect(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string alignment = parts[0] == "none" ? "None" : char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
        if (alignment is not ("None" or "XMinYMin" or "XMidYMin" or "XMaxYMin" or "XMinYMid" or "XMidYMid" or "XMaxYMid" or "XMinYMax" or "XMidYMax" or "XMaxYMax")) throw new FormatException("Invalid SVG alignment.");
        if (parts.Length > 2 || (parts.Length == 2 && parts[1] is not ("meet" or "slice"))) throw new FormatException("Invalid SVG aspect ratio.");
        return new("AspectRatio", SvgValueKind.Aspect, [parts.Length == 2 && parts[1] == "slice" ? 1 : 0], alignment);
    }

    private static string Reference(string text)
    {
        int hash = text.IndexOf('#');
        return text[(hash + 1)..].TrimEnd(')', '\'', '"', ' ');
    }
    private static double Fraction(string text) => text.EndsWith('%') ? SvgDocumentParser.Number(text[..^1]) / 100 : SvgDocumentParser.Number(text);
    private static double[] Numbers(string text) => text.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(SvgDocumentParser.Number).ToArray();
}
