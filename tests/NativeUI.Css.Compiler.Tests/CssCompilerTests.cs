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
    ("CssEmitter compiles cursor property and active/disabled pseudo-classes", TestButtonAndCursorCssEmission),
    ("Browser CSS gates color-scheme rules on the root's theme instead of the OS preference", TestBrowserCssThemeRules),
    ("CssEmitter compiles width: fit-content and rejects it on other properties", TestFitContentWidthEmission),
    ("CssEmitter compiles position relative, absolute, fixed, and offset lengths", TestPositionAndOffsetsCssEmission),
    ("CssCompiler compiles platform-specific styling rules", TestPlatformSpecificStyling)
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
    Assert.True(HasDiagnostic(".a:visited { color: #ffffff; }", "NCSS004"), "Expected unsupported pseudo-class error.");
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
    Assert.Contains("State(ElementStates.Hover)", emitted);
    Assert.Contains("using System.Collections.Immutable;", emitted);
    Assert.Contains("CssSelector.Descendant(", emitted);
    Assert.Contains(".PseudoElement(\"selection\")", emitted);
    Assert.Contains("span.title:hover", emitted);
    Assert.False(emitted.Contains("text.title:hover {", StringComparison.Ordinal), "Stylesheet text must use browser type names.");
}

static void TestFitContentWidthEmission()
{
    ValidatedCssStylesheet valid = CssValidator.Validate(CssParser.Parse("Fit.css", ".fit { width: fit-content; }"));
    Assert.Equal(0, valid.Diagnostics.Count);
    Assert.Contains("[\"width\"] = new CssFitContentValue()", CssEmitter.Emit(valid, "TestApp", Accessibility.Public));

    ValidatedCssStylesheet invalid = CssValidator.Validate(CssParser.Parse("Fit.css", ".fit { height: fit-content; }"));
    Assert.True(invalid.Diagnostics.Count > 0, "fit-content is supported only on width.");
}

static void TestBrowserCssThemeRules()
{
    const string Css = """
        :root { --surface: #fff; }
        @media (prefers-color-scheme: dark) {
            :root { --surface: #000; }
            text.title, .card:hover { color: #fff; }
        }
        @media (prefers-color-scheme: light) {
            .card { color: #000; }
        }
        """;

    ValidatedCssStylesheet validated = CssValidator.Validate(CssParser.Parse("Themed.css", Css));

    Assert.Equal(0, validated.Diagnostics.Count);
    Assert.False(validated.BrowserCss.Contains("@media", StringComparison.Ordinal), "Theme rules must not follow the OS preference.");
    Assert.Contains(":root { --surface: #fff; }", validated.BrowserCss);
    Assert.Contains("native-ui[data-theme=\"dark\"] { --surface: #000; }", validated.BrowserCss);
    Assert.Contains(":where(native-ui[data-theme=\"dark\"]) span.title, :where(native-ui[data-theme=\"dark\"]) .card:hover { color: #fff; }", validated.BrowserCss);
    Assert.Contains(":where(native-ui[data-theme=\"light\"]) .card { color: #000; }", validated.BrowserCss);
    Assert.Contains("@media (prefers-color-scheme: dark)", validated.RawCss);
}

static void TestButtonAndCursorCssEmission()
{
    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("ButtonTheme.css", TestRunner.ReadFixture("ButtonTheme.css")));

    Assert.Equal(0, validated.Diagnostics.Count);
    string emitted = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);

    Assert.Contains("new CssCursorValue(Cursor.Pointer)", emitted);
    Assert.Contains("new CssCursorValue(Cursor.NotAllowed)", emitted);
    Assert.Contains(".State(ElementStates.Pressed)", emitted);
    Assert.Contains(".State(ElementStates.Disabled)", emitted);
}

static void TestPositionAndOffsetsCssEmission()
{
    const string css = """
        .badge {
            position: absolute;
            top: 10px;
            right: 15px;
        }
        .overlay {
            position: fixed;
            bottom: 0px;
            left: 20px;
        }
        """;

    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("PositionTheme.css", css));

    Assert.Equal(0, validated.Diagnostics.Count);
    string emitted = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);

    Assert.Contains("new CssPositionValue(Position.Absolute)", emitted);
    Assert.Contains("new CssPositionValue(Position.Fixed)", emitted);
    Assert.Contains("new CssLengthValue(10, CssLengthUnit.Px)", emitted);
    Assert.Contains("new CssLengthValue(15, CssLengthUnit.Px)", emitted);
    Assert.Contains("new CssLengthValue(0, CssLengthUnit.Px)", emitted);
    Assert.Contains("new CssLengthValue(20, CssLengthUnit.Px)", emitted);
}

static void TestPlatformSpecificStyling()
{
    const string css = """
        [data-platform="android"] .toolbar {
            height: 56px;
        }

        [data-platform="macos"] .toolbar {
            height: 56px;
        }

        [data-platform="web"] .toolbar {
            height: 44px;
        }
        """;

    ValidatedCssStylesheet validated = CssValidator.Validate(
        CssParser.Parse("PlatformTheme.css", css));

    Assert.Equal(0, validated.Diagnostics.Count);
    Assert.Equal(3, validated.Rules.Count);

    Assert.NotNull(validated.Rules[0].Ancestors);
    Assert.Equal("data-platform", validated.Rules[0].Ancestors![0].Attributes![0].Name);
    Assert.Equal("android", validated.Rules[0].Ancestors![0].Attributes![0].Value);

    Assert.NotNull(validated.Rules[1].Ancestors);
    Assert.Equal("data-platform", validated.Rules[1].Ancestors![0].Attributes![0].Name);
    Assert.Equal("macos", validated.Rules[1].Ancestors![0].Attributes![0].Value);

    Assert.NotNull(validated.Rules[2].Ancestors);
    Assert.Equal("data-platform", validated.Rules[2].Ancestors![0].Attributes![0].Name);
    Assert.Equal("web", validated.Rules[2].Ancestors![0].Attributes![0].Value);

    string emitted = CssEmitter.Emit(validated, "NativeUI.Sample.Shared", Accessibility.Public);
    Assert.Contains("CssSelector.Attribute(\"data-platform\", \"android\")", emitted);
    Assert.Contains("CssSelector.Attribute(\"data-platform\", \"macos\")", emitted);
    Assert.Contains("CssSelector.Attribute(\"data-platform\", \"web\")", emitted);
}

