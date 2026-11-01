using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    /// The literal must be <c>Static</c>, <c>Relative</c>, <c>Absolute</c>, or <c>Fixed</c>.
    /// </summary>
    Position,

    /// <summary>
    /// The literal is three comma-separated finite numbers, assigned as a <c>System.Numerics.Vector3</c>.
    /// </summary>
    Vector3,

    /// <summary>
    /// The literal is a hexadecimal color (<c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, or <c>#RRGGBBAA</c>) or a member name of <c>Colors</c>.
    /// </summary>
    Color,

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
    /// The literal was converted according to the C# type of the property; see <see cref="ValidatedXamlProperty.ConvertedValue"/>.
    /// </summary>
    Converted,

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
/// <param name="PropertyName">The property name.</param>
/// <param name="Kind">The way the literal converts to the property's value.</param>
/// <param name="LiteralValue">The attribute text, when the attribute is not a binding.</param>
/// <param name="Binding">The binding, when the attribute is one.</param>
/// <param name="ConvertedValue">The expression that creates the value, when <paramref name="Kind"/> is <see cref="XamlPropertyKind.Converted"/>.</param>
public sealed record ValidatedXamlProperty(
    string PropertyName,
    XamlPropertyKind Kind,
    string? LiteralValue,
    BindingExpressionNode? Binding,
    ExpressionSyntax? ConvertedValue = null);

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
            ["Viewport3D"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean
            },
            ["Scene3D"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal),
            ["PerspectiveCamera"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Position"] = XamlPropertyKind.Vector3,
                ["LookAt"] = XamlPropertyKind.Vector3,
                ["Fov"] = XamlPropertyKind.Double
            },
            ["DirectionalLight"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Color"] = XamlPropertyKind.Color,
                ["Direction"] = XamlPropertyKind.Vector3
            },
            ["Mesh"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Position"] = XamlPropertyKind.Vector3,
                ["Rotation"] = XamlPropertyKind.Vector3
            },
            ["BoxGeometry"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Width"] = XamlPropertyKind.Double,
                ["Height"] = XamlPropertyKind.Double,
                ["Depth"] = XamlPropertyKind.Double
            },
            ["StandardMaterial"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Color"] = XamlPropertyKind.Color
            },
            ["GradientSky"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["TopColor"] = XamlPropertyKind.Color,
                ["HorizonColor"] = XamlPropertyKind.Color,
                ["GroundColor"] = XamlPropertyKind.Color,
                ["GroundHorizonColor"] = XamlPropertyKind.Color,
                ["SkyCurve"] = XamlPropertyKind.Double,
                ["GroundCurve"] = XamlPropertyKind.Double,
                ["EnergyMultiplier"] = XamlPropertyKind.Double
            },
            ["ProgressBar"] = new Dictionary<string, XamlPropertyKind>(StringComparer.Ordinal)
            {
                ["Id"] = XamlPropertyKind.String,
                ["Class"] = XamlPropertyKind.ClassList,
                ["IsVisible"] = XamlPropertyKind.Boolean,
                ["IsEnabled"] = XamlPropertyKind.Boolean,
                ["Value"] = XamlPropertyKind.Double,
                ["Maximum"] = XamlPropertyKind.Double,
                ["IsIndeterminate"] = XamlPropertyKind.Boolean
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
        ["Dialog"] = "Content",
        ["Scene3D"] = "Items"
    };

    // Controls whose direct child is assigned to a property that holds one value, instead of added to a collection.
    private static readonly Dictionary<string, string> s_singleContentProperties = new(StringComparer.Ordinal)
    {
        ["Viewport3D"] = "Scene"
    };

    private static readonly Dictionary<string, Dictionary<string, PropertyElementTarget>> s_propertyElements =
        new(StringComparer.Ordinal)
        {
            ["Dialog"] = new Dictionary<string, PropertyElementTarget>(StringComparer.Ordinal)
            {
                ["Title"] = new PropertyElementTarget("TitleContent", IsCollection: false),
                ["Description"] = new PropertyElementTarget("DescriptionContent", IsCollection: false),
                ["Footer"] = new PropertyElementTarget("Footer", IsCollection: true)
            },
            ["Viewport3D"] = new Dictionary<string, PropertyElementTarget>(StringComparer.Ordinal)
            {
                ["Camera"] = new PropertyElementTarget("Camera", IsCollection: false)
            },
            ["Mesh"] = new Dictionary<string, PropertyElementTarget>(StringComparer.Ordinal)
            {
                ["Geometry"] = new PropertyElementTarget("Geometry", IsCollection: false),
                ["Material"] = new PropertyElementTarget("Material", IsCollection: false)
            }
        };

    private static readonly Dictionary<string, XamlPropertyKind> s_sharedProperties = new(StringComparer.Ordinal)
    {
        ["Margin"] = XamlPropertyKind.StyleThickness,
        ["Padding"] = XamlPropertyKind.StyleThickness,
        ["Height"] = XamlPropertyKind.StyleLength,
        ["Position"] = XamlPropertyKind.Position,
        ["Top"] = XamlPropertyKind.StyleLength,
        ["Right"] = XamlPropertyKind.StyleLength,
        ["Bottom"] = XamlPropertyKind.StyleLength,
        ["Left"] = XamlPropertyKind.StyleLength,
        ["Grid.Row"] = XamlPropertyKind.AttachedInt32,
        ["Grid.Column"] = XamlPropertyKind.AttachedInt32,
        ["Grid.RowSpan"] = XamlPropertyKind.AttachedInt32,
        ["Grid.ColumnSpan"] = XamlPropertyKind.AttachedInt32
    };

    private static readonly Dictionary<string, XamlPropertyKind> s_noProperties = new(StringComparer.Ordinal);

    private readonly string _filePath;
    private readonly IReadOnlySet<string>? _knownCssClasses;
    private readonly XamlTypeResolver? _types;
    private readonly Dictionary<string, XamlViewReference> _knownViews = new(StringComparer.Ordinal);
    private readonly List<Diagnostic> _diagnostics = [];
    private string _fullClassName = string.Empty;
    private string? _dataType;
    private string? _explicitStyleSheetId;
    private ValidatedXamlElement? _validatedRoot;

    private XamlValidator(string filePath, IReadOnlySet<string>? knownCssClasses, Compilation? compilation)
    {
        _filePath = filePath;
        _knownCssClasses = knownCssClasses;
        _types = compilation is null ? null : new XamlTypeResolver(compilation);
    }

    /// <summary>
    /// Validates the specified <paramref name="document"/> AST using the visitor pattern.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="knownCssClasses">The class names that <c>Class</c> attributes can use, or null to accept any.</param>
    /// <param name="defaultNamespace">The namespace of a view whose <c>x:Class</c> has none.</param>
    /// <param name="knownViews">The views compiled from other XAML files in the project.</param>
    /// <param name="compilation">
    /// The compilation that resolves XML namespaces and the C# types of properties. Without it, a document can use only
    /// built-in controls and the properties listed for them.
    /// </param>
    public static ValidatedXamlDocument Validate(
        XamlDocumentNode document,
        IReadOnlySet<string>? knownCssClasses = null,
        string defaultNamespace = "NativeUI",
        IReadOnlyList<XamlViewReference>? knownViews = null,
        Compilation? compilation = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XamlValidator validator = new(document.FilePath, knownCssClasses, compilation);
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
        if (!TryResolveControl(element, isRoot, out ResolvedControl control))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.UnknownElement,
                XamlDiagnostics.CreateLocation(_filePath, element.Position),
                element.LocalName));
            return null;
        }

        string controlType = control.TypeName;
        string controlDefinition = control.Definition;
        Dictionary<string, XamlPropertyKind> allowedProps = s_controlProperties.GetValueOrDefault(controlDefinition, s_noProperties);

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

            // Shared properties belong to controls; any other property is resolved from the element's C# type.
            IPropertySymbol? typedProperty = null;
            if (!allowedProps.TryGetValue(attr.LocalName, out XamlPropertyKind kind) &&
                (controlDefinition.Length == 0 || !s_sharedProperties.TryGetValue(attr.LocalName, out kind)))
            {
                typedProperty = control.Type is null ? null : _types!.FindSettableProperty(control.Type, attr.LocalName);
                if (typedProperty is null)
                {
                    _diagnostics.Add(Diagnostic.Create(
                        XamlDiagnostics.UnknownProperty,
                        XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                        attr.LocalName,
                        element.LocalName));
                    continue;
                }

                kind = XamlPropertyKind.Converted;
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
                bool isBindable = typedProperty is null
                    ? kind is not (XamlPropertyKind.StyleThickness or XamlPropertyKind.StyleLength or XamlPropertyKind.AttachedInt32)
                    : XamlTypeResolver.IsBindable(typedProperty);
                if (!isBindable)
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
            else if (typedProperty is not null)
            {
                XamlValueConverter? converter = XamlValueConverters.ForType(typedProperty.Type);
                if (converter is not null && converter.TryConvert(attr.Value, out ExpressionSyntax? convertedValue))
                {
                    validProps.Add(new ValidatedXamlProperty(attr.LocalName, kind, attr.Value, null, convertedValue));
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
            if (validatedChild is null)
            {
                continue;
            }

            if (!s_singleContentProperties.TryGetValue(controlDefinition, out string? singleContentProperty))
            {
                validChildren.Add(validatedChild);
            }
            else if (seenPropertyElements.Add(singleContentProperty))
            {
                propertyElements.Add(new ValidatedXamlPropertyElement(singleContentProperty, IsCollection: false, [validatedChild]));
            }
            else
            {
                ReportPropertyElement(child, $"<{element.LocalName}> can contain only one element");
            }
        }

        string contentProperty = s_contentProperties.GetValueOrDefault(controlDefinition, "Children");
        if (validChildren.Count > 0 && controlDefinition.Length == 0 && !HasMember(control.Type, contentProperty))
        {
            ReportPropertyElement(element, $"<{element.LocalName}> has no {contentProperty} property");
            return null;
        }

        return new ValidatedXamlElement(controlType, xName, validProps, validChildren, contentProperty, propertyElements);
    }

    /// <summary>
    /// Resolves <paramref name="element"/> to a built-in control, a view compiled from another XAML file, or a type in the
    /// compilation, in that order.
    /// </summary>
    private bool TryResolveControl(XamlElementNode element, bool isRoot, out ResolvedControl control)
    {
        string localName = element.LocalName;
        bool isNativeUINamespace = string.IsNullOrEmpty(element.NamespaceUri) ||
            string.Equals(element.NamespaceUri, XamlParser.NativeUINamespace, StringComparison.Ordinal);

        if (isNativeUINamespace && s_controlProperties.ContainsKey(localName))
        {
            control = new ResolvedControl(localName, localName, _types?.Resolve(XamlParser.NativeUINamespace, localName));
            return true;
        }

        // A compiled view takes the attributes of its root element. A view cannot contain itself.
        if (!isRoot &&
            _knownViews.TryGetValue(localName, out XamlViewReference? knownView) &&
            !string.Equals(knownView.FullClassName, _fullClassName, StringComparison.Ordinal) &&
            (isNativeUINamespace || IsInClrNamespace(knownView, element.NamespaceUri)))
        {
            string viewType = knownView.FullClassName.Contains('.') ? $"global::{knownView.FullClassName}" : knownView.ClassName;
            control = new ResolvedControl(viewType, knownView.RootElement, _types?.Resolve(XamlParser.NativeUINamespace, knownView.RootElement));
            return true;
        }

        INamedTypeSymbol? type = _types?.Resolve(element.NamespaceUri, localName);
        if (type is null || !_types!.IsCreatable(type))
        {
            control = default;
            return false;
        }

        control = new ResolvedControl(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), FindControlDefinition(type), type);
        return true;
    }

    private static bool IsInClrNamespace(XamlViewReference view, string xmlNamespace)
    {
        string? clrNamespace = XamlTypeResolver.ReadClrNamespace(xmlNamespace);
        return clrNamespace is not null &&
            string.Equals(view.FullClassName, $"{clrNamespace}.{view.ClassName}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the name of the nearest built-in control that <paramref name="type"/> derives from, or an empty string when
    /// it derives from none.
    /// </summary>
    private string FindControlDefinition(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (s_controlProperties.ContainsKey(current.Name) &&
                SymbolEqualityComparer.Default.Equals(current, _types!.Resolve(XamlParser.NativeUINamespace, current.Name)))
            {
                return current.Name;
            }
        }

        return string.Empty;
    }

    private static bool HasMember(INamedTypeSymbol? type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (!current.GetMembers(name).IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Identifies the type an element creates.
    /// </summary>
    /// <param name="TypeName">The C# type name to create.</param>
    /// <param name="Definition">The built-in control whose properties apply, or an empty string when there is none.</param>
    /// <param name="Type">The type whose C# properties the element can also set, when a compilation is available.</param>
    private readonly record struct ResolvedControl(string TypeName, string Definition, INamedTypeSymbol? Type);

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
        return XamlValueConverters.TryConvert(kind, literal, out _);
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
