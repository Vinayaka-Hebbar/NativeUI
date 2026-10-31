using Microsoft.CodeAnalysis;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Represents a validated property assignment or binding on a XAML element.
/// </summary>
public sealed record ValidatedXamlProperty(
    string PropertyName,
    string? LiteralValue,
    BindingExpressionNode? Binding);

/// <summary>
/// Represents a validated XAML element node ready for C# code emission.
/// </summary>
public sealed record ValidatedXamlElement(
    string ControlTypeName,
    string? XName,
    IReadOnlyList<ValidatedXamlProperty> Properties,
    IReadOnlyList<ValidatedXamlElement> Children);

/// <summary>
/// Represents a validated XAML document ready for C# partial class generation.
/// </summary>
public sealed record ValidatedXamlDocument(
    string FilePath,
    string FullClassName,
    string NamespaceName,
    string ClassName,
    string? DataType,
    string StyleSheetId,
    ValidatedXamlElement? RootElement,
    IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// AST visitor (<see cref="XamlSyntaxVisitor"/>) that traverses a <see cref="XamlDocumentNode"/>,
/// validates element tags, property attributes, <c>{Binding}</c> expressions, and CSS class names,
/// and produces a <see cref="ValidatedXamlDocument"/>.
/// </summary>
public sealed class XamlValidator : XamlSyntaxVisitor
{
    private static readonly Dictionary<string, HashSet<string>> s_controlProperties =
        new(StringComparer.Ordinal)
        {
            ["View"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Id",
                "Class",
                "IsVisible"
            },
            ["Text"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Id",
                "Class",
                "IsVisible",
                "Value",
                "AriaLabel"
            }
        };

    private readonly string _filePath;
    private readonly IReadOnlySet<string>? _knownCssClasses;
    private readonly List<Diagnostic> _diagnostics = [];
    private string _fullClassName = string.Empty;
    private string? _dataType;
    private string? _explicitStyleSheetId;
    private ValidatedXamlElement? _validatedRoot;

    private XamlValidator(string filePath, IReadOnlySet<string>? knownCssClasses)
    {
        _filePath = filePath;
        _knownCssClasses = knownCssClasses;
    }

    /// <summary>
    /// Validates the specified <paramref name="document"/> AST using the visitor pattern.
    /// </summary>
    public static ValidatedXamlDocument Validate(
        XamlDocumentNode document,
        IReadOnlySet<string>? knownCssClasses = null,
        string defaultNamespace = "NativeUI")
    {
        ArgumentNullException.ThrowIfNull(document);

        XamlValidator validator = new(document.FilePath, knownCssClasses);
        document.Accept(validator);

        string defaultSheetId = DeriveDefaultStyleSheetId(document.FilePath);
        string styleSheetId = validator._explicitStyleSheetId ?? defaultSheetId;

        (string ns, string className) = SplitFullClassName(validator._fullClassName, defaultSheetId, defaultNamespace);

        return new ValidatedXamlDocument(
            document.FilePath,
            validator._fullClassName,
            ns,
            className,
            validator._dataType,
            styleSheetId,
            validator._validatedRoot,
            validator._diagnostics);
    }

    /// <inheritdoc />
    public override void VisitDocument(XamlDocumentNode node)
    {
        if (node.XmlError is (string message, XamlSourcePosition pos))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.XmlSyntaxError,
                XamlDiagnostics.CreateLocation(_filePath, pos),
                message));
            return;
        }

        if (node.RootElement is null)
        {
            return;
        }

        _validatedRoot = ValidateElementRecursive(node.RootElement, isRoot: true);
    }

    private ValidatedXamlElement? ValidateElementRecursive(XamlElementNode element, bool isRoot)
    {
        if (!string.IsNullOrEmpty(element.NamespaceUri) &&
            !string.Equals(element.NamespaceUri, XamlParser.NativeUINamespace, StringComparison.Ordinal))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.UnknownElement,
                XamlDiagnostics.CreateLocation(_filePath, element.Position),
                element.LocalName));
            return null;
        }

        if (!s_controlProperties.TryGetValue(element.LocalName, out HashSet<string>? allowedProps))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.UnknownElement,
                XamlDiagnostics.CreateLocation(_filePath, element.Position),
                element.LocalName));
            return null;
        }

        string? xName = null;
        List<ValidatedXamlProperty> validProps = [];

        foreach (XamlAttributeNode attr in element.Attributes)
        {
            if (string.Equals(attr.NamespaceUri, XamlParser.XamlDirectiveNamespace, StringComparison.Ordinal) ||
                string.Equals(attr.Prefix, "x", StringComparison.Ordinal))
            {
                switch (attr.LocalName)
                {
                    case "Class" when isRoot:
                        _fullClassName = attr.Value.Trim();
                        break;
                    case "DataType" when isRoot:
                        _dataType = attr.Value.Trim();
                        break;
                    case "Name":
                        xName = attr.Value.Trim();
                        break;
                    default:
                        _diagnostics.Add(Diagnostic.Create(
                            XamlDiagnostics.UnknownProperty,
                            XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                            $"x:{attr.LocalName}",
                            element.LocalName));
                        break;
                }

                continue;
            }

            if (!allowedProps.Contains(attr.LocalName))
            {
                _diagnostics.Add(Diagnostic.Create(
                    XamlDiagnostics.UnknownProperty,
                    XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                    attr.LocalName,
                    element.LocalName));
                continue;
            }

            if (isRoot && string.Equals(attr.LocalName, "StyleSheetId", StringComparison.Ordinal))
            {
                _explicitStyleSheetId = attr.Value.Trim();
            }

            if (string.Equals(attr.LocalName, "Class", StringComparison.Ordinal) &&
                _knownCssClasses is not null)
            {
                ValidateCssClassTokens(attr.Value, attr.Position);
            }

            if (BindingExpressionParser.IsMarkupExtension(attr.Value))
            {
                if (!BindingExpressionParser.TryParse(
                    attr.Value,
                    attr.Position,
                    out BindingExpressionNode? bindingNode,
                    out string? errorMessage) || bindingNode is null)
                {
                    _diagnostics.Add(Diagnostic.Create(
                        XamlDiagnostics.InvalidBindingExpression,
                        XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                        errorMessage ?? "Invalid binding expression."));
                    continue;
                }

                validProps.Add(new ValidatedXamlProperty(attr.LocalName, null, bindingNode));
            }
            else
            {
                validProps.Add(new ValidatedXamlProperty(attr.LocalName, attr.Value, null));
            }
        }

        // Support <Text>Hello</Text> inner text syntax when Value attribute is not explicitly provided.
        if (string.Equals(element.LocalName, "Text", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(element.InnerText) &&
            !validProps.Any(static p => p.PropertyName == "Value"))
        {
            validProps.Add(new ValidatedXamlProperty("Value", element.InnerText, null));
        }

        if (isRoot && string.IsNullOrWhiteSpace(_fullClassName))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.MissingXClass,
                XamlDiagnostics.CreateLocation(_filePath, element.Position),
                element.LocalName));
        }

        List<ValidatedXamlElement> validChildren = [];
        foreach (XamlElementNode child in element.Children)
        {
            ValidatedXamlElement? validatedChild = ValidateElementRecursive(child, isRoot: false);
            if (validatedChild is not null)
            {
                validChildren.Add(validatedChild);
            }
        }

        return new ValidatedXamlElement(element.LocalName, xName, validProps, validChildren);
    }

    private void ValidateCssClassTokens(string classAttributeValue, XamlSourcePosition position)
    {
        if (_knownCssClasses is null)
        {
            return;
        }

        string[] tokens = classAttributeValue.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        string viewId = DeriveDefaultStyleSheetId(_filePath);
        foreach (string cls in tokens)
        {
            if (!_knownCssClasses.Contains(cls))
            {
                _diagnostics.Add(Diagnostic.Create(
                    XamlDiagnostics.UnknownCssClass,
                    XamlDiagnostics.CreateLocation(_filePath, position),
                    cls,
                    viewId));
            }
        }
    }

    private static string DeriveDefaultStyleSheetId(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        if (fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^5];
        }

        return fileName;
    }

    private static (string NamespaceName, string ClassName) SplitFullClassName(
        string fullClassName,
        string fallbackName,
        string defaultNamespace)
    {
        if (string.IsNullOrWhiteSpace(fullClassName))
        {
            return (defaultNamespace, fallbackName);
        }

        int lastDot = fullClassName.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return (defaultNamespace, fullClassName);
        }

        return (fullClassName[..lastDot], fullClassName[(lastDot + 1)..]);
    }
}
