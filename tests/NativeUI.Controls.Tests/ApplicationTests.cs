using NativeUI.Core;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies how <see cref="Application"/> members relate to shared controls.
/// </summary>
internal static class ApplicationTests
{
    public static void VerifyMainView()
    {
        using View view = new();
        SingleViewApplication application = new() { MainView = view };

        Assert.Same(view, application.MainView);
        Assert.Null(new SingleViewApplication().MainView);
    }

    public static void VerifyCurrentThemeBinding()
    {
        using TestTheme testTheme = new();
        using Text text = new() { DataContext = Application.Current };
        using IDisposable binding = text.SetBinding(
            Text.ValueProperty,
            new CompiledBinding<Application, string>(nameof(Application.CurrentTheme), static application => application.CurrentTheme.ToString()));
        Assert.Equal("Light", text.Value);

        testTheme.Apply(text, Theme.Dark);
        Assert.Equal("Dark", text.Value);
    }

    private sealed class SingleViewApplication : Application
    {
        protected override Theme SystemTheme => Theme.Light;
    }
}
