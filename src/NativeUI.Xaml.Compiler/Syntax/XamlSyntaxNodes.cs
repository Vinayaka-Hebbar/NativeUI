namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Represents a 1-based XML line and column location in a XAML source file.
/// </summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Column">The 1-based column number.</param>
public readonly record struct XamlSourcePosition(int Line, int Column);

/// <summary>
/// Abstract base class for all nodes in the XAML abstract syntax tree.
/// </summary>
/// <param name="Position">The 1-based source position of the node.</param>
public abstract record XamlSyntaxNode(XamlSourcePosition Position)
{
    /// <summary>
    /// Accepts an <see cref="IXamlSyntaxVisitor"/>.
    /// </summary>
    public abstract void Accept(IXamlSyntaxVisitor visitor);
}

/// <summary>
/// Represents a parsed <c>{Binding ...}</c> markup expression AST node.
/// </summary>
public sealed record BindingExpressionNode(
    string Path,
    string Mode,
    XamlSourcePosition Position) : XamlSyntaxNode(Position)
{
    /// <inheritdoc />
    public override void Accept(IXamlSyntaxVisitor visitor) => visitor.VisitBindingExpression(this);
}

/// <summary>
/// Represents a parsed XAML attribute on an element.
/// </summary>
public sealed record XamlAttributeNode(
    string Prefix,
    string LocalName,
    string NamespaceUri,
    string Value,
    XamlSourcePosition Position) : XamlSyntaxNode(Position)
{
    /// <inheritdoc />
    public override void Accept(IXamlSyntaxVisitor visitor) => visitor.VisitAttribute(this);
}

/// <summary>
/// Represents a parsed XAML element node and its children.
/// </summary>
public sealed record XamlElementNode(
    string LocalName,
    string NamespaceUri,
    XamlSourcePosition Position,
    IReadOnlyList<XamlAttributeNode> Attributes,
    IReadOnlyList<XamlElementNode> Children,
    string? InnerText = null) : XamlSyntaxNode(Position)
{
    /// <inheritdoc />
    public override void Accept(IXamlSyntaxVisitor visitor) => visitor.VisitElement(this);
}

/// <summary>
/// Represents the root syntax tree of a parsed XAML file.
/// </summary>
public sealed record XamlDocumentNode(
    string FilePath,
    XamlElementNode? RootElement,
    XamlSourcePosition Position,
    (string Message, XamlSourcePosition Position)? XmlError = null) : XamlSyntaxNode(Position)
{
    /// <inheritdoc />
    public override void Accept(IXamlSyntaxVisitor visitor) => visitor.VisitDocument(this);
}
