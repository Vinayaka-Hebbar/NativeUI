namespace NativeUI.Css.Compiler;

/// <summary>
/// Specifies the color scheme media query condition parsed for a CSS rule.
/// </summary>
public enum CssParsedColorScheme
{
    /// <summary>
    /// Applies to all color schemes.
    /// </summary>
    Any,

    /// <summary>
    /// Applies only under <c>@media (prefers-color-scheme: light)</c>.
    /// </summary>
    Light,

    /// <summary>
    /// Applies only under <c>@media (prefers-color-scheme: dark)</c>.
    /// </summary>
    Dark
}

/// <summary>
/// Abstract base class for all nodes in the CSS abstract syntax tree.
/// </summary>
/// <param name="Span">The source span of the syntax node.</param>
public abstract record CssSyntaxNode(CssSourceSpan Span)
{
    /// <summary>
    /// Accepts a non-returning <see cref="ICssSyntaxVisitor"/>.
    /// </summary>
    public abstract void Accept(ICssSyntaxVisitor visitor);

    /// <summary>
    /// Accepts a value-returning <see cref="ICssSyntaxVisitor{TResult}"/>.
    /// </summary>
    public abstract TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor);
}

/// <summary>
/// Base class for top-level or nested CSS statements (style rules and at-rules).
/// </summary>
public abstract record CssStatementNode(CssSourceSpan Span) : CssSyntaxNode(Span);

/// <summary>
/// Represents the root syntax tree of a parsed CSS stylesheet.
/// </summary>
public sealed record CssStylesheetNode(
    string FilePath,
    string RawCss,
    IReadOnlyList<CssStatementNode> Statements,
    IReadOnlyList<(string Message, CssSourceSpan Span)> SyntaxErrors,
    CssSourceSpan Span) : CssSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitStylesheet(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitStylesheet(this);
}

/// <summary>
/// Identifies the part of an element a parsed selector styles.
/// </summary>
public enum CssParsedPseudoElement
{
    /// <summary>
    /// The selector styles the element itself.
    /// </summary>
    None,

    /// <summary>
    /// The selector ends with <c>::selection</c>.
    /// </summary>
    Selection
}

/// <summary>
/// Represents one compound part of a selector, such as <c>text.title:hover</c>.
/// </summary>
/// <param name="ElementType">The required lowercase element type, or <see langword="null"/> if unconstrained.</param>
/// <param name="Classes">The required class names.</param>
/// <param name="RequiredStates">The <c>ElementStates</c> member names required by the part's pseudo-classes.</param>
public sealed record CssCompoundSelectorNode(
    string? ElementType,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> RequiredStates);

/// <summary>
/// Represents a parsed CSS selector (such as <c>:root</c>, <c>text</c>, <c>.title:hover</c>, or <c>.card .title::selection</c>).
/// <see cref="ElementType"/>, <see cref="Classes"/>, and <see cref="RequiredStates"/> describe the selector's rightmost compound part.
/// </summary>
/// <param name="RawText">The selector source text.</param>
/// <param name="ElementType">The required element type of the subject, or <see langword="null"/> if unconstrained.</param>
/// <param name="Classes">The required class names of the subject.</param>
/// <param name="RequiredStates">The <c>ElementStates</c> member names required on the subject.</param>
/// <param name="Ancestors">The compound parts required on ancestors of the subject, outermost first.</param>
/// <param name="PseudoElement">The part of the element the selector styles.</param>
/// <param name="TypeSelectorSpans">The source spans of the element type names in the selector.</param>
/// <param name="IsRoot">Whether the selector is <c>:root</c>.</param>
/// <param name="IsValid">Whether the selector uses only supported syntax.</param>
/// <param name="Specificity">The specificity of the complete selector.</param>
/// <param name="Span">The source span of the selector.</param>
public sealed record CssSelectorNode(
    string RawText,
    string? ElementType,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> RequiredStates,
    IReadOnlyList<CssCompoundSelectorNode> Ancestors,
    CssParsedPseudoElement PseudoElement,
    IReadOnlyList<CssSourceSpan> TypeSelectorSpans,
    bool IsRoot,
    bool IsValid,
    int Specificity,
    CssSourceSpan Span) : CssSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitSelector(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitSelector(this);
}

/// <summary>
/// Represents a parsed CSS style rule block with one or more selectors and declarations.
/// </summary>
public sealed record CssStyleRuleNode(
    IReadOnlyList<CssSelectorNode> Selectors,
    IReadOnlyList<CssDeclarationNode> Declarations,
    CssSourceSpan Span) : CssStatementNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitStyleRule(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitStyleRule(this);
}

/// <summary>
/// Represents a parsed <c>@media (...)</c> block containing nested style rules.
/// </summary>
public sealed record CssMediaRuleNode(
    string FeatureName,
    string FeatureValue,
    CssParsedColorScheme ColorScheme,
    bool IsSupportedQuery,
    IReadOnlyList<CssStyleRuleNode> Rules,
    CssSourceSpan QuerySpan,
    CssSourceSpan Span) : CssStatementNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitMediaRule(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitMediaRule(this);
}

/// <summary>
/// Represents a parsed CSS property or custom variable declaration (<c>property: value;</c>).
/// </summary>
public sealed record CssDeclarationNode(
    string PropertyName,
    CssSourceSpan PropertySpan,
    CssValueSyntaxNode Value,
    CssSourceSpan Span) : CssSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitDeclaration(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitDeclaration(this);
}

/// <summary>
/// Abstract base class for all parsed CSS declaration value AST nodes.
/// </summary>
public abstract record CssValueSyntaxNode(CssSourceSpan Span) : CssSyntaxNode(Span);

/// <summary>
/// Represents an identifier/keyword CSS value node (such as <c>bold</c>, <c>center</c>, or <c>--color-primary</c>).
/// </summary>
public sealed record CssKeywordValueNode(string Keyword, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitKeywordValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitKeywordValue(this);
}

/// <summary>
/// Represents a unitless number CSS value node (such as <c>700</c> or <c>0.65</c>).
/// </summary>
public sealed record CssNumberValueNode(double Value, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitNumberValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitNumberValue(this);
}

/// <summary>
/// Represents a dimension CSS value node with a number and unit (such as <c>24px</c>, <c>1.5rem</c>, or <c>250deg</c>).
/// </summary>
public sealed record CssDimensionValueNode(double Value, string Unit, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitDimensionValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitDimensionValue(this);
}

/// <summary>
/// Represents a percentage CSS value node (such as <c>91%</c>).
/// </summary>
public sealed record CssPercentageValueNode(double Value, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitPercentageValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitPercentageValue(this);
}

/// <summary>
/// Represents a hexadecimal color literal CSS value node (such as <c>#2563eb</c>).
/// </summary>
public sealed record CssHexColorValueNode(string Hex, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitHexColorValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitHexColorValue(this);
}

/// <summary>
/// Represents a quoted string literal CSS value node.
/// </summary>
public sealed record CssStringValueNode(string Value, CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitStringValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitStringValue(this);
}

/// <summary>
/// Represents a CSS function invocation node such as <c>oklch(0.65 0.2 250 / 0.8)</c>, <c>hsl(217 91% 60%)</c>,
/// or <c>var(--color-primary, #2563eb)</c>.
/// </summary>
public sealed record CssFunctionValueNode(
    string Name,
    IReadOnlyList<CssValueSyntaxNode> Arguments,
    CssValueSyntaxNode? SlashArgument,
    CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitFunctionValue(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitFunctionValue(this);
}

/// <summary>
/// Represents a space- or comma-separated list of CSS value nodes (such as <c>padding: 12px 24px</c>).
/// </summary>
public sealed record CssValueListNode(
    IReadOnlyList<CssValueSyntaxNode> Items,
    CssSourceSpan Span) : CssValueSyntaxNode(Span)
{
    /// <inheritdoc />
    public override void Accept(ICssSyntaxVisitor visitor) => visitor.VisitValueList(this);

    /// <inheritdoc />
    public override TResult Accept<TResult>(ICssSyntaxVisitor<TResult> visitor) => visitor.VisitValueList(this);
}
