using Microsoft.CodeAnalysis;
using NativeUI.Css.Compiler;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Css.Compiler.Tests",
[
    ("CssTokenizer and CssParser build structured AST with comment trivia and @media rules", TestCssTokenizerAndParserAst),
    ("CssValidator visitor validates rules and reports diagnostics on invalid properties/values", TestCssValidatorVisitorDiagnostics),
    ("CssEmitter uses Roslyn SyntaxFactory to emit clean C# without global:: clutter", TestCssEmitterCleanSyntaxFactoryOutput)
]);

return exitCode;

static void TestCssTokenizerAndParserAst()
{
    string css = TestRunner.ReadFixture("SampleTheme.css");
    CssStylesheetNode ast = CssParser.Parse("SampleTheme.css", css);

    Assert.Equal(4, ast.Statements.Count);
    Assert.True(ast.Statements[0] is CssStyleRuleNode);
    Assert.True(ast.Statements[1] is CssMediaRuleNode);
    Assert.True(ast.Statements[2] is CssStyleRuleNode);
    Assert.True(ast.Statements[3] is CssStyleRuleNode);
}

static void TestCssValidatorVisitorDiagnostics()
{
    string invalidCss = TestRunner.ReadFixture("InvalidTheme.css");
    CssStylesheetNode ast = CssParser.Parse("InvalidTheme.css", invalidCss);
    ValidatedCssStylesheet validated = CssValidator.Validate(ast);

    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NCSS002"), "Expected unsupported CSS property error NCSS002.");
    Assert.True(validated.Diagnostics.Any(static d => d.Id == "NCSS003"), "Expected invalid CSS value error NCSS003.");
}

static void TestCssEmitterCleanSyntaxFactoryOutput()
{
    string css = TestRunner.ReadFixture("SampleTheme.css");
    CssStylesheetNode ast = CssParser.Parse("SampleTheme.css", css);
    ValidatedCssStylesheet validated = CssValidator.Validate(ast);

    Assert.Equal(0, validated.Diagnostics.Count);

    string emittedCSharp = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);

    Assert.Contains("using NativeUI.Css;", emittedCSharp);
    Assert.Contains("using NativeUI.Media;", emittedCSharp);
    Assert.Contains("namespace NativeUI.Sample.Shared;", emittedCSharp);
    Assert.Contains("public partial class SampleThemeStyleSheet", emittedCSharp);
    Assert.Contains("Color.FromOklch(", emittedCSharp);
    Assert.Contains("Color.FromHsl(", emittedCSharp);
    Assert.False(emittedCSharp.Contains("global::", StringComparison.Ordinal), "Generated C# must not contain global:: clutter.");

    string internalCSharp = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Internal);
    Assert.Contains("internal partial class SampleThemeStyleSheet", internalCSharp);
}
