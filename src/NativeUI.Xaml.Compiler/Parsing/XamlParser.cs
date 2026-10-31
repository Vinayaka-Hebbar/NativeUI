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
            XDocument doc = XDocument.Parse(xamlText, LoadOptions.SetLineInfo);
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
        foreach (XElement child in element.Elements())
        {
            children.Add(ConvertElement(child));
        }

        string? innerText = null;
        if (children.Count == 0 && !element.IsEmpty)
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
            innerText);
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
