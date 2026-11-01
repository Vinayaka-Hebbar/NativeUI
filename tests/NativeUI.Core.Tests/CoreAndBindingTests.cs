using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Core.Tests",
[
    ("Application resolves CurrentTheme, raises changes once, and carries state to a replacement", TestApplicationTheme),
    ("Color spaces convert accurately across sRGB, HSL, Oklab, and Oklch", TestColorSpacesAndConversions),
    ("ThemeValue<T> supports literal values and CSS variable references", TestThemeValueAndCssVariables),
    ("BindableProperty<T> is primary type and BindableObject raises PropertyChanged", TestBindablePropertyAndPropertyChanged),
    ("CompiledBinding updates target incrementally on OneWay and TwoWay changes", TestCompiledBindingOneWayAndTwoWay),
    ("UnicodeTextSegmentation computes grapheme clusters and UAX #29 word boundaries", TestUnicodeTextSegmentation),
    ("BidiAlgorithm resolves mixed LTR/RTL, paired brackets, and directional isolates", TestBidiAlgorithm),
    ("LineBreakEnumerator computes UAX #14 optional and mandatory break opportunities", TestLineBreakEnumerator),
    ("Unicode tables preserve private-use defaults and range boundaries", NativeUI.Core.Tests.UnicodeDataTablesTests.VerifyPrivateUseDefaults),
    ("Unicode bracket lookups match the pinned official data", NativeUI.Core.Tests.UnicodeDataTablesTests.VerifyBracketMappings)
]);

return exitCode;

static void TestApplicationTheme()
{
    Application initial = Application.Current;
    Assert.Equal(Theme.Default, initial.RequestedTheme);
    Assert.Equal(Theme.Light, initial.CurrentTheme);

    List<(Theme Old, Theme New)> changes = [];
    initial.CurrentThemeChanged += (_, e) => changes.Add((e.OldTheme, e.NewTheme));
    List<string?> properties = [];
    initial.PropertyChanged += (_, e) => properties.Add(e.Property.Name);

    initial.RequestedTheme = Theme.Dark;
    initial.RequestedTheme = Theme.Dark;
    Assert.Equal("CurrentTheme,RequestedTheme", string.Join(',', properties));
    Assert.Equal(Theme.Dark, initial.CurrentTheme);
    Assert.Equal(1, changes.Count);
    Assert.Equal((Theme.Light, Theme.Dark), changes[0]);

    initial.RequestedTheme = Theme.Default;
    Assert.Equal(Theme.Light, initial.CurrentTheme);
    Assert.Equal(2, changes.Count);

    initial.RequestedTheme = Theme.Light;
    SampleBindable boundToInitial = new() { DataContext = initial };
    using IDisposable binding = boundToInitial.SetBinding(
        SampleBindable.TextProperty,
        new CompiledBinding<Application, string>(nameof(Application.CurrentTheme), static application => application.CurrentTheme.ToString()));
    Assert.Equal("Light", boundToInitial.GetValue(SampleBindable.TextProperty));

    FakeApplication replacement = new(Theme.Dark);
    Assert.Same(replacement, Application.Current);
    Assert.Equal(Theme.Light, replacement.RequestedTheme);
    Assert.Equal(Theme.Light, replacement.CurrentTheme);

    replacement.ChangeSystemTheme(Theme.Light);
    replacement.ChangeSystemTheme(Theme.Dark);
    Assert.Equal(Theme.Light, replacement.CurrentTheme);
    Assert.Equal(2, changes.Count);

    replacement.RequestedTheme = Theme.Default;
    Assert.Equal(Theme.Dark, replacement.CurrentTheme);
    Assert.Equal(3, changes.Count);
    Assert.Equal((Theme.Light, Theme.Dark), changes[2]);

    properties.Clear();
    replacement.ChangeSystemTheme(Theme.Light);
    Assert.Equal(Theme.Light, replacement.CurrentTheme);
    Assert.Equal(4, changes.Count);
    Assert.Equal("CurrentTheme", string.Join(',', properties));
    Assert.Equal("Light", boundToInitial.GetValue(SampleBindable.TextProperty));

    replacement.RequestedTheme = Theme.Dark;
    Assert.Equal("Dark", boundToInitial.GetValue(SampleBindable.TextProperty));
    Assert.Equal(Theme.Dark, initial.RequestedTheme);
    Assert.Equal(replacement.Dispatcher, initial.Dispatcher);
    FakeApplication darkSystem = new(Theme.Light);
    Assert.Equal(Theme.Dark, darkSystem.CurrentTheme);
    darkSystem.RequestedTheme = Theme.Default;
    Assert.Equal(Theme.Light, darkSystem.CurrentTheme);
}

static void TestColorSpacesAndConversions()
{
    Color blue = Color.FromRgbBytes(37, 99, 235);
    Assert.Equal(ColorSpace.Srgb, blue.Space);
    Assert.Equal("#2563eb", blue.ToCssString());

    Color hslColor = Color.FromHsl(217, 0.91, 0.60);
    Assert.Equal(ColorSpace.Hsl, hslColor.Space);
    (byte hslR, _, byte hslB, _) = hslColor.ToSrgbBytes();
    Assert.True(hslR > 40 && hslB > 200);

    Color oklabColor = Color.FromOklab(0.65, -0.05, -0.15);
    Assert.Equal(ColorSpace.Oklab, oklabColor.Space);
    Assert.Equal(1.0, oklabColor.Alpha);

    Color oklchColor = Color.FromOklch(0.62, 0.19, 255);
    Assert.Equal(ColorSpace.Oklch, oklchColor.Space);
    (byte lchR, _, byte lchB, _) = oklchColor.ToSrgbBytes();
    Assert.True(lchB > lchR);
}

static void TestThemeValueAndCssVariables()
{
    ThemeValue<Color> directColor = Colors.Blue;
    Assert.False(directColor.IsThemeKey);
    Assert.Equal(Colors.Blue, directColor.Value);

    ThemeValue<Color> tokenColor = new("--accent", Colors.Indigo);
    Assert.True(tokenColor.IsThemeKey);
    Assert.Equal("--accent", tokenColor.Key);

    CssVariables vars = new();
    Assert.Equal(Colors.Indigo, vars.ResolveColor(tokenColor));

    vars.Set("--accent", new CssColorValue(Colors.Green));
    Assert.Equal(Colors.Green, vars.ResolveColor(tokenColor));
}

static void TestBindablePropertyAndPropertyChanged()
{
    BindableProperty<string> labelProperty = BindableProperty.Create(
        "Label",
        typeof(SampleBindable),
        defaultValue: "Initial");

    SampleBindable target = new();
    Assert.Equal("Initial", target.GetValue(labelProperty));

    int propertyChangedCount = 0;
    BindableProperty? changedProp = null;
    target.PropertyChanged += (_, e) =>
    {
        propertyChangedCount++;
        changedProp = e.Property;
    };

    bool updated = target.SetValue(labelProperty, "Changed");
    Assert.True(updated);
    Assert.Equal(1, propertyChangedCount);
    Assert.Same(labelProperty, changedProp!);

    // Setting identical value does not re-fire PropertyChanged
    bool duplicate = target.SetValue(labelProperty, "Changed");
    Assert.False(duplicate);
    Assert.Equal(1, propertyChangedCount);
}

static void TestCompiledBindingOneWayAndTwoWay()
{
    SampleBindable target = new();
    SampleViewModel vm = new() { Title = "First" };
    target.DataContext = vm;

    CompiledBinding<SampleViewModel, string> binding = new(
        nameof(SampleViewModel.Title),
        static s => s.Title,
        static (s, v) => s.Title = v,
        BindingMode.TwoWay);

    using IDisposable sub = target.SetBinding(SampleBindable.TextProperty, binding);
    Assert.Equal("First", target.GetValue(SampleBindable.TextProperty));

    vm.Title = "FromViewModel";
    Assert.Equal("FromViewModel", target.GetValue(SampleBindable.TextProperty));

    target.SetValue(SampleBindable.TextProperty, "FromControl");
    Assert.Equal("FromControl", vm.Title);
}

static void TestUnicodeTextSegmentation()
{
    string emojiText = "A👩‍💻B";
    ImmutableArray<TextRange> clusters = UnicodeTextSegmentation.GetGraphemeClusters(emojiText, 0, emojiText.Length);
    Assert.Equal(3, clusters.Length);
    Assert.Equal(new TextRange(0, 1), clusters[0]);
    Assert.Equal(new TextRange(1, emojiText.Length - 2), clusters[1]);
    Assert.Equal(new TextRange(emojiText.Length - 1, 1), clusters[2]);

    string sample = "We don't pay 1,234.56 today";
    TextRange contraction = UnicodeTextSegmentation.GetWordRangeAt(sample, 4);
    Assert.Equal("don't", sample.Substring(contraction.Start, contraction.Length));

    TextRange numeric = UnicodeTextSegmentation.GetWordRangeAt(sample, 15);
    Assert.Equal("1,234.56", sample.Substring(numeric.Start, numeric.Length));
}

static void TestBidiAlgorithm()
{
    // Mixed English, Arabic, and numbers: Arabic letters get odd (RTL) level 1, numbers after Arabic get level 2
    string mixedArabicNumbers = "English العربية 123";
    ImmutableArray<BiDiTextRun> runs1 = UnicodeTextSegmentation.AnalyzeParagraphBiDi(
        mixedArabicNumbers,
        0,
        mixedArabicNumbers.Length,
        out bool baseIsRtl1);
    Assert.False(baseIsRtl1);
    Assert.Equal((byte)0, runs1[0].EmbeddingLevel);
    Assert.True(runs1.Any(static r => r.EmbeddingLevel == 1), "Expected Arabic run to have level 1.");
    Assert.Equal((byte)2, runs1[^1].EmbeddingLevel);

    // Paired brackets enclosing Hebrew inside LTR paragraph
    string bracketHebrew = "Hello (שלום) world";
    ImmutableArray<BiDiTextRun> runs2 = UnicodeTextSegmentation.AnalyzeParagraphBiDi(
        bracketHebrew,
        0,
        bracketHebrew.Length,
        out bool baseIsRtl2);
    Assert.False(baseIsRtl2);
    Assert.True(runs2.Any(static r => r.IsRightToLeft), "Expected Hebrew run inside paired brackets to resolve to RTL.");

    // Directional isolate RLI ... PDI
    string isolated = "ABC \u2067مرحبا\u2069 XYZ";
    ImmutableArray<BiDiTextRun> runs3 = UnicodeTextSegmentation.AnalyzeParagraphBiDi(
        isolated,
        0,
        isolated.Length,
        out bool baseIsRtl3);
    Assert.False(baseIsRtl3);
    Assert.True(runs3.Any(static r => r.IsRightToLeft), "Expected isolated Arabic content to resolve to an odd RTL embedding level.");
    Assert.Equal((byte)0, runs3[^1].EmbeddingLevel);
}

static void TestLineBreakEnumerator()
{
    string text = "Hello (world)\r\nNext\u00A0Word";
    ImmutableArray<TextRange> clusters = UnicodeTextSegmentation.GetGraphemeClusters(text, 0, text.Length);
    ImmutableArray<LineBreakOpportunity> breaks = LineBreakEnumerator.GetBreakOpportunities(text, clusters);

    // Must allow breaking after space before '(' ("Hello | (world)"), but not immediately after '('
    int openParenPos = text.IndexOf('(');
    Assert.True(breaks.Any(b => b.TextPosition == openParenPos && !b.IsRequired), "Expected optional break opportunity before opening parenthesis after space.");
    Assert.False(breaks.Any(b => b.TextPosition == openParenPos + 1), "Must not break immediately after opening parenthesis.");

    // CRLF must produce a single mandatory break at the end of the CRLF sequence
    int afterCrLf = text.IndexOf("Next", StringComparison.Ordinal);
    Assert.True(breaks.Any(b => b.TextPosition == afterCrLf && b.IsRequired), "Expected mandatory break after CRLF.");

    // Non-breaking space U+00A0 must not allow a break before or after it
    int nbspIndex = text.IndexOf('\u00A0');
    Assert.False(breaks.Any(b => b.TextPosition == nbspIndex || b.TextPosition == nbspIndex + 1), "Must not break around non-breaking space.");
}

file sealed class SampleBindable : BindableObject
{
    public static readonly BindableProperty<SampleBindable, string> TextProperty =
        BindableProperty.Create<SampleBindable, string>("Text", string.Empty);
}

file sealed class SampleViewModel : ObservableObject
{
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}

sealed class FakeApplication : Application
{
    private Theme _systemTheme;

    public FakeApplication(Theme systemTheme)
    {
        _systemTheme = systemTheme;
        Current = this;
    }

    protected override Theme SystemTheme => _systemTheme;

    public void ChangeSystemTheme(Theme systemTheme)
    {
        _systemTheme = systemTheme;
        NotifySystemThemeChanged();
    }
}
