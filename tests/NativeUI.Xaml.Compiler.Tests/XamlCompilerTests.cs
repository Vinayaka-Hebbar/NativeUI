using NativeUI.Tests;
using NativeUI.Xaml.Compiler;

int exitCode = TestRunner.RunSuite("NativeUI.Xaml.Compiler.Tests",
[
    ("XamlParser builds structured AST with attributes and children from XAML file", TestXamlParserAst),
    ("BindingExpressionParser parses shorthand and named Mode parameters", TestBindingExpressionParser),
    ("XamlValidator reports diagnostics for unknown elements, properties, and CSS classes", TestXamlValidatorDiagnostics),
    ("XamlEmitter uses SyntaxFactory to generate clean C# without global:: prefixes", TestXamlEmitterCleanSyntaxFactoryOutput),
    ("XamlEmitter generates collection expressions for Class attribute", TestXamlEmitterClassCollectionExpressions),
    ("XamlEmitter converts literals by property type, not by their text", TestXamlEmitterLiteralsUsePropertyTypes),
    ("XamlValidator rejects literals that cannot convert to the property type", TestXamlValidatorRejectsInvalidLiterals),
    ("XamlEmitter emits int, long, and double literals using the invariant culture", TestXamlEmitterNumericLiterals),
    ("XamlValidator accepts numeric literals only within their property type's range", TestXamlValidatorNumericLiteralRanges)
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
    Assert.Contains("CompiledBinding.Create(\"Title\", static (HelloViewModel source) => source.Title, null, BindingMode.OneWay)", emittedCSharp);
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

static void TestXamlEmitterLiteralsUsePropertyTypes()
{
    string xaml = TestRunner.ReadFixture("LiteralPropertiesView.xaml");
    ValidatedXamlDocument doc = XamlValidator.Validate(XamlParser.Parse("LiteralPropertiesView.xaml", xaml));

    Assert.Equal(0, doc.Diagnostics.Count);

    string emittedCSharp = XamlEmitter.Emit(doc);

    Assert.Contains("Id = \"true\";", emittedCSharp);
    Assert.Contains("IsVisible = false;", emittedCSharp);
    Assert.Contains("ClipToBounds = true;", emittedCSharp);
    Assert.Contains("Class = [\"first\", \"second\", \"third\"];", emittedCSharp);
    Assert.Contains("BooleanLikeText.Value = \"true\";", emittedCSharp);
    Assert.Contains("BooleanLikeText.AriaLabel = \"False\";", emittedCSharp);
    Assert.Contains("BooleanLikeButton.Text = \"false\";", emittedCSharp);
    Assert.Contains("BooleanLikeButton.IsEnabled = true;", emittedCSharp);
    Assert.Contains("BooleanLikeButton.CommandParameter = \"true\";", emittedCSharp);
    Assert.Contains("PathBinding.Bind(BooleanLikeButton, Button.CommandProperty, \"Save\"", emittedCSharp);
}

static void TestXamlEmitterNumericLiterals()
{
    ValidatedXamlElement root = new(
        "View",
        null,
        [
            new ValidatedXamlProperty("Count", XamlPropertyKind.Int32, "42", null),
            new ValidatedXamlProperty("Offset", XamlPropertyKind.Int32, "-7", null),
            new ValidatedXamlProperty("Ticks", XamlPropertyKind.Int64, "9000000000", null),
            new ValidatedXamlProperty("Ratio", XamlPropertyKind.Double, "1.5", null),
            new ValidatedXamlProperty("Scale", XamlPropertyKind.Double, "2", null),
            new ValidatedXamlProperty("Limit", XamlPropertyKind.Double, "1e3", null)
        ],
        [],
        "Children",
        []);
    ValidatedXamlDocument doc = new("NumericView.xaml", "TestApp.NumericView", "TestApp", "NumericView", null, "NumericView", root, []);

    string emittedCSharp = XamlEmitter.Emit(doc);

    Assert.Contains("Count = 42;", emittedCSharp);
    Assert.Contains("Offset = -7;", emittedCSharp);
    Assert.Contains("Ticks = 9000000000L;", emittedCSharp);
    Assert.Contains("Ratio = 1.5;", emittedCSharp);
    Assert.Contains("Scale = 2;", emittedCSharp);
    Assert.Contains("Limit = 1000;", emittedCSharp);
}

static void TestXamlValidatorNumericLiteralRanges()
{
    Assert.True(XamlValidator.IsValidLiteral(XamlPropertyKind.Int32, "-2147483648"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Int32, "2147483648"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Int32, "1.5"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Int32, "1,000"));

    Assert.True(XamlValidator.IsValidLiteral(XamlPropertyKind.Int64, "9223372036854775807"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Int64, "9223372036854775808"));

    Assert.True(XamlValidator.IsValidLiteral(XamlPropertyKind.Double, "-1.5e3"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Double, "1,5"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Double, "NaN"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Double, "Infinity"));
    Assert.False(XamlValidator.IsValidLiteral(XamlPropertyKind.Double, "1e999"));
}

static void TestXamlValidatorRejectsInvalidLiterals()
{
    string xaml = TestRunner.ReadFixture("InvalidLiteralsView.xaml");
    ValidatedXamlDocument doc = XamlValidator.Validate(XamlParser.Parse("InvalidLiteralsView.xaml", xaml));

    string[] messages = doc.Diagnostics
        .Where(static d => d.Id == "NXAML007")
        .Select(static d => d.GetMessage())
        .ToArray();

    Assert.Equal(3, messages.Length);
    Assert.Contains("'maybe' is not valid for property 'IsVisible' on control '<View>'", messages[0]);
    Assert.Contains("'1' is not valid for property 'IsEnabled' on control '<Text>'", messages[1]);
    Assert.Contains("'Save' is not valid for property 'Command' on control '<Button>'", messages[2]);
    Assert.False(doc.RootElement!.Properties.Any(static p => p.PropertyName == "IsVisible"), "Invalid literals must not reach the emitter.");
}
