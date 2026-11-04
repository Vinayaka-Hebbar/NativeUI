using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NativeUI.Controls;
using NativeUI.Media;
using NativeUI.Tests;
using NativeUI.Xaml.Compiler;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler.Tests;

internal static class VectorEmissionTests
{
    public static void GeometryConstructorsCompile()
    {
        string path = TestRunner.ReadFixture("Vector/geometry.path");
        ExpressionSyntax geometry = VectorCodeEmitter.Geometry(PathDataParser.Parse(path));
        string source = WrapExpression(geometry);

        Assert.Contains("new PathGeometry", source);
        Assert.Contains("new PathFigure", source);
        Assert.Contains("new ArcSegment", source);
        Assert.False(source.Contains("global::", StringComparison.Ordinal));
        Assert.False(source.Contains("Geometry.Parse", StringComparison.Ordinal));
        AssertCompiles(source);
    }

    public static void LiteralsUseParsedValues()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            VectorValueConverter converter = new("NativeUI.Layout.Rect");
            bool success = converter.TryConvert("1e2, -2.5, 24, 24", out ExpressionSyntax? expression);
            Assert.True(success);
            Assert.NotNull(expression);
            AssertCompiles(WrapExpression(expression!));
            Assert.False(converter.TryConvert("NaN, 0, 24, 24", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    public static void FactoriesShareDefinitionsAndImportTypes()
    {
        SvgNodeData definition = SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/logo.svg"));
        SvgAsset asset = new("Assets/logo.svg", "logo", "CreateLogo", VectorCodeEmitter.Node(definition), null);
        string source = SvgAssetCompiler.Emit([asset, asset with { Identity = "Assets/alias.svg" }]);
        CompilationUnitSyntax unit = SyntaxFactory.ParseCompilationUnit(source);
        string[] imports = unit.Usings.Select(static directive => directive.Name?.ToString() ?? "").ToArray();

        Assert.True(imports.Contains("NativeUI.Media"));
        Assert.True(imports.Contains("NativeUI.Layout"));
        Assert.True(imports.Contains("NativeUI.Controls"));
        Assert.True(imports.Contains("System.Collections.Generic"));
        Assert.False(source.Contains("global::", StringComparison.Ordinal));
        Assert.Equal(0, unit.DescendantNodes().OfType<FieldDeclarationSyntax>().Count());
        Assert.Contains("new Svg()", source);
        Assert.Contains("Color.FromRgbBytes(255, 0, 0)", source);
        Assert.Contains("new SvgNode(SvgNodeKind.Svg)", source);
        Assert.Contains("Definition =", source);
        Assert.Equal(2, unit.DescendantNodes().OfType<MethodDeclarationSyntax>().Count());
        AssertCompiles(source);
    }

    public static void TypedPaintsCompileAndFactoriesCreateDefinitions()
    {
        SvgNodeData scene = SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/typed-scene.svg"));
        AssertCompiles(SvgAssetCompiler.Emit([new SvgAsset("scene.svg", "scene", "CreateScene", VectorCodeEmitter.Node(scene), null)]));
        SvgNodeData definition = SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/typed-paints.svg"));
        string source = SvgAssetCompiler.Emit([new SvgAsset("typed.svg", "typed", "CreateTyped", VectorCodeEmitter.Node(definition), null)]);
        Assert.Contains("new SvgNode(SvgNodeKind.Svg)", source);
        Assert.False(source.Contains("Color.Parse", StringComparison.Ordinal));
        Assert.False(source.Contains("\"#4285F4\"", StringComparison.Ordinal));
        Assert.Contains("Color.FromRgbBytes(66, 133, 244)", source);
        Assert.Contains("StopColor = Color.FromSrgb(1, 0, 0, 0.5)", source);
        AssertCompiles(source);
        using MemoryStream assemblyBytes = new();
        CSharpCompilation compilation = CreateCompilation(source);
        Assert.True(compilation.Emit(assemblyBytes).Success);
        System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(assemblyBytes.ToArray());
        System.Reflection.MethodInfo factory = assembly.GetType("NativeUI.Generated.GeneratedSvgAssets")?.GetMethod("CreateTyped", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Generated SVG factory is missing.");
        using Svg first = (Svg)(factory.Invoke(null, null) ?? throw new InvalidOperationException("Factory returned null."));
        using Svg second = (Svg)(factory.Invoke(null, null) ?? throw new InvalidOperationException("Factory returned null."));
        Assert.False(ReferenceEquals(first.Definition, second.Definition));
        Assert.False(ReferenceEquals(first.Definition?.Children[0], second.Definition?.Children[0]));
        SvgNode definitionNode = first.Definition ?? throw new InvalidOperationException("Factory definition is missing.");
        Assert.Equal(SvgNodeKind.Svg, definitionNode.Kind);
        Assert.Equal(new NativeUI.Layout.Rect(0, 0, 24, 24), definitionNode.ViewBox);
        SvgNode group = definitionNode.Children[1];
        Assert.Equal(new SolidColorBrush(Color.FromRgbBytes(66, 133, 244)), group.Fill);
        Assert.Null(group.Children[0].Fill);
        Assert.True(group.Children[1].Fill is SvgNoBrush);
        Assert.True(group.Children[1].Stroke is SvgCurrentColorBrush);
        Assert.Equal("paint", ((SvgReferenceBrush)group.Children[2].Fill!).Id);
    }

    public static void ViewsImportGeneratedAssetsOnlyWhenUsed()
    {
        SvgAsset asset = new("Assets/logo.svg", "logo", "CreateLogo",
            VectorCodeEmitter.Node(SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/logo.svg"))), null);
        ValidatedXamlElement image = new("Image", "Logo",
            [new ValidatedXamlProperty("Source", XamlPropertyKind.Converted, "Assets/logo.svg", null, LiteralExpression(SyntaxKind.NullLiteralExpression))],
            [], "Children", []);
        ValidatedXamlElement root = new("View", null, [], [image], "Children", []);
        ValidatedXamlDocument document = new("VectorView.xaml", "Tests.VectorView", "Tests", "VectorView", null, "VectorView", root, []);
        string withoutAssets = XamlEmitter.Emit(document);
        string withAssets = XamlEmitter.Emit(SvgAssetCompiler.Rewrite(document, [asset], CreateCompilation()));

        Assert.False(withoutAssets.Contains("using NativeUI.Generated;", StringComparison.Ordinal));
        Assert.Contains("using NativeUI.Generated;", withAssets);
        Assert.Contains("new CompiledSvgSource(GeneratedSvgAssets.CreateLogo)", withAssets);
        Assert.False(withAssets.Contains("global::", StringComparison.Ordinal));
        AssertCompiles(withAssets, SvgAssetCompiler.Emit([asset]));
    }

    public static void LeavesNonImageSourcesUnchanged()
    {
        SvgAsset asset = new("Assets/logo.svg", "logo", "CreateLogo",
            VectorCodeEmitter.Node(SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/logo.svg"))), null);
        ValidatedXamlProperty property = new("Source", XamlPropertyKind.String, "Assets/logo.svg", null);
        ValidatedXamlElement element = new("UnknownControl", null, [property], [], "Children", []);
        ValidatedXamlDocument document = new("Custom.xaml", "Custom", "", "Custom", null, "", element, []);
        ValidatedXamlDocument rewritten = SvgAssetCompiler.Rewrite(document, [asset], CreateCompilation());
        Assert.True(ReferenceEquals(property, rewritten.RootElement?.Properties[0]));
    }

    public static void InlineSvgCompilesAndKeepsTextOrder()
    {
        XamlDocumentNode parsed = XamlParser.Parse("InlineView.xaml", TestRunner.ReadFixture("Vector/InlineView.xaml"));
        ValidatedXamlDocument validated = XamlValidator.Validate(parsed, compilation: CreateCompilation());
        Assert.Equal(0, validated.Diagnostics.Count, string.Join(Environment.NewLine, validated.Diagnostics));
        string generated = XamlEmitter.Emit(validated);
        Assert.Contains("new SvgNode(SvgNodeKind.Text)", generated);
        Assert.False(generated.Contains("new SvgElement", StringComparison.Ordinal));
        Assert.False(generated.Contains("KeyValuePair", StringComparison.Ordinal));
        Assert.False(generated.Contains("Geometry.Parse", StringComparison.Ordinal));
        AssertCompiles(generated);
        SvgNodeData text = SvgDocumentParser.Parse(TestRunner.ReadFixture("Vector/mixed-text.svg")).Children[0];
        Assert.Equal("before", text.Children[0].Text);
        Assert.Equal("tspan", text.Children[1].Name);
        Assert.Equal("after", text.Children[2].Text);
    }

    public static void RejectsInvalidSvgAttributes()
    {
        foreach (string fixture in new[] { "Vector/invalid-viewbox.svg", "Vector/invalid-transform.svg", "Vector/invalid-linecap.svg" })
        {
            bool rejected = false;
            try
            {
                SvgDocumentParser.Parse(TestRunner.ReadFixture(fixture));
            }
            catch (FormatException)
            {
                rejected = true;
            }

            Assert.True(rejected, "Invalid SVG must fail before C# emission: " + fixture);
        }
    }

    private static string WrapExpression(ExpressionSyntax expression)
    {
        MethodDeclarationSyntax factory = MethodDeclaration(PredefinedType(Token(SyntaxKind.ObjectKeyword)), Identifier("Create"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithExpressionBody(ArrowExpressionClause(expression))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        return CompilationUnit()
            .WithUsings(List(
            [
                UsingDirective(ParseName("System.Collections.Generic")),
                UsingDirective(ParseName("NativeUI.Media")),
                UsingDirective(ParseName("NativeUI.Layout"))
            ]))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(ClassDeclaration("GeneratedGeometry")
                .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
                .WithMembers(SingletonList<MemberDeclarationSyntax>(factory))))
            .NormalizeWhitespace().ToFullString();
    }

    private static CSharpCompilation CreateCompilation(params string[] sources)
    {
        string trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Runtime assembly references are unavailable.");
        IEnumerable<string> paths = trustedAssemblies.Split(System.IO.Path.PathSeparator)
            .Append(typeof(Image).Assembly.Location)
            .Append(typeof(Geometry).Assembly.Location)
            .Append(typeof(NativeUI.Xaml.XamlCompilationAttribute).Assembly.Location);
        CSharpCompilation compilation = CSharpCompilation.Create("VectorEmissionVerification",
            sources.Select(static source => CSharpSyntaxTree.ParseText(source)),
            paths.Distinct(StringComparer.Ordinal).Select(static path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation;
    }

    private static void AssertCompiles(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);
        Diagnostic[] errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Equal(0, errors.Length, string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
    }
}
