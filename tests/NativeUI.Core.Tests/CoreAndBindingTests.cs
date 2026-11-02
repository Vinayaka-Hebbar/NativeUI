using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Core.Tests",
[
    ("Animation cancellation is idempotent and completes after failure", NativeUI.Core.Tests.AnimationTests.VerifyCancellationAndCompletion),
    ("Completed animation releases its cancellation callback", NativeUI.Core.Tests.AnimationTests.VerifyCompletedAnimationReleasesCallback),
    ("Application resolves CurrentTheme, raises changes once, and carries state to a replacement", TestApplicationTheme),
    ("Color spaces convert accurately across sRGB, HSL, Oklab, and Oklch", TestColorSpacesAndConversions),
    ("ThemeValue<T> supports literal values and CSS variable references", TestThemeValueAndCssVariables),
    ("BindableProperty<T> is primary type and BindableObject raises PropertyChanged", TestBindablePropertyAndPropertyChanged),
    ("CompiledBinding updates target incrementally on OneWay and TwoWay changes", TestCompiledBindingOneWayAndTwoWay),
    ("UnicodeTextSegmentation computes grapheme clusters and UAX #29 word boundaries", TestUnicodeTextSegmentation),
    ("BidiAlgorithm resolves mixed LTR/RTL, paired brackets, and directional isolates", TestBidiAlgorithm),
    ("LineBreakEnumerator computes UAX #14 optional and mandatory break opportunities", TestLineBreakEnumerator),
    ("Unicode tables preserve private-use defaults and range boundaries", NativeUI.Core.Tests.UnicodeDataTablesTests.VerifyPrivateUseDefaults),
    ("Unicode bracket lookups match the pinned official data", NativeUI.Core.Tests.UnicodeDataTablesTests.VerifyBracketMappings),
    ("ComputedBindableProperty evaluates dynamically, rejects mutations, and triggers subscriber hooks", TestComputedBindableProperty),
    ("EventManager registers routed events, traverses inheritance, and creates on-demand observers", TestEventManager)
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

static void TestComputedBindableProperty()
{
    ComputedSample obj = new();
    Assert.Equal(42, obj.ComputedValue);
    Assert.Equal(42, obj.GetValue(ComputedSample.ComputedValueProperty));
    Assert.Equal(42, obj.GetValue((BindableProperty<int>)ComputedSample.ComputedValueProperty));

    int notifications = 0;
    obj.PropertyChanged += (_, e) =>
    {
        if (e.Property == ComputedSample.ComputedValueProperty)
        {
            notifications++;
        }
    };

    obj.RaiseComputedChanged(42, 100);
    Assert.Equal(1, notifications);

    // Verify observation lifecycle through CreateComputed
    Assert.False(ComputedSample.ObservedValueProperty.HasSubscribers(obj));
    Assert.Equal(0, obj.ObserveStartCount);
    Assert.Equal(0, obj.ObserveStopCount);

    IDisposable sub1 = ComputedSample.ObservedValueProperty.Subscribe(obj);
    Assert.True(ComputedSample.ObservedValueProperty.HasSubscribers(obj));
    Assert.Equal(1, obj.ObserveStartCount);
    Assert.Equal(0, obj.ObserveStopCount);

    IDisposable sub2 = ComputedSample.ObservedValueProperty.Subscribe(obj);
    Assert.Equal(1, obj.ObserveStartCount); // Shared, not called again
    Assert.Equal(0, obj.ObserveStopCount);

    sub1.Dispose();
    Assert.True(ComputedSample.ObservedValueProperty.HasSubscribers(obj));
    Assert.Equal(0, obj.ObserveStopCount);

    sub2.Dispose();
    Assert.False(ComputedSample.ObservedValueProperty.HasSubscribers(obj));
    Assert.Equal(1, obj.ObserveStopCount);
}

static void TestEventManager()
{
    RoutedEvent<RoutedEventArgs> routedEvent = EventManager.RegisterRoutedEvent<TestEventOwnerBase, RoutedEventArgs>(
        "TestCustom",
        RoutingStrategy.Bubble);

    int startCount = 0;
    int stopCount = 0;

    EventManager.RegisterEventObserver<TestEventOwnerBase>(
        routedEvent,
        _ =>
        {
            startCount++;
            return new TestEventToken(() => stopCount++);
        });

    bool duplicateThrown = false;
    try
    {
        EventManager.RegisterEventObserver<TestEventOwnerBase>(routedEvent, _ => null);
    }
    catch (InvalidOperationException)
    {
        duplicateThrown = true;
    }
    Assert.True(duplicateThrown, "Expected duplicate observer registration to throw InvalidOperationException.");

    TestEventOwnerDerived derived = new();
    Assert.Equal(0, startCount);

    using (IDisposable? token = EventManager.CreateObserver(derived, routedEvent))
    {
        Assert.NotNull(token);
        Assert.Equal(1, startCount);
        Assert.Equal(0, stopCount);
    }

    Assert.Equal(1, stopCount);

    RoutedEvent<RoutedEventArgs> unregisteredEvent = RoutedEvent.Register<TestEventOwnerBase, RoutedEventArgs>(
        "Unregistered",
        RoutingStrategy.Direct);

    Assert.Null(EventManager.CreateObserver(derived, unregisteredEvent));
}

file class TestEventOwnerBase
{
}

file sealed class TestEventOwnerDerived : TestEventOwnerBase
{
}

file sealed class TestEventToken(Action onDispose) : IDisposable
{
    private readonly Action _onDispose = onDispose;

    public void Dispose()
    {
        _onDispose();
    }
}

file sealed class ComputedSample : BindableObject
{
    public int BaseValue { get; set; } = 42;
    public int ObserveStartCount;
    public int ObserveStopCount;

    public static readonly ComputedBindableProperty<ComputedSample, int> ComputedValueProperty =
        new(nameof(ComputedValue), static s => s.BaseValue);

    public static readonly ComputedBindableProperty<ComputedSample, int> ObservedValueProperty =
        BindableProperty.CreateComputed<ComputedSample, int>(
            nameof(ObservedValueProperty),
            static s => s.BaseValue,
            observe: static s =>
            {
                s.ObserveStartCount++;
                return new ObservationToken(s);
            });

    public int ComputedValue => GetValue(ComputedValueProperty);

    public void RaiseComputedChanged(int oldValue, int newValue)
    {
        NotifyPropertyChanged(ComputedValueProperty, oldValue, newValue);
    }

    private sealed class ObservationToken(ComputedSample owner) : IDisposable
    {
        public void Dispose()
        {
            owner.ObserveStopCount++;
        }
    }
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
