using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NativeUI.Css.Compiler;

/// <summary>
/// Defines Roslyn <see cref="DiagnosticDescriptor"/> instances for the NativeUI CSS compiler.
/// </summary>
public static class CssDiagnostics
{
    /// <summary>
    /// Diagnostic reported when a CSS file contains a lexical or block syntax error.
    /// </summary>
    public static readonly DiagnosticDescriptor SyntaxError = new(
        id: "NCSS001",
        title: "CSS syntax error",
        messageFormat: "{0}",
        category: "NativeUI.Css",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when an unsupported CSS property name is used.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedProperty = new(
        id: "NCSS002",
        title: "Unsupported CSS property",
        messageFormat: "The CSS property '{0}' is not supported by NativeUI",
        category: "NativeUI.Css",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when a CSS property has an invalid value expression.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPropertyValue = new(
        id: "NCSS003",
        title: "Invalid CSS property value",
        messageFormat: "The value '{0}' is not valid for CSS property '{1}'",
        category: "NativeUI.Css",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic reported when a CSS selector is malformed or unsupported.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidSelector = new(
        id: "NCSS004",
        title: "Invalid CSS selector",
        messageFormat: "The CSS selector '{0}' is not valid or supported",
        category: "NativeUI.Css",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Creates a Roslyn <see cref="Location"/> from a file path and <see cref="CssSourceSpan"/>.
    /// </summary>
    public static Location CreateLocation(string filePath, CssSourceSpan span)
    {
        TextSpan textSpan = new(span.StartPos, Math.Max(1, span.Length));
        LinePositionSpan lineSpan = new(
            new LinePosition(span.StartLine, span.StartColumn),
            new LinePosition(span.EndLine, Math.Max(span.StartColumn + 1, span.EndColumn)));
        return Location.Create(filePath, textSpan, lineSpan);
    }
}
