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
    /// Gets the view shown by a single-view application, or <see langword="null"/> when none is set.
    /// Desktop applications show windows instead and leave this unset.
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
