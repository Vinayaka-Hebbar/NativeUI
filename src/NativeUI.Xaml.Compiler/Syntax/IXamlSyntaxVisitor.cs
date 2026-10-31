namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Defines a visitor over the XAML abstract syntax tree (<see cref="XamlSyntaxNode"/>).
/// </summary>
public interface IXamlSyntaxVisitor
{
    /// <summary>Visits a <see cref="XamlDocumentNode"/>.</summary>
    void VisitDocument(XamlDocumentNode node);

    /// <summary>Visits a <see cref="XamlElementNode"/>.</summary>
    void VisitElement(XamlElementNode node);

    /// <summary>Visits a <see cref="XamlAttributeNode"/>.</summary>
    void VisitAttribute(XamlAttributeNode node);

    /// <summary>Visits a <see cref="BindingExpressionNode"/>.</summary>
    void VisitBindingExpression(BindingExpressionNode node);
}

/// <summary>
/// Base class for depth-first traversal of a <see cref="XamlSyntaxNode"/> AST.
/// </summary>
public abstract class XamlSyntaxVisitor : IXamlSyntaxVisitor
{
    /// <inheritdoc />
    public virtual void VisitDocument(XamlDocumentNode node)
    {
        node.RootElement?.Accept(this);
    }

    /// <inheritdoc />
    public virtual void VisitElement(XamlElementNode node)
    {
        foreach (XamlAttributeNode attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        foreach (XamlElementNode child in node.Children)
        {
            child.Accept(this);
        }
    }

    /// <inheritdoc />
    public virtual void VisitAttribute(XamlAttributeNode node)
    {
    }

    /// <inheritdoc />
    public virtual void VisitBindingExpression(BindingExpressionNode node)
    {
    }
}
