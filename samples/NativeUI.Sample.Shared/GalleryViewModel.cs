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
                Name = "Select",
                Tag = "Select",
                IconData = "M12 3l5 5H7l5-5zm0 18l-5-5h10l-5 5z"
            },
            new NavigationItemViewModel
            {
                Name = "Scrolling",
                Tag = "Scrolling",
                IconData = "M8 4H5v16h3M16 4h3v16h-3M9 7l3-3 3 3M12 4v16M9 17l3 3 3-3"
            },
            new NavigationItemViewModel
            {
                Name = "Canvas",
                Tag = "Canvas",
                IconData = "M4 3h16v16H4V3zM8 23l4-4 4 4M12 3V1M8 13l3-4 3 3 3-5"
            },
            new NavigationItemViewModel
            {
                Name = "Input",
                Tag = "Input",
                IconData = "M3 5h18v14H3V5zM8 9v6M5 9h6M5 15h6M14 12h4"
            },
            new NavigationItemViewModel
            {
                Name = "Display",
                Tag = "Display",
                IconData = "M3 4h18v13H3V4zM12 17v4M8 21h8M7 8h10M7 12h6"
            },
            new NavigationItemViewModel
            {
                Name = "Popups",
                Tag = "Popups",
                IconData = "M3 4h18v13h-7l-4 4v-4H3V4zM7 8h10M7 12h6"
            },
            new NavigationItemViewModel
            {
                Name = "Tabs",
                Tag = "Tabs",
                IconData = "M3 7h18v14H3V7zM3 7V3h7v4M10 3h7v4M17 3h4v4"
            },
            new NavigationItemViewModel
            {
                Name = "List & Table",
                Tag = "Collections",
                IconData = "M3 3h18v18H3V3zM3 9h18M3 15h18M9 3v18"
            },
            new NavigationItemViewModel
            {
                Name = "Dialog",
                Tag = "Dialog",
                IconData = "M3 4h18v16H3V4zM3 8h18M7 6h1M10 6h1M8 12h8M10 16h6"
            },
            new NavigationItemViewModel
            {
                Name = "Animation",
                Tag = "Animation",
                IconData = "M13 2L4 14h7l-1 8 10-13h-7l1-7z"
            },
            new NavigationItemViewModel
            {
                Name = "3D",
                Tag = "Scene3D",
                IconData = "M12 2l9 5v10l-9 5-9-5V7l9-5zM3 7l9 5 9-5M12 12v10"
            },
            new NavigationItemViewModel
            {
                Name = "ResizeObserver",
                Tag = "ResizeObserver",
                IconData = "M3 9V3h6M15 3h6v6M21 15v6h-6M9 21H3v-6M8 8h8v8H8V8z"
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
            "StackPanel" => new StackPanelSampleView(),
            "Grid" => new GridSampleView(),
            "Images" => new ImageSampleView(),
            "Text" => new TextSampleView(),
            "Toggles" => new ToggleSampleView(),
            "Select" => new SelectSampleView(),
            "Forms" => new FormSampleView(),
            "Scrolling" => new ScrollSampleView(),
            "Canvas" => new CanvasSampleView(),
            "Input" => new InputSampleView(),
            "Display" => new DisplaySampleView(),
            "Popups" => new PopupSampleView(),
            "Tabs" => new TabSampleView(),
            "Collections" => new CollectionSampleView(),
            "Dialog" => new DialogSampleView(),
            "Animation" => new AnimationSampleView(),
            "Scene3D" => new Scene3DSampleView(),
            "ResizeObserver" => new ResizeSampleView(),
            _ => null
        };
    }
}
