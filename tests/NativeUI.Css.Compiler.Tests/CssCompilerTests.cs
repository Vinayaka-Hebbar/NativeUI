using Microsoft.CodeAnalysis;
using NativeUI.Css.Compiler;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Css.Compiler.Tests",
[
    ("CssTokenizer and CssParser build structured AST with comment trivia and @media rules", TestCssTokenizerAndParserAst),
    ("CssValidator visitor validates rules and reports diagnostics on invalid properties/values", TestCssValidatorVisitorDiagnostics),
    ("CssEmitter uses Roslyn SyntaxFactory to emit clean C# without global:: clutter", TestCssEmitterCleanSyntaxFactoryOutput),
    ("CssParser compiles hover, descendant, universal, and ::selection selectors", TestStateAndDescendantSelectors),
    ("CssValidator diagnoses unsupported pseudo-selectors, combinators, and ::selection properties", TestUnsupportedSelectorDiagnostics),
    ("CssEmitter translates only type selectors for browser CSS and emits selector metadata", TestBrowserCssAndSelectorEmission),
    ("CssEmitter compiles cursor property and active/disabled pseudo-classes", TestButtonAndCursorCssEmission)
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
    Assert.Contains("public partial class SampleTheme", emittedCSharp);
    Assert.Contains("partial void AttachStyles()", emittedCSharp);
    Assert.Contains("StyleSheet = SampleThemeStyleSheet.Instance;", emittedCSharp);
    Assert.Contains("Color.FromOklch(", emittedCSharp);
    Assert.Contains("Color.FromHsl(", emittedCSharp);
    Assert.False(emittedCSharp.Contains("global::", StringComparison.Ordinal), "Generated C# must not contain global:: clutter.");

    string internalCSharp = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Internal);
    Assert.Contains("internal partial class SampleThemeStyleSheet", internalCSharp);
}

static void TestStateAndDescendantSelectors()
{
    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("HoverSelectors.css", TestRunner.ReadFixture("HoverSelectors.css")));

    Assert.Equal(0, validated.Diagnostics.Count);
    Assert.Equal(5, validated.Rules.Count);

    ValidatedCssRule hover = validated.Rules[0];
    Assert.Equal("text", hover.ElementType);
    Assert.Equal("Hover", hover.RequiredStates![0]);

    ValidatedCssRule descendant = validated.Rules[1];
    Assert.Equal(0, descendant.RequiredStates!.Count);
    Assert.Equal("card", descendant.Ancestors![0].Classes[0]);
    Assert.Equal("Hover", descendant.Ancestors[0].RequiredStates[0]);

    ValidatedCssRule selection = validated.Rules[2];
    Assert.Equal(CssParsedPseudoElement.Selection, selection.PseudoElement);
    Assert.Equal("view", selection.Ancestors![0].ElementType);
    Assert.Equal("card", selection.Ancestors[1].Classes[0]);

    ValidatedCssRule universal = validated.Rules[3];
    Assert.Null(universal.ElementType);
    Assert.Equal(0, universal.Classes.Count);
}

static void TestUnsupportedSelectorDiagnostics()
{
    Assert.True(HasDiagnostic(".a:focus { color: #ffffff; }", "NCSS004"), "Expected unsupported pseudo-class error.");
    Assert.True(HasDiagnostic(".a::before { color: #ffffff; }", "NCSS004"), "Expected unsupported pseudo-element error.");
    Assert.True(HasDiagnostic(".a > .b { color: #ffffff; }", "NCSS004"), "Expected unsupported child combinator error.");
    Assert.True(HasDiagnostic(".a + .b { color: #ffffff; }", "NCSS004"), "Expected unsupported next-sibling combinator error.");
    Assert.True(HasDiagnostic(".a ~ .b { color: #ffffff; }", "NCSS004"), "Expected unsupported subsequent-sibling combinator error.");
    Assert.True(HasDiagnostic(".a:has(.b) { color: #ffffff; }", "NCSS004"), "Expected unsupported :has() error.");
    Assert.True(HasDiagnostic(".a::selection .b { color: #ffffff; }", "NCSS004"), "Expected non-terminal ::selection error.");
    Assert.True(HasDiagnostic(".a::selection { font-size: 12px; }", "NCSS005"), "Expected unsupported ::selection property error.");
    Assert.True(HasDiagnostic(".a::selection { opacity: 0.5; }", "NCSS005"), "Expected unsupported ::selection property error.");
    Assert.False(HasDiagnostic(".a::selection { color: #ffffff; background-color: #000000; }", "NCSS005"));

    static bool HasDiagnostic(string css, string id)
    {
        ValidatedCssStylesheet validated = CssValidator.Validate(CssParser.Parse("Inline.css", css));
        return validated.Diagnostics.Any(d => d.Id == id);
    }
}

static void TestBrowserCssAndSelectorEmission()
{
    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("HoverSelectors.css", TestRunner.ReadFixture("HoverSelectors.css")));

    Assert.Contains("span.title:hover", validated.BrowserCss);
    Assert.Contains("div .card:hover span.title::selection", validated.BrowserCss);
    Assert.Contains(".text {", validated.BrowserCss);
    Assert.Contains("text.title:hover", validated.RawCss);

    string emitted = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);
    Assert.Contains("using NativeUI.Core;", emitted);
    Assert.Contains("requiredStates: ElementStates.Hover", emitted);
    Assert.Contains("using System.Collections.Immutable;", emitted);
    Assert.Contains("ancestors: ImmutableArray.Create(new CompoundSelector(", emitted);
    Assert.Contains("pseudoElement: CssPseudoElement.Selection", emitted);
    Assert.Contains("span.title:hover", emitted);
    Assert.False(emitted.Contains("text.title:hover {", StringComparison.Ordinal), "Stylesheet text must use browser type names.");
}

static void TestButtonAndCursorCssEmission()
{
    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("ButtonTheme.css", TestRunner.ReadFixture("ButtonTheme.css")));

    Assert.Equal(0, validated.Diagnostics.Count);
    string emitted = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);

    Assert.Contains("new CssCursorValue(Cursor.Pointer)", emitted);
    Assert.Contains("new CssCursorValue(Cursor.NotAllowed)", emitted);
    Assert.Contains("requiredStates: ElementStates.Pressed", emitted);
    Assert.Contains("requiredStates: ElementStates.Disabled", emitted);
}
