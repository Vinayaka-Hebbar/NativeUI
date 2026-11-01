using System.Globalization;
using Microsoft.CodeAnalysis;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Identifies how a XAML attribute's literal text converts to its control property's value.
/// </summary>
public enum XamlPropertyKind
{
    /// <summary>
    /// The literal is assigned as a string, whatever its text.
    /// </summary>
    String,

    /// <summary>
    /// The literal must be <c>true</c> or <c>false</c>, in any letter case.
    /// </summary>
    Boolean,

    /// <summary>
    /// The literal must be an invariant-culture integer in the range of <see cref="int"/>.
    /// </summary>
    Int32,

    /// <summary>
    /// The literal must be an invariant-culture integer in the range of <see cref="long"/>.
    /// </summary>
    Int64,

    /// <summary>
    /// The literal must be a finite invariant-culture number in the range of <see cref="double"/>.
    /// </summary>
    Double,

    /// <summary>
    /// The literal must be <c>Vertical</c> or <c>Horizontal</c>.
    /// </summary>
    Orientation,

    /// <summary>
    /// The literal is a comma-separated list of <c>Auto</c>, <c>*</c>, <c>2*</c>, or a number of pixels.
    /// </summary>
    GridLengthList,

    /// <summary>
    /// The literal must be <c>Top</c>, <c>Right</c>, <c>Bottom</c>, or <c>Left</c>.
    /// </summary>
    PortalSide,

    /// <summary>
    /// The literal must be <c>Start</c>, <c>Center</c>, or <c>End</c>.
    /// </summary>
    Alignment,

    /// <summary>
    /// The literal is one, two (vertical, horizontal), or four (top, right, bottom, left) comma-separated numbers,
    /// assigned to the element's inline style. It has no binding form.
    /// </summary>
    StyleThickness,

    /// <summary>
    /// The literal must be a finite invariant-culture number, assigned to a size property that forwards to the element's
    /// inline style. It has no binding form.
    /// </summary>
    StyleLength,

    /// <summary>
    /// The literal is an invariant-culture integer assigned through a static setter such as <c>Grid.SetRow</c>.
    /// It has no binding form.
    /// </summary>
    AttachedInt32,

    /// <summary>
    /// The literal is a whitespace-separated list of class names.
    /// </summary>
    ClassList,

    /// <summary>
    /// The property has no literal form and can only be set by a binding.
    /// </summary>
    BindingOnly
}

/// <summary>
/// Represents a validated property assignment or binding on a XAML element.
/// </summary>
public sealed record ValidatedXamlProperty(
    string PropertyName,
    XamlPropertyKind Kind,
    string? LiteralValue,
    BindingExpressionNode? Binding);

/// <summary>
/// Represents a validated XAML element node ready for C# code emission.
/// </summary>
/// <param name="ControlTypeName">The control type to create.</param>
/// <param name="XName">The <c>x:Name</c> value, if any.</param>
/// <param name="Properties">The attribute assignments and bindings.</param>
/// <param name="Children">The child elements, added to the collection named by <paramref name="ContentProperty"/>.</param>
/// <param name="ContentProperty">The collection property that receives <paramref name="Children"/>.</param>
/// <param name="PropertyElements">The property elements such as <c>&lt;Dialog.Footer&gt;</c>.</param>
public sealed record ValidatedXamlElement(
    string ControlTypeName,
    string? XName,
    IReadOnlyList<ValidatedXamlProperty> Properties,
    IReadOnlyList<ValidatedXamlElement> Children,
    string ContentProperty,
    IReadOnlyList<ValidatedXamlPropertyElement> PropertyElements);

/// <summary>
/// Represents a validated property element, whose child elements are assigned to a control property.
/// </summary>
/// <param name="PropertyName">The C# property that receives the children.</param>
/// <param name="IsCollection">Whether the property is a collection; otherwise it holds exactly one child.</param>
/// <param name="Children">The child elements.</param>
public sealed record ValidatedXamlPropertyElement(
    string PropertyName,
    bool IsCollection,
    IReadOnlyList<ValidatedXamlElement> Children);

/// <summary>
/// Describes a view compiled from a XAML file in the project, which other XAML files can use as an element by its class name.
/// </summary>
/// <param name="ClassName">The class name without its namespace, which is the element name.</param>
/// <param name="FullClassName">The <c>x:Class</c> value.</param>
/// <param name="RootElement">The root element of the view's file, which determines the attributes it accepts.</param>
public sealed record XamlViewReference(string ClassName, string FullClassName, string RootElement);

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
    private static readonly Dictionary<string, Dictionary<string, XamlPropertyKind>> s_controlProperties =
        new(StringComparer.Ordinal)
        {
            ["View"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["ClipToBounds"] = XamlPropertyKind.Boolean
            },
            ["CanvasView"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["ClipToBounds"] = XamlPropertyKind.Boolean
            },
            ["StackPanel"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["ClipToBounds"] = XamlPropertyKind.Boolean,
                ["Orientation"] = XamlPropertyKind.Orientation,
                ["Spacing"] = XamlPropertyKind.Double,
                ["HorizontalAlignment"] = XamlPropertyKind.Alignment
            },
            ["Grid"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["ClipToBounds"] = XamlPropertyKind.Boolean,
                ["Rows"] = XamlPropertyKind.GridLengthList,
                ["Columns"] = XamlPropertyKind.GridLengthList,
                ["RowSpacing"] = XamlPropertyKind.Double,
                ["ColumnSpacing"] = XamlPropertyKind.Double
            },
            ["ScrollView"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["ClipToBounds"] = XamlPropertyKind.Boolean
            },
            ["Text"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Value"] = XamlPropertyKind.String,
                ["AriaLabel"] = XamlPropertyKind.String
            },
            ["Button"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Text"] = XamlPropertyKind.String,
                ["Command"] = XamlPropertyKind.BindingOnly,
                ["CommandParameter"] = XamlPropertyKind.String
            },
            ["Input"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Value"] = XamlPropertyKind.String,
                ["Placeholder"] = XamlPropertyKind.String,
                ["IsReadOnly"] = XamlPropertyKind.Boolean
            },
            ["Portal"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Side"] = XamlPropertyKind.PortalSide,
                ["Align"] = XamlPropertyKind.Alignment,
                ["SideOffset"] = XamlPropertyKind.Double,
                ["AlignOffset"] = XamlPropertyKind.Double
            },
            ["Dialog"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Side"] = XamlPropertyKind.PortalSide,
                ["Align"] = XamlPropertyKind.Alignment,
                ["SideOffset"] = XamlPropertyKind.Double,
                ["AlignOffset"] = XamlPropertyKind.Double,
                ["IsModal"] = XamlPropertyKind.Boolean,
                ["Title"] = XamlPropertyKind.String,
                ["Description"] = XamlPropertyKind.String
            }
        };

    private static readonly Dictionary<string, string> s_contentProperties = new(StringComparer.Ordinal)
    {
        ["Dialog"] = "Content"
    };

    private static readonly Dictionary<string, Dictionary<string, PropertyElementTarget>> s_propertyElements =
        new(StringComparer.Ordinal)
        {
            ["Dialog"] = new Dictionary<string, PropertyElementTarget>(StringComparer.Ordinal)
            {
                ["Title"] = new PropertyElementTarget("TitleContent", IsCollection: false),
                ["Description"] = new PropertyElementTarget("DescriptionContent", IsCollection: false),
                ["Footer"] = new PropertyElementTarget("Footer", IsCollection: true)
            }
        };

    private static readonly Dictionary<string, XamlPropertyKind> s_sharedProperties = new(StringComparer.Ordinal)
    {
        ["Margin"] = XamlPropertyKind.StyleThickness,
        ["Padding"] = XamlPropertyKind.StyleThickness,
        ["Width"] = XamlPropertyKind.StyleLength,
        ["Height"] = XamlPropertyKind.StyleLength,
        ["Grid.Row"] = XamlPropertyKind.AttachedInt32,
        ["Grid.Column"] = XamlPropertyKind.AttachedInt32,
        ["Grid.RowSpan"] = XamlPropertyKind.AttachedInt32,
        ["Grid.ColumnSpan"] = XamlPropertyKind.AttachedInt32
    };

    private readonly string _filePath;
    private readonly IReadOnlySet<string>? _knownCssClasses;
    private readonly Dictionary<string, XamlViewReference> _knownViews = new(StringComparer.Ordinal);
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
        string defaultNamespace = "NativeUI",
        IReadOnlyList<XamlViewReference>? knownViews = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XamlValidator validator = new(document.FilePath, knownCssClasses);
        foreach (XamlViewReference view in knownViews ?? [])
        {
            validator._knownViews.TryAdd(view.ClassName, view);
        }

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

    /// <summary>
    /// Returns the view that <paramref name="document"/> compiles to, or null when it has no <c>x:Class</c> or a root
    /// element that is not a built-in control.
    /// </summary>
    public static XamlViewReference? ReadViewReference(XamlDocumentNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XamlElementNode? root = document.RootElement;
        if (root is null || !s_controlProperties.ContainsKey(root.LocalName))
        {
            return null;
        }

        foreach (XamlAttributeNode attr in root.Attributes)
        {
            if (string.Equals(attr.NamespaceUri, XamlParser.XamlDirectiveNamespace, StringComparison.Ordinal) &&
                string.Equals(attr.LocalName, "Class", StringComparison.Ordinal))
            {
                string fullClassName = attr.Value.Trim();
                string className = fullClassName[(fullClassName.LastIndexOf('.') + 1)..];
                return new XamlViewReference(className, fullClassName, root.LocalName);
            }
        }

        return null;
    }

    /// <summary>
    /// Splits a <c>Class</c> attribute into its class names, separated by any whitespace.
    /// </summary>
    internal static string[] SplitClassNames(string classAttributeValue)
    {
        return classAttributeValue.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
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

        // A compiled view takes the attributes of its root element. A view cannot contain itself.
        string controlType = element.LocalName;
        string controlDefinition = element.LocalName;
        if (!isRoot &&
            !s_controlProperties.ContainsKey(element.LocalName) &&
            _knownViews.TryGetValue(element.LocalName, out XamlViewReference? knownView) &&
            !string.Equals(knownView.FullClassName, _fullClassName, StringComparison.Ordinal))
        {
            controlDefinition = knownView.RootElement;
            controlType = knownView.FullClassName.Contains('.') ? $"global::{knownView.FullClassName}" : knownView.ClassName;
        }

        if (!s_controlProperties.TryGetValue(controlDefinition, out Dictionary<string, XamlPropertyKind>? allowedProps))
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

            if (!allowedProps.TryGetValue(attr.LocalName, out XamlPropertyKind kind) &&
                !s_sharedProperties.TryGetValue(attr.LocalName, out kind))
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
                if (kind is XamlPropertyKind.StyleThickness or XamlPropertyKind.StyleLength or XamlPropertyKind.AttachedInt32)
                {
                    _diagnostics.Add(Diagnostic.Create(
                        XamlDiagnostics.InvalidPropertyValue,
                        XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                        attr.Value,
                        attr.LocalName,
                        element.LocalName));
                    continue;
                }

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

                validProps.Add(new ValidatedXamlProperty(attr.LocalName, kind, null, bindingNode));
            }
            else if (IsValidLiteral(kind, attr.Value))
            {
                validProps.Add(new ValidatedXamlProperty(attr.LocalName, kind, attr.Value, null));
            }
            else
            {
                _diagnostics.Add(Diagnostic.Create(
                    XamlDiagnostics.InvalidPropertyValue,
                    XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                    attr.Value,
                    attr.LocalName,
                    element.LocalName));
            }
        }

        // Support <Text>Hello</Text> inner text syntax when Value attribute is not explicitly provided.
        if (string.Equals(element.LocalName, "Text", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(element.InnerText) &&
            !validProps.Any(static p => p.PropertyName == "Value"))
        {
            validProps.Add(new ValidatedXamlProperty("Value", XamlPropertyKind.String, element.InnerText, null));
        }

        if (isRoot && string.IsNullOrWhiteSpace(_fullClassName))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.MissingXClass,
                XamlDiagnostics.CreateLocation(_filePath, element.Position),
                element.LocalName));
        }

        List<ValidatedXamlElement> validChildren = [];
        List<ValidatedXamlPropertyElement> propertyElements = [];
        HashSet<string> seenPropertyElements = new(StringComparer.Ordinal);
        foreach (XamlElementNode child in element.Children)
        {
            if (child.LocalName.Contains('.'))
            {
                ValidatedXamlPropertyElement? propertyElement = ValidatePropertyElement(element, child, validProps, seenPropertyElements);
                if (propertyElement is not null)
                {
                    propertyElements.Add(propertyElement);
                }

                continue;
            }

            ValidatedXamlElement? validatedChild = ValidateElementRecursive(child, isRoot: false);
            if (validatedChild is not null)
            {
                validChildren.Add(validatedChild);
            }
        }

        string contentProperty = s_contentProperties.GetValueOrDefault(controlDefinition, "Children");
        return new ValidatedXamlElement(controlType, xName, validProps, validChildren, contentProperty, propertyElements);
    }

    /// <summary>
    /// Validates a property element such as <c>&lt;Dialog.Footer&gt;</c> on <paramref name="owner"/>. Returns null after
    /// reporting a diagnostic.
    /// </summary>
    private ValidatedXamlPropertyElement? ValidatePropertyElement(
        XamlElementNode owner,
        XamlElementNode element,
        List<ValidatedXamlProperty> ownerProperties,
        HashSet<string> seenPropertyElements)
    {
        string[] parts = element.LocalName.Split('.');
        if (parts.Length != 2 ||
            !string.Equals(parts[0], owner.LocalName, StringComparison.Ordinal) ||
            !s_propertyElements.TryGetValue(owner.LocalName, out Dictionary<string, PropertyElementTarget>? targets) ||
            !targets.TryGetValue(parts[1], out PropertyElementTarget target))
        {
            ReportPropertyElement(element, $"<{owner.LocalName}> has no such property element");
            return null;
        }

        if (element.Attributes.Count > 0)
        {
            ReportPropertyElement(element, "a property element cannot have attributes");
            return null;
        }

        if (!seenPropertyElements.Add(parts[1]))
        {
            ReportPropertyElement(element, "it is set more than once");
            return null;
        }

        if (ownerProperties.Any(property => string.Equals(property.PropertyName, parts[1], StringComparison.Ordinal)))
        {
            ReportPropertyElement(element, $"the {parts[1]} attribute already sets this property");
            return null;
        }

        if (!target.IsCollection && element.Children.Count != 1)
        {
            ReportPropertyElement(element, "it must contain exactly one element");
            return null;
        }

        List<ValidatedXamlElement> children = [];
        foreach (XamlElementNode child in element.Children)
        {
            ValidatedXamlElement? validatedChild = ValidateElementRecursive(child, isRoot: false);
            if (validatedChild is not null)
            {
                children.Add(validatedChild);
            }
        }

        return new ValidatedXamlPropertyElement(target.PropertyName, target.IsCollection, children);
    }

    private void ReportPropertyElement(XamlElementNode element, string reason)
    {
        _diagnostics.Add(Diagnostic.Create(
            XamlDiagnostics.InvalidPropertyElement,
            XamlDiagnostics.CreateLocation(_filePath, element.Position),
            element.LocalName,
            reason));
    }

    /// <summary>
    /// Identifies the C# property that a property element assigns.
    /// </summary>
    private readonly record struct PropertyElementTarget(string PropertyName, bool IsCollection);

    /// <summary>
    /// Returns whether <paramref name="literal"/> converts to a value of the property <paramref name="kind"/>.
    /// </summary>
    internal static bool IsValidLiteral(XamlPropertyKind kind, string literal)
    {
        return kind switch
        {
            XamlPropertyKind.Boolean => bool.TryParse(literal, out _),
            XamlPropertyKind.Int32 => int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            XamlPropertyKind.AttachedInt32 => int.TryParse(literal, NumberStyles.None, CultureInfo.InvariantCulture, out _),
            XamlPropertyKind.StyleLength => TryParseFiniteDouble(literal, out _),
            XamlPropertyKind.Orientation => literal is "Vertical" or "Horizontal",
            XamlPropertyKind.PortalSide => literal is "Top" or "Right" or "Bottom" or "Left",
            XamlPropertyKind.Alignment => literal is "Start" or "Center" or "End",
            XamlPropertyKind.GridLengthList => IsValidGridLengthList(literal),
            XamlPropertyKind.StyleThickness => TryParseThickness(literal, out _),
            XamlPropertyKind.Int64 => long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            XamlPropertyKind.Double => double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && double.IsFinite(value),
            XamlPropertyKind.BindingOnly => false,
            _ => true
        };
    }

    /// <summary>
    /// Splits a comma-separated attribute into trimmed tokens.
    /// </summary>
    internal static string[] SplitList(string literal)
    {
        return literal.Split(',').Select(static token => token.Trim()).ToArray();
    }

    /// <summary>
    /// Parses one, two (vertical, horizontal), or four (top, right, bottom, left) comma-separated numbers.
    /// </summary>
    internal static bool TryParseThickness(string literal, out double[] values)
    {
        string[] tokens = SplitList(literal);
        values = new double[tokens.Length];
        if (tokens.Length is not (1 or 2 or 4))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!TryParseFiniteDouble(tokens[i], out values[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidGridLengthList(string literal)
    {
        return SplitList(literal).All(static token =>
        {
            if (string.Equals(token, "Auto", StringComparison.OrdinalIgnoreCase) || token == "*")
            {
                return true;
            }

            string number = token.EndsWith("*", StringComparison.Ordinal) ? token.Substring(0, token.Length - 1) : token;
            return TryParseFiniteDouble(number, out double value) && value >= 0;
        });
    }

    private static bool TryParseFiniteDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private void ValidateCssClassTokens(string classAttributeValue, XamlSourcePosition position)
    {
        if (_knownCssClasses is null)
        {
            return;
        }

        string[] tokens = SplitClassNames(classAttributeValue);
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
