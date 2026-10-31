using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NativeUI.Css.Compiler;

/// <summary>
/// Represents a validated CSS declaration with a Roslyn <see cref="ExpressionSyntax"/> ready for syntax tree emission.
/// </summary>
public sealed record ValidatedCssDeclaration(
    string PropertyName,
    ExpressionSyntax ValueExpression,
    ParsedCssColor? ColorLiteral = null);

/// <summary>
/// Represents a validated CSS rule ready for Roslyn syntax emission.
/// </summary>
public sealed record ValidatedCssRule(
    string Selector,
    string? ElementType,
    IReadOnlyList<string> Classes,
    bool IsRoot,
    int Specificity,
    CssParsedColorScheme ColorScheme,
    IReadOnlyList<ValidatedCssDeclaration> Declarations);

/// <summary>
/// Represents the output of <see cref="CssValidator"/> for a CSS file.
/// </summary>
public sealed record ValidatedCssStylesheet(
    string Id,
    string FilePath,
    string RawCss,
    IReadOnlyList<ValidatedCssRule> Rules,
    IReadOnlySet<string> DefinedClasses,
    IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// AST visitor (<see cref="CssSyntaxVisitor"/>) that traverses a <see cref="CssStylesheetNode"/>,
/// validates selectors, media queries, property names, and <see cref="CssValueSyntaxNode"/> values,
/// and builds Roslyn <see cref="ExpressionSyntax"/> nodes for <see cref="CssEmitter"/>.
/// </summary>
public sealed class CssValidator : CssSyntaxVisitor
{
    private static readonly HashSet<string> s_supportedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "color",
        "background-color",
        "background",
        "font-size",
        "font-weight",
        "font-family",
        "text-align",
        "padding",
        "margin",
        "border-radius",
        "opacity",
        "width",
        "height"
    };

    private readonly string _filePath;
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly List<ValidatedCssRule> _rules = [];
    private readonly HashSet<string> _definedClasses = new(StringComparer.Ordinal);
    private CssParsedColorScheme _currentColorScheme = CssParsedColorScheme.Any;

    private CssValidator(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Validates the given <paramref name="stylesheet"/> AST using the visitor pattern.
    /// </summary>
    public static ValidatedCssStylesheet Validate(CssStylesheetNode stylesheet, string? stylesheetId = null)
    {
        ArgumentNullException.ThrowIfNull(stylesheet);

        CssValidator validator = new(stylesheet.FilePath);
        stylesheet.Accept(validator);

        string id = stylesheetId ?? DeriveStylesheetId(stylesheet.FilePath);
        return new ValidatedCssStylesheet(
            id,
            stylesheet.FilePath,
            stylesheet.RawCss,
            validator._rules,
            validator._definedClasses,
            validator._diagnostics);
    }

    /// <summary>
    /// Derives the canonical stylesheet identifier from its file path.
    /// </summary>
    public static string DeriveStylesheetId(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        if (fileName.EndsWith(".xaml.css", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^9];
        }

        if (fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^4];
        }

        return fileName;
    }

    /// <inheritdoc />
    public override void VisitStylesheet(CssStylesheetNode node)
    {
        foreach ((string message, CssSourceSpan span) in node.SyntaxErrors)
        {
            _diagnostics.Add(Diagnostic.Create(
                CssDiagnostics.SyntaxError,
                CssDiagnostics.CreateLocation(_filePath, span),
                message));
        }

        base.VisitStylesheet(node);
    }

    /// <inheritdoc />
    public override void VisitMediaRule(CssMediaRuleNode node)
    {
        if (!node.IsSupportedQuery)
        {
            _diagnostics.Add(Diagnostic.Create(
                CssDiagnostics.SyntaxError,
                CssDiagnostics.CreateLocation(_filePath, node.QuerySpan),
                $"Unsupported @media query '({node.FeatureName}: {node.FeatureValue})'."));
            return;
        }

        CssParsedColorScheme previous = _currentColorScheme;
        _currentColorScheme = node.ColorScheme;
        base.VisitMediaRule(node);
        _currentColorScheme = previous;
    }

    /// <inheritdoc />
    public override void VisitStyleRule(CssStyleRuleNode node)
    {
        List<ValidatedCssDeclaration> validDecls = [];
        foreach (CssDeclarationNode decl in node.Declarations)
        {
            if (TryValidateDeclaration(decl, out ValidatedCssDeclaration? validated) && validated is not null)
            {
                validDecls.Add(validated);
            }
        }

        foreach (CssSelectorNode selector in node.Selectors)
        {
            selector.Accept(this);
            if (!selector.IsValid)
            {
                continue;
            }

            foreach (string cls in selector.Classes)
            {
                _definedClasses.Add(cls);
            }

            _rules.Add(new ValidatedCssRule(
                selector.RawText,
                selector.ElementType,
                selector.Classes,
                selector.IsRoot,
                selector.Specificity,
                _currentColorScheme,
                validDecls));
        }
    }

    /// <inheritdoc />
    public override void VisitSelector(CssSelectorNode node)
    {
        if (!node.IsValid)
        {
            _diagnostics.Add(Diagnostic.Create(
                CssDiagnostics.InvalidSelector,
                CssDiagnostics.CreateLocation(_filePath, node.Span),
                node.RawText));
        }
    }

    private bool TryValidateDeclaration(CssDeclarationNode decl, out ValidatedCssDeclaration? validated)
    {
        validated = null;
        string propName = decl.PropertyName.StartsWith("--", StringComparison.Ordinal)
            ? decl.PropertyName
            : decl.PropertyName.ToLowerInvariant();

        if (!propName.StartsWith("--", StringComparison.Ordinal) &&
            !s_supportedProperties.Contains(propName))
        {
            _diagnostics.Add(Diagnostic.Create(
                CssDiagnostics.UnsupportedProperty,
                CssDiagnostics.CreateLocation(_filePath, decl.PropertySpan),
                decl.PropertyName));
            return false;
        }

        if (!CssValueSyntaxBuilder.TryBuildValueExpression(
            propName,
            decl.Value,
            out ExpressionSyntax? valueExpr,
            out ParsedCssColor? colorLiteral) || valueExpr is null)
        {
            _diagnostics.Add(Diagnostic.Create(
                CssDiagnostics.InvalidPropertyValue,
                CssDiagnostics.CreateLocation(_filePath, decl.Value.Span),
                CssValueSyntaxBuilder.FormatValueSummary(decl.Value),
                decl.PropertyName));
            return false;
        }

        validated = new ValidatedCssDeclaration(propName, valueExpr, colorLiteral);
        return true;
    }
}
