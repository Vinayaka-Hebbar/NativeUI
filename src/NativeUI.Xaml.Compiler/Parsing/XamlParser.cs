using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Parses a XAML XML document into a <see cref="XamlDocumentNode"/> syntax tree with source line/column positions.
/// </summary>
public static class XamlParser
{
    /// <summary>
    /// The canonical default XML namespace for NativeUI XAML controls.
    /// </summary>
    public const string NativeUINamespace = "urn:nativeui";

    /// <summary>
    /// The standard XAML directive XML namespace (<c>xmlns:x</c>).
    /// </summary>
    public const string XamlDirectiveNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Parses the specified <paramref name="xamlText"/> from <paramref name="filePath"/>.
    /// </summary>
    public static XamlDocumentNode Parse(string filePath, string xamlText)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(xamlText);

        try
        {
            XDocument doc = XDocument.Parse(xamlText, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            if (doc.Root is null)
            {
                XamlSourcePosition defaultPos = new(1, 1);
                return new XamlDocumentNode(filePath, null, defaultPos, ("XAML document has no root element.", defaultPos));
            }

            XamlElementNode root = ConvertElement(doc.Root);
            return new XamlDocumentNode(filePath, root, root.Position);
        }
        catch (XmlException ex)
        {
            XamlSourcePosition pos = new(Math.Max(1, ex.LineNumber), Math.Max(1, ex.LinePosition));
            return new XamlDocumentNode(filePath, null, pos, (ex.Message, pos));
        }
    }

    private static XamlElementNode ConvertElement(XElement element)
    {
        XamlSourcePosition pos = GetPosition(element);
        List<XamlAttributeNode> attributes = [];

        foreach (XAttribute attr in element.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                continue;
            }

            string prefix = element.GetPrefixOfNamespace(attr.Name.Namespace) ?? string.Empty;
            attributes.Add(new XamlAttributeNode(
                prefix,
                attr.Name.LocalName,
                attr.Name.NamespaceName,
                attr.Value,
                GetPosition(attr)));
        }

        List<XamlElementNode> children = [];
        bool inlineContent = element.Name.LocalName is "Span" or "Hyperlink" ||
            (element.Name.LocalName == "Text" && element.Elements().Any(static child => !child.Name.LocalName.Contains('.')));
        foreach (XNode child in element.Nodes())
        {
            if (child is XElement childElement)
            {
                children.Add(ConvertElement(childElement));
                continue;
            }

            if (!inlineContent || child is not XText text ||
                (string.IsNullOrWhiteSpace(text.Value) &&
                 (text.Value.Contains('\n') || text.Value.Contains('\r'))))
            {
                continue;
            }

            string value = Regex.Replace(text.Value, @"\s+", " ");
            if (child.PreviousNode is null || child.PreviousNode is XElement { Name.LocalName: "LineBreak" })
            {
                value = value.TrimStart();
            }

            if (child.NextNode is null || child.NextNode is XElement { Name.LocalName: "LineBreak" })
            {
                value = value.TrimEnd();
            }

            XamlSourcePosition textPosition = GetPosition(text);
            children.Add(new XamlElementNode("Run", NativeUINamespace, textPosition,
                [new XamlAttributeNode(string.Empty, "Value", string.Empty, value, textPosition)], []));
        }

        string? innerText = null;
        if (!inlineContent && children.Count == 0 && !element.IsEmpty)
        {
            string rawText = element.Value;
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                innerText = rawText.Trim();
            }
        }

        return new XamlElementNode(
            element.Name.LocalName,
            element.Name.NamespaceName,
            pos,
            attributes,
            children,
            innerText)
        {
            NamespacePrefixes = ReadNamespacePrefixes(element),
            SvgMarkup = element.Name.NamespaceName == "http://www.w3.org/2000/svg" ||
                (element.Name.LocalName == "Svg" && element.Elements().Any(static child => child.Name.NamespaceName == "http://www.w3.org/2000/svg"))
                ? element.ToString(SaveOptions.DisableFormatting)
                : null
        };
    }

    /// <summary>
    /// Returns the namespace of each prefix in scope on <paramref name="element"/>. A declaration on an element replaces
    /// a declaration of the same prefix on its ancestors.
    /// </summary>
    private static Dictionary<string, string> ReadNamespacePrefixes(XElement element)
    {
        Dictionary<string, string> prefixes = new(StringComparer.Ordinal);
        foreach (XElement scope in element.AncestorsAndSelf())
        {
            foreach (XAttribute attribute in scope.Attributes())
            {
                if (attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.Xmlns)
                {
                    prefixes.TryAdd(attribute.Name.LocalName, attribute.Value);
                }
            }
        }

        return prefixes;
    }

    private static XamlSourcePosition GetPosition(IXmlLineInfo lineInfo)
    {
        if (lineInfo.HasLineInfo())
        {
            return new XamlSourcePosition(Math.Max(1, lineInfo.LineNumber), Math.Max(1, lineInfo.LinePosition));
        }

        return new XamlSourcePosition(1, 1);
    }
}
