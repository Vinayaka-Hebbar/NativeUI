using NativeUI.Tests;
using NativeUI.Xaml.Compiler;

int exitCode = TestRunner.RunSuite("NativeUI.Xaml.Compiler.Tests",
[
    ("XamlParser builds structured AST with attributes and children from XAML file", TestXamlParserAst),
    ("BindingExpressionParser parses shorthand and named Mode parameters", TestBindingExpressionParser),
    ("XamlValidator reports diagnostics for unknown elements, properties, and CSS classes", TestXamlValidatorDiagnostics),
    ("XamlEmitter uses SyntaxFactory to generate clean C# without global:: prefixes", TestXamlEmitterCleanSyntaxFactoryOutput),
    ("XamlEmitter generates collection expressions for Class attribute", TestXamlEmitterClassCollectionExpressions)
]);

return exitCode;

static void TestXamlParserAst()
{
    string xaml = TestRunner.ReadFixture("MainView.xaml");

    XamlDocumentNode doc = XamlParser.Parse("MainView.xaml", xaml);
    Assert.Null(doc.XmlError);
    Assert.NotNull(doc.RootElement);
    Assert.Equal("View", doc.RootElement!.LocalName);
    Assert.Equal(2, doc.RootElement.Children.Count);
    Assert.Equal("Inner Text Content", doc.RootElement.Children[1].InnerText);
}

static void TestBindingExpressionParser()
{
    XamlSourcePosition pos = new(1, 1);

    bool ok1 = BindingExpressionParser.TryParse("{Binding Greeting}", pos, out BindingExpressionNode? node1, out string? err1);
    Assert.True(ok1);
    Assert.Null(err1);
    Assert.Equal("Greeting", node1!.Path);
    Assert.Equal("OneWay", node1.Mode);

    bool ok2 = BindingExpressionParser.TryParse("{Binding Path=Subtitle, Mode=TwoWay}", pos, out BindingExpressionNode? node2, out string? err2);
    Assert.True(ok2);
    Assert.Null(err2);
    Assert.Equal("Subtitle", node2!.Path);
    Assert.Equal("TwoWay", node2.Mode);

    bool bad = BindingExpressionParser.TryParse("{Binding Mode=InvalidMode, Path=Title}", pos, out _, out string? badErr);
    Assert.False(bad);
    Assert.NotNull(badErr);
}

static void TestXamlValidatorDiagnostics()
{
    string invalidXaml = TestRunner.ReadFixture("BrokenView.xaml");

    HashSet<string> knownClasses = ["card", "title"];
    XamlDocumentNode ast = XamlParser.Parse("BrokenView.xaml", invalidXaml);
    ValidatedXamlDocument validated = XamlValidator.Validate(ast, knownClasses, "MyApp.Views");

    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NXAML006"), "Expected unknown CSS class warning NXAML006.");
    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NXAML003"), "Expected unknown property error NXAML003.");
    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NXAML004"), "Expected invalid binding error NXAML004.");
    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NXAML002"), "Expected unknown element error NXAML002.");
}

static void TestXamlEmitterCleanSyntaxFactoryOutput()
{
    string validXaml = TestRunner.ReadFixture("HelloView.xaml");

    XamlDocumentNode ast = XamlParser.Parse("HelloView.xaml", validXaml);
    ValidatedXamlDocument validated = XamlValidator.Validate(
        ast,
        new HashSet<string>(["card", "title", "subtitle"]),
        "NativeUI.Sample.Shared");

    Assert.Equal(0, validated.Diagnostics.Count);

    string emittedCSharp = XamlEmitter.Emit(validated);

    Assert.Contains("using NativeUI.Controls;", emittedCSharp);
    Assert.Contains("using NativeUI.Core;", emittedCSharp);
    Assert.Contains("using NativeUI.Xaml;", emittedCSharp);
    Assert.Contains("namespace NativeUI.Sample.Shared;", emittedCSharp);
    Assert.Contains("public partial class HelloView : View", emittedCSharp);
    Assert.Contains("partial void AttachStyles();", emittedCSharp);
    Assert.Contains("Class = [\"card\"];", emittedCSharp);
    Assert.Contains("AttachStyles();", emittedCSharp);
    Assert.Contains("TitleText.Class = [\"title\"];", emittedCSharp);
    Assert.Contains("public Text TitleText { get; private set; } = null!;", emittedCSharp);
    Assert.Contains("new CompiledBinding<HelloViewModel, string>(\"Title\", static source => source.Title, null, BindingMode.OneWay)", emittedCSharp);
    Assert.False(emittedCSharp.Contains("global::", StringComparison.Ordinal), "Generated C# must not contain global:: clutter.");
}

static void TestXamlEmitterClassCollectionExpressions()
{
    string xaml = TestRunner.ReadFixture("ClassExpressionView.xaml");
    ValidatedXamlDocument doc = XamlValidator.Validate(
        XamlParser.Parse("ClassExpressionView.xaml", xaml),
        new HashSet<string>(["card", "badge", "test"]),
        "TestApp");

    string emittedCSharp = XamlEmitter.Emit(doc);

    Assert.Contains("Class = [\"card\"];", emittedCSharp);
    Assert.Contains("SingleClassText.Class = [\"badge\"];", emittedCSharp);
    Assert.Contains("MultiClassText.Class = [\"badge\", \"test\"];", emittedCSharp);
    Assert.Contains("EmptyClassText.Class = [];", emittedCSharp);
}
