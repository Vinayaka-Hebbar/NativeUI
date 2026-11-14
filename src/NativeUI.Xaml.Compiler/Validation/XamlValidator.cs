using System.Globalization;
using System.Xml.Linq;
using NativeUI.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>A direct CLR event handler subscription.</summary>
    Event,

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
/// <param name="TemplateDataType">For a <c>DataTemplate</c>, the full name of the item type, whose single child in
/// <paramref name="Children"/> is the element tree built for each item. Otherwise <see langword="null"/>.</param>
public sealed record ValidatedXamlElement(
    string ControlTypeName,
    string? XName,
    IReadOnlyList<ValidatedXamlProperty> Properties,
    IReadOnlyList<ValidatedXamlElement> Children,
    string ContentProperty,
    IReadOnlyList<ValidatedXamlPropertyElement> PropertyElements,
    string? TemplateDataType = null);

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
    private const string ContentPropertyAttributeName = "NativeUI.Xaml.ContentPropertyAttribute";
    private const string DataTemplateElementName = "DataTemplate";

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
        ["Left"] = XamlPropertyKind.StyleLength
    };

    private static readonly Dictionary<string, XamlPropertyKind> s_noProperties = new(StringComparer.Ordinal);

    private readonly string _filePath;
    private readonly IReadOnlySet<string>? _knownCssClasses;
    private readonly XamlTypeResolver? _types;
    private readonly Compilation? _compilation;
    private readonly Dictionary<string, XamlViewReference> _knownViews = new(StringComparer.Ordinal);
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly Dictionary<string, INamedTypeSymbol?> _namedElements = new(StringComparer.Ordinal);
    private string _fullClassName = string.Empty;
    private string? _dataType;
    private string? _explicitStyleSheetId;
    private ValidatedXamlElement? _validatedRoot;
    private int _templateDepth;

    private XamlValidator(string filePath, IReadOnlySet<string>? knownCssClasses, Compilation? compilation)
    {
        _filePath = filePath;
        _knownCssClasses = knownCssClasses;
        _compilation = compilation;
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
        if (element.SvgMarkup is not null && element.LocalName is "Svg" or "svg")
        {
            return ValidateInlineSvg(element, isRoot);
        }

        if (IsDataTemplate(element))
        {
            ReportPropertyElement(element, "a DataTemplate can only be the value of a property element");
            return null;
        }

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
                    case "Name" when _templateDepth > 0:
                        _diagnostics.Add(Diagnostic.Create(
                            XamlDiagnostics.UnknownProperty,
                            XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                            "x:Name inside a DataTemplate",
                            element.LocalName));
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

            IEventSymbol? eventSymbol = FindEvent(control.Type, attr.LocalName);
            if (eventSymbol is not null)
            {
                INamedTypeSymbol? view = _compilation?.GetTypeByMetadataName(_fullClassName);
                IMethodSymbol? invoke = (eventSymbol.Type as INamedTypeSymbol)?.DelegateInvokeMethod;
                bool valid = view is not null && invoke is not null && FindMethods(view, attr.Value).Any(method =>
                    !method.IsStatic && method.ReturnsVoid && method.Parameters.Length == invoke.Parameters.Length &&
                    method.Parameters.Zip(invoke.Parameters).All(pair => pair.First.RefKind == RefKind.None &&
                        _compilation!.ClassifyCommonConversion(pair.Second.Type, pair.First.Type).IsImplicit));
                if (valid) validProps.Add(new ValidatedXamlProperty(attr.LocalName, XamlPropertyKind.Event, attr.Value, null));
                else _diagnostics.Add(Diagnostic.Create(XamlDiagnostics.InvalidPropertyValue, XamlDiagnostics.CreateLocation(_filePath, attr.Position), attr.Value, attr.LocalName, element.LocalName));
                continue;
            }

            // Shared properties belong to controls; any other property is resolved from the element's C# type or an attached property owner.
            ITypeSymbol? typedPropertyType = null;
            bool isTypedPropertyBindable = false;
            if (!allowedProps.TryGetValue(attr.LocalName, out XamlPropertyKind kind) &&
                (controlDefinition.Length == 0 || !s_sharedProperties.TryGetValue(attr.LocalName, out kind)))
            {
                if (control.Type is not null && _types is not null)
                {
                    if (attr.LocalName.Contains('.'))
                    {
                        string[] parts = attr.LocalName.Split('.');
                        if (parts.Length == 2 &&
                            string.IsNullOrEmpty(attr.NamespaceUri) &&
                            _types.FindAttachedSetter(parts[0], parts[1], control.Type) is IMethodSymbol attachedSetter)
                        {
                            typedPropertyType = attachedSetter.Parameters[1].Type;
                            isTypedPropertyBindable = XamlTypeResolver.IsBindable(attachedSetter.ContainingType, parts[1]);
                        }
                    }
                    else if (_types.FindSettableProperty(control.Type, attr.LocalName) is IPropertySymbol typedProperty)
                    {
                        typedPropertyType = typedProperty.Type;
                        isTypedPropertyBindable = XamlTypeResolver.IsBindable(typedProperty);
                    }
                }

                if (typedPropertyType is null)
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

            if (TryParseReference(attr.Value, out string referenceName))
            {
                ValidateReference(attr, element, typedPropertyType, referenceName, validProps);
                continue;
            }

            if (BindingExpressionParser.IsMarkupExtension(attr.Value))
            {
                bool isBindable = typedPropertyType is null
                    ? kind is not (XamlPropertyKind.StyleThickness or XamlPropertyKind.StyleLength)
                    : isTypedPropertyBindable;
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
            else if (typedPropertyType is not null)
            {
                XamlValueConverter? converter = XamlValueConverters.ForType(typedPropertyType);
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

        if (!isRoot && _templateDepth == 0 && !string.IsNullOrWhiteSpace(xName))
        {
            _namedElements[xName!] = control.Type;
        }

        string contentProperty = s_contentProperties.GetValueOrDefault(controlDefinition) ?? FindContentProperty(control.Type) ?? "Children";
        string? singleContentProperty = s_singleContentProperties.GetValueOrDefault(controlDefinition) ??
            (FindProperty(control.Type, contentProperty) is IPropertySymbol { SetMethod: not null } property && !IsCollectionType(property.Type)
                ? contentProperty
                : null);

        // Support <Text>Hello</Text> inner text syntax when Value attribute is not explicitly provided.
        if (string.Equals(element.LocalName, "Text", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(element.InnerText) &&
            !validProps.Any(static p => p.PropertyName == "Value"))
        {
            validProps.Add(new ValidatedXamlProperty("Value", XamlPropertyKind.String, element.InnerText, null));
        }
        else if (!string.IsNullOrEmpty(element.InnerText) &&
            singleContentProperty is not null &&
            FindProperty(control.Type, singleContentProperty)?.Type.SpecialType is SpecialType.System_Object or SpecialType.System_String &&
            !validProps.Any(p => p.PropertyName == singleContentProperty))
        {
            validProps.Add(new ValidatedXamlProperty(singleContentProperty, XamlPropertyKind.String, element.InnerText, null));
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
                ValidatedXamlPropertyElement? propertyElement = ValidatePropertyElement(element, control.Type, child, validProps, seenPropertyElements);
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

            if (singleContentProperty is null)
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

        if (validChildren.Count > 0 && controlDefinition.Length == 0 && !HasMember(control.Type, contentProperty))
        {
            ReportPropertyElement(element, $"<{element.LocalName}> has no {contentProperty} property");
            return null;
        }

        return new ValidatedXamlElement(controlType, xName, validProps, validChildren, contentProperty, propertyElements);
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is <c>{x:Reference Name}</c>, and the name it refers to.
    /// </summary>
    private static bool TryParseReference(string value, out string name)
    {
        name = string.Empty;
        if (!BindingExpressionParser.IsMarkupExtension(value))
        {
            return false;
        }

        string inner = value.Trim()[1..^1].Trim();
        const string Prefix = "x:Reference";
        if (!inner.StartsWith(Prefix, StringComparison.Ordinal) ||
            inner.Length == Prefix.Length ||
            !char.IsWhiteSpace(inner[Prefix.Length]))
        {
            return false;
        }

        name = inner[Prefix.Length..].Trim();
        return true;
    }

    /// <summary>
    /// Accepts <c>{x:Reference Name}</c> on a typed property when an element declared earlier in the document has that
    /// <c>x:Name</c> and its type converts implicitly to the property type. The value is the field that <c>x:Name</c> creates.
    /// </summary>
    private void ValidateReference(
        XamlAttributeNode attr,
        XamlElementNode element,
        ITypeSymbol? propertyType,
        string name,
        List<ValidatedXamlProperty> validProps)
    {
        if (propertyType is null)
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.InvalidPropertyValue,
                XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                attr.Value,
                attr.LocalName,
                element.LocalName));
            return;
        }

        if (!SyntaxFacts.IsValidIdentifier(name) || !_namedElements.TryGetValue(name, out INamedTypeSymbol? type))
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.UnknownReference,
                XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                name));
            return;
        }

        if (type is not null && _compilation is not null && !_compilation.ClassifyCommonConversion(type, propertyType).IsImplicit)
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.InvalidPropertyValue,
                XamlDiagnostics.CreateLocation(_filePath, attr.Position),
                attr.Value,
                attr.LocalName,
                element.LocalName));
            return;
        }

        validProps.Add(new ValidatedXamlProperty(attr.LocalName, XamlPropertyKind.Converted, attr.Value, null, SyntaxFactory.IdentifierName(name)));
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

    private ValidatedXamlElement? ValidateInlineSvg(XamlElementNode element, bool isRoot)
    {
        try
        {
            XElement markup = XElement.Parse(element.SvgMarkup ?? throw new InvalidOperationException("Inline SVG markup is missing."), LoadOptions.PreserveWhitespace);
            bool nativeControl = element.NamespaceUri != "http://www.w3.org/2000/svg";
            XNamespace svgNamespace = "http://www.w3.org/2000/svg";
            markup.Name = svgNamespace + "svg";
            foreach (XAttribute attribute in markup.Attributes().ToArray())
            {
                if (attribute.IsNamespaceDeclaration || attribute.Name.NamespaceName == XamlParser.XamlDirectiveNamespace ||
                    (nativeControl && attribute.Name.LocalName != "ViewBox"))
                {
                    attribute.Remove();
                }
                else if (attribute.Name.LocalName == "ViewBox")
                {
                    markup.SetAttributeValue("viewBox", attribute.Value);
                    attribute.Remove();
                }
            }

            SvgNodeData definition = SvgDocumentParser.Parse(markup.ToString(SaveOptions.DisableFormatting));
            XamlAttributeNode[] controlAttributes = element.Attributes.Where(attribute =>
                attribute.NamespaceUri == XamlParser.XamlDirectiveNamespace ||
                (nativeControl && attribute.LocalName != "ViewBox")).ToArray();
            XamlElementNode control = element with
            {
                LocalName = "Svg",
                NamespaceUri = XamlParser.NativeUINamespace,
                Attributes = controlAttributes,
                Children = [],
                SvgMarkup = null
            };
            ValidatedXamlElement? validated = ValidateElementRecursive(control, isRoot);
            return validated is null ? null : validated with
            {
                Properties = [.. validated.Properties, new ValidatedXamlProperty("Definition", XamlPropertyKind.Converted, null, null, VectorCodeEmitter.Node(definition))]
            };
        }
        catch (Exception exception) when (exception is FormatException or System.Xml.XmlException or ArgumentException)
        {
            _diagnostics.Add(Diagnostic.Create(XamlDiagnostics.XmlSyntaxError,
                XamlDiagnostics.CreateLocation(_filePath, element.Position), exception.Message));
            return null;
        }
    }

    private static IEventSymbol? FindEvent(INamedTypeSymbol? type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            IEventSymbol? result = current.GetMembers(name).OfType<IEventSymbol>().FirstOrDefault();
            if (result is not null) return result;
        }
        return null;
    }

    private static IEnumerable<IMethodSymbol> FindMethods(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (IMethodSymbol method in current.GetMembers(name).OfType<IMethodSymbol>()) yield return method;
        }
    }

    /// <summary>
    /// Returns the property that <c>NativeUI.Xaml.ContentPropertyAttribute</c> names on <paramref name="type"/> or its
    /// nearest attributed base type, or null when no type in the hierarchy has the attribute.
    /// </summary>
    private static string? FindContentProperty(INamedTypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (AttributeData attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == ContentPropertyAttributeName &&
                    attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is string name)
                {
                    return name;
                }
            }
        }

        return null;
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol? type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { IsStatic: false } property)
                {
                    return property;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns whether <paramref name="type"/> is a collection that child elements are added to, rather than a single value.
    /// </summary>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T)
        {
            return true;
        }

        foreach (INamedTypeSymbol implemented in type.AllInterfaces)
        {
            if (implemented.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T)
            {
                return true;
            }
        }

        return false;
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
    /// <summary>
    /// Validates a property element such as <c>&lt;Dialog.Footer&gt;</c>. Built-in controls list their property elements.
    /// Other types accept a property element for any property: one that holds a collection receives each child, and
    /// one with a setter receives a single child.
    /// </summary>
    private ValidatedXamlPropertyElement? ValidatePropertyElement(
        XamlElementNode owner,
        INamedTypeSymbol? ownerType,
        XamlElementNode element,
        List<ValidatedXamlProperty> ownerProperties,
        HashSet<string> seenPropertyElements)
    {
        string[] parts = element.LocalName.Split('.');
        if (parts.Length != 2 ||
            !string.Equals(parts[0], owner.LocalName, StringComparison.Ordinal) ||
            !TryGetPropertyElementTarget(owner.LocalName, ownerType, parts[1], out PropertyElementTarget target))
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
            ValidatedXamlElement? validatedChild = IsDataTemplate(child)
                ? ValidateDataTemplate(child)
                : ValidateElementRecursive(child, isRoot: false);
            if (validatedChild is not null)
            {
                children.Add(validatedChild);
            }
        }

        return new ValidatedXamlPropertyElement(target.PropertyName, target.IsCollection, children);
    }

    private bool TryGetPropertyElementTarget(
        string ownerName,
        INamedTypeSymbol? ownerType,
        string propertyName,
        out PropertyElementTarget target)
    {
        if (s_propertyElements.TryGetValue(ownerName, out Dictionary<string, PropertyElementTarget>? targets))
        {
            return targets.TryGetValue(propertyName, out target);
        }

        if (FindProperty(ownerType, propertyName) is IPropertySymbol property)
        {
            bool isCollection = IsCollectionType(property.Type);
            target = new PropertyElementTarget(propertyName, isCollection);
            return isCollection || property.SetMethod is not null;
        }

        target = default;
        return false;
    }

    private static bool IsDataTemplate(XamlElementNode element)
    {
        return string.Equals(element.LocalName, DataTemplateElementName, StringComparison.Ordinal) &&
            (string.IsNullOrEmpty(element.NamespaceUri) ||
             string.Equals(element.NamespaceUri, XamlParser.NativeUINamespace, StringComparison.Ordinal));
    }

    /// <summary>
    /// Validates <c>&lt;DataTemplate DataType="prefix:Type"&gt;</c> with exactly one child element, the tree built for each
    /// item. <c>DataType</c> takes a prefixed type name or a full CLR type name, and bindings in the tree compile against it.
    /// </summary>
    private ValidatedXamlElement? ValidateDataTemplate(XamlElementNode element)
    {
        XamlAttributeNode? dataTypeAttribute = element.Attributes.FirstOrDefault(static attr =>
            string.Equals(attr.LocalName, "DataType", StringComparison.Ordinal) && string.IsNullOrEmpty(attr.Prefix));
        if (dataTypeAttribute is null || element.Attributes.Count != 1)
        {
            ReportPropertyElement(element, "a DataTemplate needs exactly one attribute, DataType");
            return null;
        }

        INamedTypeSymbol? dataType = ResolveTypeReference(element, dataTypeAttribute.Value.Trim());
        if (dataType is null)
        {
            _diagnostics.Add(Diagnostic.Create(
                XamlDiagnostics.InvalidPropertyValue,
                XamlDiagnostics.CreateLocation(_filePath, dataTypeAttribute.Position),
                dataTypeAttribute.Value,
                "DataType",
                element.LocalName));
            return null;
        }

        if (element.Children.Count != 1)
        {
            ReportPropertyElement(element, "a DataTemplate must contain exactly one element");
            return null;
        }

        _templateDepth++;
        ValidatedXamlElement? body;
        try
        {
            body = ValidateElementRecursive(element.Children[0], isRoot: false);
        }
        finally
        {
            _templateDepth--;
        }

        return body is null
            ? null
            : new ValidatedXamlElement(
                DataTemplateElementName,
                null,
                [],
                [body],
                string.Empty,
                [],
                dataType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    /// <summary>
    /// Resolves <c>prefix:Type</c> through the namespace prefixes in scope, or a full CLR type name, to a type in the compilation.
    /// </summary>
    private INamedTypeSymbol? ResolveTypeReference(XamlElementNode element, string reference)
    {
        if (_types is null || _compilation is null)
        {
            return null;
        }

        int separator = reference.IndexOf(':');
        if (separator < 0)
        {
            return _compilation.GetTypeByMetadataName(reference);
        }

        string prefix = reference[..separator];
        return element.NamespacePrefixes.TryGetValue(prefix, out string? xmlNamespace)
            ? _types.Resolve(xmlNamespace, reference[(separator + 1)..])
            : null;
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
