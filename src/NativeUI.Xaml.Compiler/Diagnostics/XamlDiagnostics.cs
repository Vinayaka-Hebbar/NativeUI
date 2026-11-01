using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Defines Roslyn <see cref="DiagnosticDescriptor"/> instances for the NativeUI XAML compiler.
/// </summary>
public static class XamlDiagnostics
{
    /// <summary>
    /// Diagnostic reported when a XAML file is not well-formed XML.
    /// </summary>
    public static readonly DiagnosticDescriptor XmlSyntaxError = new(
        id: "NXAML001",
        title: "XAML XML syntax error",
        messageFormat: "{0}",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when an unknown or unsupported XAML control element is encountered.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownElement = new(
        id: "NXAML002",
        title: "Unknown XAML element",
        messageFormat: "The element '<{0}>' is not a recognized NativeUI control",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when an unknown or unsupported property attribute is set on a control.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownProperty = new(
        id: "NXAML003",
        title: "Unknown XAML property",
        messageFormat: "The property '{0}' does not exist on control '<{1}>'",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when a <c>{Binding ...}</c> expression is malformed.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidBindingExpression = new(
        id: "NXAML004",
        title: "Invalid XAML binding expression",
        messageFormat: "{0}",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when the root element of a compiled XAML view is missing <c>x:Class</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingXClass = new(
        id: "NXAML005",
        title: "Missing x:Class attribute",
        messageFormat: "The root element '<{0}>' must declare an x:Class attribute (for example, x:Class=\"MyApp.MainView\")",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when a <c>Class</c> attribute references a CSS class not defined in the paired stylesheet.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownCssClass = new(
        id: "NXAML006",
        title: "Undefined CSS class in XAML",
        messageFormat: "The CSS class '{0}' is not defined in the paired stylesheet for '{1}'",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when an attribute's literal text cannot convert to its property's type.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPropertyValue = new(
        id: "NXAML007",
        title: "Invalid XAML property value",
        messageFormat: "The value '{0}' is not valid for property '{1}' on control '<{2}>'",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when a property element such as <c>&lt;Dialog.Footer&gt;</c> is unknown or used incorrectly.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPropertyElement = new(
        id: "NXAML008",
        title: "Invalid XAML property element",
        messageFormat: "The property element '<{0}>' is not valid here: {1}",
        category: "NativeUI.Xaml",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Creates a Roslyn <see cref="Location"/> from a file path and 1-based <see cref="XamlSourcePosition"/>.
    /// </summary>
    public static Location CreateLocation(string filePath, XamlSourcePosition position)
    {
        int zeroLine = Math.Max(0, position.Line - 1);
        int zeroCol = Math.Max(0, position.Column - 1);
        LinePosition start = new(zeroLine, zeroCol);
        LinePosition end = new(zeroLine, zeroCol + 1);
        return Location.Create(filePath, new TextSpan(0, 0), new LinePositionSpan(start, end));
    }
}
