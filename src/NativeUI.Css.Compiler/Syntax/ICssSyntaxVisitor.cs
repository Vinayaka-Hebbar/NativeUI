namespace NativeUI.Css.Compiler;

/// <summary>
/// Defines a visitor over the CSS abstract syntax tree (<see cref="CssSyntaxNode"/>).
/// </summary>
public interface ICssSyntaxVisitor
{
    /// <summary>Visits a <see cref="CssStylesheetNode"/>.</summary>
    void VisitStylesheet(CssStylesheetNode node);

    /// <summary>Visits a <see cref="CssStyleRuleNode"/>.</summary>
    void VisitStyleRule(CssStyleRuleNode node);

    /// <summary>Visits a <see cref="CssMediaRuleNode"/>.</summary>
    void VisitMediaRule(CssMediaRuleNode node);

    /// <summary>Visits a <see cref="CssSelectorNode"/>.</summary>
    void VisitSelector(CssSelectorNode node);

    /// <summary>Visits a <see cref="CssDeclarationNode"/>.</summary>
    void VisitDeclaration(CssDeclarationNode node);

    /// <summary>Visits a <see cref="CssKeywordValueNode"/>.</summary>
    void VisitKeywordValue(CssKeywordValueNode node);

    /// <summary>Visits a <see cref="CssNumberValueNode"/>.</summary>
    void VisitNumberValue(CssNumberValueNode node);

    /// <summary>Visits a <see cref="CssDimensionValueNode"/>.</summary>
    void VisitDimensionValue(CssDimensionValueNode node);

    /// <summary>Visits a <see cref="CssPercentageValueNode"/>.</summary>
    void VisitPercentageValue(CssPercentageValueNode node);

    /// <summary>Visits a <see cref="CssHexColorValueNode"/>.</summary>
    void VisitHexColorValue(CssHexColorValueNode node);

    /// <summary>Visits a <see cref="CssStringValueNode"/>.</summary>
    void VisitStringValue(CssStringValueNode node);

    /// <summary>Visits a <see cref="CssFunctionValueNode"/>.</summary>
    void VisitFunctionValue(CssFunctionValueNode node);

    /// <summary>Visits a <see cref="CssValueListNode"/>.</summary>
    void VisitValueList(CssValueListNode node);
}

/// <summary>
/// Defines a generic value-returning visitor over the CSS abstract syntax tree (<see cref="CssSyntaxNode"/>).
/// </summary>
/// <typeparam name="TResult">The return type of each visit operation.</typeparam>
public interface ICssSyntaxVisitor<out TResult>
{
    /// <summary>Visits a <see cref="CssStylesheetNode"/>.</summary>
    TResult VisitStylesheet(CssStylesheetNode node);

    /// <summary>Visits a <see cref="CssStyleRuleNode"/>.</summary>
    TResult VisitStyleRule(CssStyleRuleNode node);

    /// <summary>Visits a <see cref="CssMediaRuleNode"/>.</summary>
    TResult VisitMediaRule(CssMediaRuleNode node);

    /// <summary>Visits a <see cref="CssSelectorNode"/>.</summary>
    TResult VisitSelector(CssSelectorNode node);

    /// <summary>Visits a <see cref="CssDeclarationNode"/>.</summary>
    TResult VisitDeclaration(CssDeclarationNode node);

    /// <summary>Visits a <see cref="CssKeywordValueNode"/>.</summary>
    TResult VisitKeywordValue(CssKeywordValueNode node);

    /// <summary>Visits a <see cref="CssNumberValueNode"/>.</summary>
    TResult VisitNumberValue(CssNumberValueNode node);

    /// <summary>Visits a <see cref="CssDimensionValueNode"/>.</summary>
    TResult VisitDimensionValue(CssDimensionValueNode node);

    /// <summary>Visits a <see cref="CssPercentageValueNode"/>.</summary>
    TResult VisitPercentageValue(CssPercentageValueNode node);

    /// <summary>Visits a <see cref="CssHexColorValueNode"/>.</summary>
    TResult VisitHexColorValue(CssHexColorValueNode node);

    /// <summary>Visits a <see cref="CssStringValueNode"/>.</summary>
    TResult VisitStringValue(CssStringValueNode node);

    /// <summary>Visits a <see cref="CssFunctionValueNode"/>.</summary>
    TResult VisitFunctionValue(CssFunctionValueNode node);

    /// <summary>Visits a <see cref="CssValueListNode"/>.</summary>
    TResult VisitValueList(CssValueListNode node);
}

/// <summary>
/// Base class for depth-first traversal of a <see cref="CssSyntaxNode"/> tree.
/// </summary>
public abstract class CssSyntaxVisitor : ICssSyntaxVisitor
{
    /// <inheritdoc />
    public virtual void VisitStylesheet(CssStylesheetNode node)
    {
        foreach (CssStatementNode statement in node.Statements)
        {
            statement.Accept(this);
        }
    }

    /// <inheritdoc />
    public virtual void VisitStyleRule(CssStyleRuleNode node)
    {
        foreach (CssSelectorNode selector in node.Selectors)
        {
            selector.Accept(this);
        }

        foreach (CssDeclarationNode declaration in node.Declarations)
        {
            declaration.Accept(this);
        }
    }

    /// <inheritdoc />
    public virtual void VisitMediaRule(CssMediaRuleNode node)
    {
        foreach (CssStyleRuleNode rule in node.Rules)
        {
            rule.Accept(this);
        }
    }

    /// <inheritdoc />
    public virtual void VisitSelector(CssSelectorNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitDeclaration(CssDeclarationNode node)
    {
        node.Value.Accept(this);
    }

    /// <inheritdoc />
    public virtual void VisitKeywordValue(CssKeywordValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitNumberValue(CssNumberValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitDimensionValue(CssDimensionValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitPercentageValue(CssPercentageValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitHexColorValue(CssHexColorValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitStringValue(CssStringValueNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitFunctionValue(CssFunctionValueNode node)
    {
        foreach (CssValueSyntaxNode arg in node.Arguments)
        {
            arg.Accept(this);
        }

        node.SlashArgument?.Accept(this);
    }

    /// <inheritdoc />
    public virtual void VisitValueList(CssValueListNode node)
    {
        foreach (CssValueSyntaxNode item in node.Items)
        {
            item.Accept(this);
        }
    }
}
