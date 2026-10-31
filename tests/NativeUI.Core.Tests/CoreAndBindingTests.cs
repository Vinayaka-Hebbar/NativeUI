using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Media;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Core.Tests",
[
    ("Color spaces convert accurately across sRGB, HSL, Oklab, and Oklch", TestColorSpacesAndConversions),
    ("ThemeValue<T> supports literal values and CSS variable references", TestThemeValueAndCssVariables),
    ("BindableProperty<T> is primary type and BindableObject raises PropertyChanged", TestBindablePropertyAndPropertyChanged),
    ("CompiledBinding updates target incrementally on OneWay and TwoWay changes", TestCompiledBindingOneWayAndTwoWay)
]);

return exitCode;

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
