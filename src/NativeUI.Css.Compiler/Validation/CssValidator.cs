using System.Text;
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
    CssParsedColorScheme ColorScheme,
    IReadOnlyList<ValidatedCssDeclaration> Declarations,
    IReadOnlyList<string>? RequiredStates = null,
    IReadOnlyList<CssCompoundSelectorNode>? Ancestors = null,
    CssParsedPseudoElement PseudoElement = CssParsedPseudoElement.None,
    IReadOnlyList<CssAttributeSelectorNode>? Attributes = null);

/// <summary>
/// Represents the output of <see cref="CssValidator"/> for a CSS file.
/// </summary>
public sealed record ValidatedCssStylesheet(
    string Id,
    string FilePath,
    string RawCss,
    string BrowserCss,
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
        "border-width",
        "border-color",
        "opacity",
        "width",
        "height",
        "cursor",
        "overflow",
        "overflow-x",
        "overflow-y",
        "scrollbar-color",
        "scrollbar-width",
        "position",
        "top",
        "right",
        "bottom",
        "left"
    };

    private readonly string _filePath;
    private readonly string _rawCss;
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly List<ValidatedCssRule> _rules = [];
    private readonly HashSet<string> _definedClasses = new(StringComparer.Ordinal);
    private readonly List<BrowserCssEdit> _browserCssEdits = [];
    private CssParsedColorScheme _currentColorScheme = CssParsedColorScheme.Any;

    private CssValidator(string filePath, string rawCss)
    {
        _filePath = filePath;
        _rawCss = rawCss;
    }

    /// <summary>
    /// Validates the given <paramref name="stylesheet"/> AST using the visitor pattern.
    /// </summary>
    public static ValidatedCssStylesheet Validate(CssStylesheetNode stylesheet, string? stylesheetId = null)
    {
        ArgumentNullException.ThrowIfNull(stylesheet);

        CssValidator validator = new(stylesheet.FilePath, stylesheet.RawCss);
        stylesheet.Accept(validator);

        string id = stylesheetId ?? DeriveStylesheetId(stylesheet.FilePath);
        return new ValidatedCssStylesheet(
            id,
            stylesheet.FilePath,
            stylesheet.RawCss,
            ApplyBrowserCssEdits(stylesheet.RawCss, validator._browserCssEdits),
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

        if (node.ColorScheme != CssParsedColorScheme.Any)
        {
            RemoveMediaBlockDelimiters(node);
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

            if (selector.PseudoElement == CssParsedPseudoElement.Selection)
            {
                ValidateSelectionDeclarations(node.Declarations);
            }

            foreach (string cls in selector.Classes)
            {
                _definedClasses.Add(cls);
            }

            foreach (CssCompoundSelectorNode ancestor in selector.Ancestors)
            {
                _definedClasses.UnionWith(ancestor.Classes);
            }

            AddBrowserSelectorEdits(selector);
            _rules.Add(new ValidatedCssRule(
                selector.RawText,
                selector.ElementType,
                selector.Classes,
                selector.IsRoot,
                _currentColorScheme,
                validDecls,
                selector.RequiredStates,
                selector.Ancestors,
                selector.PseudoElement,
                selector.Attributes));
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

    /// <summary>
    /// Applies edits in source order, leaving all other source text unchanged. An insertion precedes a replacement at the same position.
    /// </summary>
    private static string ApplyBrowserCssEdits(string rawCss, IReadOnlyList<BrowserCssEdit> edits)
    {
        StringBuilder browserCss = new(rawCss.Length);
        int position = 0;
        foreach (BrowserCssEdit edit in edits.OrderBy(static e => e.Start).ThenBy(static e => e.Length))
        {
            browserCss.Append(rawCss, position, edit.Start - position).Append(edit.Replacement);
            position = edit.Start + edit.Length;
        }

        return browserCss.Append(rawCss, position, rawCss.Length - position).ToString();
    }

    /// <summary>
    /// Removes a color-scheme block's <c>@media</c> wrapper from browser CSS. A media query reports only the operating
    /// system preference; <see cref="AddBrowserSelectorEdits"/> gates the block's rules on the root's effective theme instead.
    /// </summary>
    private void RemoveMediaBlockDelimiters(CssMediaRuleNode node)
    {
        int start = node.Span.StartPos;
        if (node.Rules.Count == 0)
        {
            _browserCssEdits.Add(new BrowserCssEdit(start, node.Span.Length, string.Empty));
            return;
        }

        _browserCssEdits.Add(new BrowserCssEdit(start, node.Rules[0].Span.StartPos - start, string.Empty));
        _browserCssEdits.Add(new BrowserCssEdit(start + node.Span.Length - 1, 1, string.Empty));
    }

    /// <summary>
    /// Maps a selector to browser CSS: NativeUI type names become the HTML elements created on Web, and rules in a
    /// color-scheme block match only under a <c>native-ui</c> root with that theme. <c>:where</c> preserves specificity.
    /// </summary>
    private void AddBrowserSelectorEdits(CssSelectorNode selector)
    {
        if (_currentColorScheme != CssParsedColorScheme.Any)
        {
            string themedRoot = _currentColorScheme == CssParsedColorScheme.Dark
                ? "native-ui[data-theme=\"dark\"]"
                : "native-ui[data-theme=\"light\"]";
            _browserCssEdits.Add(selector.IsRoot
                ? new BrowserCssEdit(selector.Span.StartPos, selector.Span.Length, themedRoot)
                : new BrowserCssEdit(selector.Span.StartPos, 0, $":where({themedRoot}) "));
        }

        foreach (CssSourceSpan span in selector.TypeSelectorSpans)
        {
            string? htmlTag = GetHtmlTag(_rawCss.Substring(span.StartPos, span.Length));
            if (htmlTag is not null)
            {
                _browserCssEdits.Add(new BrowserCssEdit(span.StartPos, span.Length, htmlTag));
            }
        }
    }

    private static string? GetHtmlTag(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "text" => "span",
            "view" => "div",
            _ => null
        };
    }

    private void ValidateSelectionDeclarations(IReadOnlyList<CssDeclarationNode> declarations)
    {
        foreach (CssDeclarationNode decl in declarations)
        {
            string propName = decl.PropertyName.ToLowerInvariant();
            if (propName is not ("color" or "background-color"))
            {
                _diagnostics.Add(Diagnostic.Create(
                    CssDiagnostics.UnsupportedSelectionProperty,
                    CssDiagnostics.CreateLocation(_filePath, decl.PropertySpan),
                    decl.PropertyName));
            }
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

    private readonly record struct BrowserCssEdit(int Start, int Length, string Replacement);
}
