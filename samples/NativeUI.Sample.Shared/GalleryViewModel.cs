using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NativeUI.Core;
using ObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace NativeUI.Sample;

/// <summary>
/// Gallery ViewModel managing navigation items, active page resolution, and theme toggling.
/// </summary>
public partial class GalleryViewModel : ObservableObject
{
    private NavigationItemViewModel? _selectedPage;
    private object? _currentPage;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _themeButtonText = "Switch to Dark Mode";

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryViewModel"/> class.
    /// </summary>
    public GalleryViewModel()
    {
        IsDarkMode = Application.Current.CurrentTheme == Theme.Dark;
        ThemeButtonText = IsDarkMode ? "Switch to Light Mode" : "Switch to Dark Mode";
        Application.Current.CurrentThemeChanged += OnCurrentThemeChanged;

        NavigationItems =
        [
            new NavigationItemViewModel
            {
                Name = "SplitView",
                Tag = "SplitView",
                IconData = "M2 3a1 1 0 0 1 1-1h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V3zm5 0v14"
            },
            new NavigationItemViewModel
            {
                Name = "StackPanel",
                Tag = "StackPanel",
                IconData = "M3 5a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5zm0 10a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-3z"
            },
            new NavigationItemViewModel
            {
                Name = "Grid",
                Tag = "Grid",
                IconData = "M3 3h7v7H3V3zm11 0h7v7h-7V3zM3 14h7v7H3v-7zm11 0h7v7h-7v-7z"
            },
            new NavigationItemViewModel
            {
                Name = "Images & SVG",
                Tag = "Images",
                IconData = "M3 4a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4zm3 13l4-5 3 4 3-3 5 6H6z"
            },
            new NavigationItemViewModel
            {
                Name = "Text",
                Tag = "Text",
                IconData = "M4 4h16v3H13v13h-3V7H4V4z"
            },
            new NavigationItemViewModel
            {
                Name = "Toggles",
                Tag = "Toggles",
                IconData = "M7 7a5 5 0 0 0 0 10h10a5 5 0 0 0 0-10H7zm0 2h10a3 3 0 0 1 0 6H7a3 3 0 0 1 0-6zm10 1a2 2 0 1 0 0 4 2 2 0 0 0 0-4z"
            },
            new NavigationItemViewModel
            {
                Name = "Forms",
                Tag = "Forms",
                IconData = "M4 3h16a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1zm3 5h10V6H7v2zm0 5h10v-2H7v2zm0 5h6v-2H7v2z"
            }
        ];

        SelectedPage = NavigationItems[0];
    }

    /// <summary>
    /// Gets the collection of navigation items in the gallery shell.
    /// </summary>
    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    /// <summary>
    /// Gets or sets the currently selected navigation page item.
    /// </summary>
    public NavigationItemViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                CurrentPage = CreatePage(value);
            }
        }
    }

    /// <summary>
    /// Gets the active page control created from the selected item.
    /// </summary>
    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        Application.Current.RequestedTheme = IsDarkMode ? Theme.Light : Theme.Dark;
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        ThemeButtonText = value ? "Switch to Light Mode" : "Switch to Dark Mode";
    }

    private void OnCurrentThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        IsDarkMode = e.NewTheme == Theme.Dark;
    }

    private static object? CreatePage(NavigationItemViewModel? item)
    {
        if (item is null)
        {
            return null;
        }

        return item.Tag switch
        {
            "SplitView" => new SplitViewSampleView(),
            "StackPanel" => new DisplaySampleView(),
            "Grid" => new CollectionSampleView(),
            "Images" => new ImageSampleView(),
            "Text" => new TextSampleView(),
            "Toggles" => new ToggleSampleView(),
            "Forms" => new FormSampleView(),
            _ => new SplitViewSampleView()
        };
    }
}
