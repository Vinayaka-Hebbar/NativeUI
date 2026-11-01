namespace NativeUI.Core;

/// <summary>
/// Represents the running application and owns its theme. A platform host installs its own implementation by
/// assigning <see cref="Current"/>. Members must be used on the UI thread, which <see cref="BindableObject.Dispatcher"/>
/// represents. <see cref="RequestedTheme"/> and <see cref="CurrentTheme"/> can be binding sources.
/// </summary>
public abstract class Application : BindableObject
{
    /// <summary>
    /// Identifies the <see cref="RequestedTheme"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Theme> RequestedThemeProperty =
        BindableProperty.Create<Application, Theme>(
            nameof(RequestedTheme),
            defaultValue: Theme.Default,
            propertyChanged: static (owner, _, _) => owner.UpdateCurrentTheme());

    // Private so that only this class can write the read-only CurrentTheme. Theme.Default means not resolved yet.
    private static readonly BindableProperty<Theme> CurrentThemeProperty =
        BindableProperty.Create<Application, Theme>(nameof(CurrentTheme), defaultValue: Theme.Default);

    private static Application s_current = new HeadlessApplication();
    private string? _platform;

    /// <summary>
    /// Gets or sets the running platform name (such as <c>"macos"</c>, <c>"windows"</c>, <c>"linux"</c>, <c>"web"</c>, <c>"android"</c>, or <c>"ios"</c>).
    /// </summary>
    public string Platform
    {
        get => _platform ?? DetectDefaultPlatform();
        set => _platform = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets or sets the running platform name. Alias for <see cref="Platform"/>.
    /// </summary>
    public string Platfrom
    {
        get => Platform;
        set => Platform = value;
    }

    /// <summary>
    /// Determines whether the specified platform name matches the active running platform.
    /// <c>"web"</c> and <c>"browser"</c> are treated as synonyms.
    /// </summary>
    /// <param name="platform">The lowercase platform name to test.</param>
    public bool IsPlatform(string platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        string current = Platform.Trim();
        string normalized = platform.Trim();
        if (string.Equals(normalized, current, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((string.Equals(normalized, "web", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalized, "browser", StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(current, "web", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current, "browser", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string DetectDefaultPlatform()
    {
        if (OperatingSystem.IsBrowser())
        {
            return "web";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsAndroid())
        {
            return "android";
        }

        if (OperatingSystem.IsIOS())
        {
            return "ios";
        }

        return "unknown";
    }

    /// <summary>
    /// Gets the running application, which is never <see langword="null"/>. Before a platform host starts, this is an
    /// application whose system theme is <see cref="Theme.Light"/>. A new application takes over
    /// <see cref="RequestedTheme"/>, <see cref="BindableObject.Dispatcher"/>, and the <see cref="CurrentThemeChanged"/>
    /// subscribers. The replaced application keeps reporting the theme properties of its replacement, so bindings to it stay current.
    /// </summary>
    public static Application Current
    {
        get => s_current;
        protected set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(s_current, value))
            {
                return;
            }

            Application previous = s_current;
            value.Dispatcher = previous.Dispatcher;
            value._platform = previous._platform;
            value.SetValue(CurrentThemeProperty, previous.CurrentTheme);
            value.CurrentThemeChanged += previous.CurrentThemeChanged;
            previous.CurrentThemeChanged = null;
            value.PropertyChanged += previous.OnReplacementPropertyChanged;
            s_current = value;
            value.SetValue(RequestedThemeProperty, previous.RequestedTheme);
            value.UpdateCurrentTheme();
        }
    }

    /// <summary>
    /// Gets the application's main view, or <see langword="null"/> when none is set. A platform host that creates the
    /// application sets it to the view it mounts: the root view on Web, and the content of the main window on desktop.
    /// </summary>
    public IView? MainView { get; init; }

    /// <summary>
    /// Gets or sets the requested theme. <see cref="Theme.Default"/> follows the system theme.
    /// </summary>
    public Theme RequestedTheme
    {
        get => GetValue(RequestedThemeProperty);
        set => SetValue(RequestedThemeProperty, value);
    }

    /// <summary>
    /// Gets the effective theme, which is always <see cref="Theme.Light"/> or <see cref="Theme.Dark"/>.
    /// </summary>
    public Theme CurrentTheme
    {
        get
        {
            Theme theme = GetValue(CurrentThemeProperty);
            if (theme != Theme.Default)
            {
                return theme;
            }

            theme = ResolveTheme();
            SetValue(CurrentThemeProperty, theme);
            return theme;
        }
    }

    /// <summary>
    /// Occurs after <see cref="CurrentTheme"/> changes.
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? CurrentThemeChanged;

    /// <summary>
    /// Gets the theme reported by the operating system or browser.
    /// </summary>
    protected abstract Theme SystemTheme { get; }

    /// <summary>
    /// Reevaluates <see cref="CurrentTheme"/> on the UI thread after <see cref="SystemTheme"/> changes. Can be called from any thread.
    /// </summary>
    protected void NotifySystemThemeChanged()
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateCurrentTheme();
        }
        else
        {
            Dispatcher.Invoke(UpdateCurrentTheme);
        }
    }

    private Theme ResolveTheme()
    {
        Theme requestedTheme = RequestedTheme;
        return requestedTheme != Theme.Default ? requestedTheme : SystemTheme;
    }

    private void UpdateCurrentTheme()
    {
        // A replaced application mirrors its replacement and no longer resolves a theme of its own.
        if (!ReferenceEquals(s_current, this))
        {
            return;
        }

        Theme oldTheme = CurrentTheme;
        Theme newTheme = ResolveTheme();
        if (oldTheme == newTheme)
        {
            return;
        }

        SetValue(CurrentThemeProperty, newTheme);
        CurrentThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, newTheme));
    }

    private void OnReplacementPropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.Property, CurrentThemeProperty) || ReferenceEquals(e.Property, RequestedThemeProperty))
        {
            SetValue((BindableProperty<Theme>)e.Property, (Theme)e.NewValue!);
        }
    }

    private sealed class HeadlessApplication : Application
    {
        protected override Theme SystemTheme => Theme.Light;
    }
}
