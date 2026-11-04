using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NativeUI.Media;

internal sealed record SvgNodeData(string Name, Dictionary<string, string> Attributes, string Text, SvgNodeData[] Children, IReadOnlyList<PathCommand>? Commands, IReadOnlyList<SvgPropertyData> Properties);

internal static partial class SvgDocumentParser
{
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polygon", "polyline", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "use", "text", "tspan", "title", "desc"
    };
    private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
    {
        "id", "viewBox", "width", "height", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r", "rx", "ry", "fx", "fy", "d", "points", "transform", "fill", "fill-rule", "fill-opacity", "stroke", "stroke-width", "stroke-opacity", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "opacity", "clip-path", "clip-rule", "clipPathUnits", "mask", "maskUnits", "maskContentUnits", "mask-type", "gradientUnits", "gradientTransform", "spreadMethod", "offset", "stop-color", "stop-opacity", "href", "font-family", "font-size", "font-weight", "font-style", "text-anchor", "dominant-baseline", "dx", "dy", "preserveAspectRatio", "color", "display", "visibility", "version", "role", "aria-label", "aria-hidden"
    };

    public static SvgNodeData Parse(string text)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = Math.Max(1, text.Length) };
        using StringReader source = new(text);
        using XmlReader reader = XmlReader.Create(source, settings);
        XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new FormatException("An SVG root is required.");
        if (root.Name.LocalName != "svg") throw new FormatException("An SVG root is required.");
        int count = 0, commands = 0;
        SvgNodeData result = Read(root, 0, ref count, ref commands);
        Dictionary<string, SvgNodeData> ids = new(StringComparer.Ordinal);
        void Index(SvgNodeData node)
        {
            if (node.Attributes.TryGetValue("id", out string? id) && !ids.TryAdd(id, node)) throw new FormatException($"Duplicate SVG id '{id}'.");
            foreach (SvgNodeData child in node.Children) Index(child);
        }
        Index(result);
        HashSet<SvgNodeData> active = new(ReferenceEqualityComparer.Instance);
        HashSet<SvgNodeData> complete = new(ReferenceEqualityComparer.Instance);
        void Validate(SvgNodeData node, int depth)
        {
            if (depth > 64 || !active.Add(node)) throw new FormatException("SVG contains cyclic or excessively deep references.");
            if (complete.Contains(node)) { active.Remove(node); return; }
            foreach (KeyValuePair<string, string> attribute in node.Attributes)
            {
                string value = attribute.Value;
                string? id = value.StartsWith('#') ? value[1..] : ReferencePattern().Match(value) is { Success: true } match ? match.Groups[1].Value : null;
                if (id is not null)
                {
                    // Hex colors are not resource references.
                    if (value.StartsWith('#') && attribute.Key != "href") continue;
                    if (!ids.TryGetValue(id, out SvgNodeData? target)) throw new FormatException($"Unknown SVG reference '#{id}'.");
                    Validate(target, depth + 1);
                }
            }
            foreach (SvgNodeData child in node.Children) Validate(child, depth + 1);
            active.Remove(node);
            complete.Add(node);
        }
        Validate(result, 0);
        return result;
    }

    private static SvgNodeData Read(XElement element, int depth, ref int count, ref int commands)
    {
        if (++count > 10_000 || depth > 64) throw new FormatException("SVG exceeds element or nesting limits.");
        if (element.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg") || !Elements.Contains(element.Name.LocalName)) throw new FormatException($"Unsupported SVG element '{element.Name}'.");
        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) continue;
            string name = attribute.Name.LocalName;
            if (attribute.Name.NamespaceName is not ("" or "http://www.w3.org/1999/xlink")) throw new FormatException($"Unsupported SVG attribute '{attribute.Name}'.");
            if (name == "style")
            {
                foreach (string declaration in attribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int colon = declaration.IndexOf(':');
                    if (colon <= 0) throw new FormatException("Invalid SVG style declaration.");
                    Set(declaration[..colon].Trim(), declaration[(colon + 1)..].Trim(), attributes);
                }
            }
            else Set(name, attribute.Value, attributes);
        }
        ValidateAttributes(attributes);
        IReadOnlyList<PathCommand>? path = BuildPath(element.Name.LocalName, attributes);
        commands += path?.Count ?? 0;
        if (commands > 100_000) throw new FormatException("SVG exceeds 100,000 geometry commands.");
        List<SvgNodeData> children = [];
        foreach (XNode child in element.Nodes())
        {
            if (child is XElement childElement)
            {
                children.Add(Read(childElement, depth + 1, ref count, ref commands));
            }
            else if (child is XText text && element.Name.LocalName is "text" or "tspan" or "title" or "desc")
            {
                if (++count > 10_000)
                {
                    throw new FormatException("SVG exceeds the element and text-node limit.");
                }

                children.Add(new SvgNodeData("#text", [], text.Value, [], null, []));
            }
        }

        return new SvgNodeData(element.Name.LocalName, attributes, string.Empty, [.. children], path, SvgValueParser.Parse(attributes));
    }

    private static void Set(string name, string value, Dictionary<string, string> attributes)
    {
        if (!Attributes.Contains(name) || name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) throw new FormatException($"Unsupported SVG attribute '{name}'.");
        if (name == "href" && (!value.StartsWith('#') || value.Length < 2)) throw new FormatException("Only local SVG fragment references are supported.");
        if (value.Contains("url", StringComparison.OrdinalIgnoreCase) && !ReferencePattern().IsMatch(value)) throw new FormatException("External SVG URLs are not supported.");
        if (value.Contains('\\') || value.Contains('@') || value.Contains("!important", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Unsupported SVG value.");
        attributes[name] = value;
    }

    private static void ValidateAttributes(Dictionary<string, string> attributes)
    {
        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            string name = attribute.Key;
            string value = attribute.Value;
            if (name == "viewBox")
            {
                double[] numbers = NumberList(value);
                if (numbers.Length != 4 || numbers[2] <= 0 || numbers[3] <= 0)
                {
                    throw new FormatException("SVG viewBox requires four finite numbers and positive dimensions.");
                }
            }
            else if (name is "opacity" or "fill-opacity" or "stroke-opacity" or "stop-opacity" or "offset")
            {
                double number = value.EndsWith('%') ? Number(value[..^1]) / 100 : Number(value);
                if (number < 0 || number > 1)
                {
                    throw new FormatException($"SVG {name} must be between 0 and 1.");
                }
            }
            else if (name is "transform" or "gradientTransform")
            {
                int position = 0;
                foreach (Match transform in TransformPattern().Matches(value))
                {
                    if (value[position..transform.Index].Trim(' ', ',', '\t', '\r', '\n').Length != 0)
                    {
                        throw new FormatException("Invalid SVG transform syntax.");
                    }

                    int count = NumberList(transform.Groups[2].Value).Length;
                    bool valid = transform.Groups[1].Value switch
                    {
                        "matrix" => count == 6,
                        "translate" or "scale" => count is 1 or 2,
                        "rotate" => count is 1 or 3,
                        "skewX" or "skewY" => count == 1,
                        _ => false
                    };
                    if (!valid)
                    {
                        throw new FormatException("Unsupported SVG transform or argument count.");
                    }

                    position = transform.Index + transform.Length;
                }

                if (position == 0 || value[position..].Trim().Length != 0)
                {
                    throw new FormatException("Invalid SVG transform syntax.");
                }
            }
            else if (name is "width" or "height" or "r" or "rx" or "ry" or "stroke-width" or "font-size")
            {
                string numberText = LengthUnitPattern().Replace(value, "");
                if (Number(numberText) < 0)
                {
                    throw new FormatException($"SVG {name} must not be negative.");
                }
            }
            else if (name is "x" or "y" or "cx" or "cy" or "fx" or "fy" or "x1" or "x2" or "y1" or "y2" or "dx" or "dy" or "stroke-dashoffset" or "stroke-miterlimit")
            {
                Number(LengthUnitPattern().Replace(value, ""));
            }
            else if (name is "fill-rule" or "clip-rule")
            {
                if (value is not ("nonzero" or "evenodd"))
                {
                    throw new FormatException($"Unsupported SVG {name} value '{value}'.");
                }
            }
        }
    }

    private static double[] NumberList(string value)
    {
        return value.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray();
    }

    [GeneratedRegex(@"([A-Za-z]+)\s*\(([^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex TransformPattern();

    [GeneratedRegex(@"(?:px|in|cm|mm|pt|pc|%)$", RegexOptions.CultureInvariant)]
    private static partial Regex LengthUnitPattern();

    private static IReadOnlyList<PathCommand>? BuildPath(string name, Dictionary<string, string> a)
    {
        double N(string key, double fallback = 0) => a.TryGetValue(key, out string? value) ? Number(value) : fallback;
        string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        double x = N("x"), y = N("y");
        string? data = name switch
        {
            "path" => a.GetValueOrDefault("d", ""),
            "line" => $"M{F(N("x1"))},{F(N("y1"))} L{F(N("x2"))},{F(N("y2"))}",
            "polygon" or "polyline" => "M" + a.GetValueOrDefault("points", "") + (name == "polygon" ? " Z" : ""),
            _ => null
        };
        if (name == "rect")
        {
            double w = N("width"), h = N("height");
            double rx = Math.Clamp(N("rx", N("ry")), 0, Math.Max(0, w / 2));
            double ry = Math.Clamp(N("ry", rx), 0, Math.Max(0, h / 2));
            if (w < 0 || h < 0) throw new FormatException("SVG shape dimensions cannot be negative.");
            data = rx == 0 || ry == 0
                ? $"M{F(x)},{F(y)} h{F(w)} v{F(h)} h{F(-w)} Z"
                : $"M{F(x + rx)},{F(y)} H{F(x+w-rx)} A{F(rx)},{F(ry)} 0 0 1 {F(x+w)},{F(y+ry)} V{F(y+h-ry)} A{F(rx)},{F(ry)} 0 0 1 {F(x+w-rx)},{F(y+h)} H{F(x+rx)} A{F(rx)},{F(ry)} 0 0 1 {F(x)},{F(y+h-ry)} V{F(y+ry)} A{F(rx)},{F(ry)} 0 0 1 {F(x+rx)},{F(y)} Z";
        }
        if (name is "circle" or "ellipse")
        {
            double cx = N("cx"), cy = N("cy"), rx = N(name == "circle" ? "r" : "rx"), ry = N(name == "circle" ? "r" : "ry");
            if (rx < 0 || ry < 0) throw new FormatException("SVG radii cannot be negative.");
            data = $"M{F(cx-rx)},{F(cy)} A{F(rx)},{F(ry)} 0 1 0 {F(cx+rx)},{F(cy)} A{F(rx)},{F(ry)} 0 1 0 {F(cx-rx)},{F(cy)} Z";
        }
        return data is null ? null : PathDataParser.Parse(data);
    }

    internal static double Number(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) || !double.IsFinite(result) || Math.Abs(result) > 1e9) throw new FormatException($"Invalid SVG number '{value}'.");
        return result;
    }

    [GeneratedRegex("^url\\(\\s*['\"]?#([^\\s)'\"]+)['\"]?\\s*\\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
