using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents an application shell providing top-level or hierarchical navigation.
/// The template exposes <c>splitview</c>, <c>toggle-button</c>, <c>pane-header</c>, <c>menu-items</c>,
/// <c>footer-items</c>, <c>top-items</c>, and <c>content</c> parts.
/// </summary>
public class NavigationView : ContentControl
{
    /// <summary>
    /// Identifies the <see cref="MenuItems"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IList<NavigationViewItemBase>> MenuItemsProperty =
        BindableProperty.Create<NavigationView, IList<NavigationViewItemBase>>(
            nameof(MenuItems),
            defaultValue: null!);

    /// <summary>
    /// Identifies the <see cref="FooterMenuItems"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IList<NavigationViewItemBase>> FooterMenuItemsProperty =
        BindableProperty.Create<NavigationView, IList<NavigationViewItemBase>>(
            nameof(FooterMenuItems),
            defaultValue: null!);

    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Create<NavigationView, IEnumerable?>(
            nameof(ItemsSource),
            defaultValue: null,
            propertyChanged: static (owner, oldVal, newVal) => owner.OnItemsSourceChanged(oldVal, newVal));

    /// <summary>
    /// Identifies the <see cref="MenuItemsSource"/> bindable property (WinUI/UWP naming alias for <see cref="ItemsSource"/>).
    /// </summary>
    public static readonly BindableProperty<IEnumerable?> MenuItemsSourceProperty = ItemsSourceProperty;

    /// <summary>
    /// Identifies the <see cref="ItemTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> ItemTemplateProperty =
        BindableProperty.Create<NavigationView, DataTemplate?>(
            nameof(ItemTemplate),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.RebuildRealizedItems());

    /// <summary>
    /// Identifies the <see cref="SelectedItem"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Create<NavigationView, object?>(
            nameof(SelectedItem),
            defaultValue: null,
            propertyChanged: static (owner, oldVal, newVal) => owner.OnSelectedItemChanged(oldVal, newVal));

    /// <summary>
    /// Identifies the <see cref="IsPaneOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsPaneOpenProperty =
        BindableProperty.Create<NavigationView, bool>(
            nameof(IsPaneOpen),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.OnIsPaneOpenChanged());

    /// <summary>
    /// Identifies the <see cref="IsPaneToggleButtonVisible"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsPaneToggleButtonVisibleProperty =
        BindableProperty.Create<NavigationView, bool>(
            nameof(IsPaneToggleButtonVisible),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.UpdateToggleButtonVisibility());

    /// <summary>
    /// Identifies the <see cref="IsSettingsVisible"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsSettingsVisibleProperty =
        BindableProperty.Create<NavigationView, bool>(
            nameof(IsSettingsVisible),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateSettingsVisibility());

    /// <summary>
    /// Identifies the <see cref="PaneDisplayMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<NavigationViewPaneDisplayMode> PaneDisplayModeProperty =
        BindableProperty.Create<NavigationView, NavigationViewPaneDisplayMode>(
            nameof(PaneDisplayMode),
            defaultValue: NavigationViewPaneDisplayMode.CompactInline,
            propertyChanged: static (owner, _, _) => owner.UpdateDisplayMode());

    /// <summary>
    /// Identifies the <see cref="PanePlacement"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Dock> PanePlacementProperty =
        BindableProperty.Create<NavigationView, Dock>(
            nameof(PanePlacement),
            defaultValue: Dock.Left,
            propertyChanged: static (owner, _, _) => owner.UpdatePanePlacement());

    /// <summary>
    /// Identifies the <see cref="OpenPaneLength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> OpenPaneLengthProperty =
        BindableProperty.Create<NavigationView, double>(
            nameof(OpenPaneLength),
            defaultValue: 240,
            propertyChanged: static (owner, _, _) => owner.UpdatePaneLengths());

    /// <summary>
    /// Identifies the <see cref="CompactPaneLength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> CompactPaneLengthProperty =
        BindableProperty.Create<NavigationView, double>(
            nameof(CompactPaneLength),
            defaultValue: 48,
            propertyChanged: static (owner, _, _) => owner.UpdatePaneLengths());

    /// <summary>
    /// Identifies the <see cref="PaneHeader"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> PaneHeaderProperty =
        BindableProperty.Create<NavigationView, object?>(
            nameof(PaneHeader),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePaneHeader());

    /// <summary>
    /// Identifies the <see cref="PaneFooter"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> PaneFooterProperty =
        BindableProperty.Create<NavigationView, object?>(
            nameof(PaneFooter),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePaneFooter());

    /// <summary>
    /// Identifies the <see cref="Header"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Create<NavigationView, object?>(
            nameof(Header),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateHeader());

    private readonly ObservableCollection<NavigationViewItemBase> _menuItems = [];
    private readonly ObservableCollection<NavigationViewItemBase> _footerMenuItems = [];
    private readonly List<NavigationViewItemBase> _realizedSourceItems = [];

    private SplitView? _splitView;
    private Button? _toggleButton;
    private View? _paneHeaderHost;
    private StackPanel? _menuItemsHost;
    private StackPanel? _footerItemsHost;
    private View? _paneFooterHost;
    private View? _headerHost;
    private StackPanel? _topItemsHost;
    private View? _contentHost;
    private NavigationViewItem? _settingsItem;

    private UIElement? _presentedPaneHeader;
    private UIElement? _presentedPaneFooter;
    private UIElement? _presentedHeader;
    private bool _isSyncingSelection;
    private bool _isSyncingPaneOpen;

    static NavigationView()
    {
        WidthProperty.OverrideMetadata<NavigationView>(WidthValue.Percent(100));
        HeightProperty.OverrideMetadata<NavigationView>(HeightValue.Percent(100));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationView"/> class.
    /// </summary>
    public NavigationView()
    {
        SetValue(MenuItemsProperty, _menuItems);
        SetValue(FooterMenuItemsProperty, _footerMenuItems);

        _menuItems.CollectionChanged += OnMenuItemsChanged;
        _footerMenuItems.CollectionChanged += OnFooterMenuItemsChanged;
    }

    /// <inheritdoc />
    public override string TagName => "navigationview";

    /// <summary>
    /// Occurs when the <see cref="SelectedItem"/> property changes.
    /// </summary>
    public event EventHandler<NavigationViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Occurs when an item is clicked, tapped, or invoked through keyboard navigation.
    /// </summary>
    public event EventHandler<NavigationViewItemInvokedEventArgs>? ItemInvoked;

    /// <summary>
    /// Occurs when the navigation pane opens.
    /// </summary>
    public event EventHandler? PaneOpened;

    /// <summary>
    /// Occurs when the navigation pane closes.
    /// </summary>
    public event EventHandler? PaneClosed;

    /// <summary>
    /// Gets the collection of primary navigation items.
    /// </summary>
    public IList<NavigationViewItemBase> MenuItems => _menuItems;

    /// <summary>
    /// Gets the collection of footer navigation items.
    /// </summary>
    public IList<NavigationViewItemBase> FooterMenuItems => _footerMenuItems;

    /// <summary>
    /// Gets or sets an object source used to generate navigation items.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the collection used to generate navigation items (WinUI/UWP naming alias for <see cref="ItemsSource"/>).
    /// </summary>
    public IEnumerable? MenuItemsSource
    {
        get => ItemsSource;
        set => ItemsSource = value;
    }

    /// <summary>
    /// Gets or sets the template used to display each item from <see cref="ItemsSource"/>.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected item.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the navigation pane is open.
    /// </summary>
    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the hamburger toggle button is displayed.
    /// </summary>
    public bool IsPaneToggleButtonVisible
    {
        get => GetValue(IsPaneToggleButtonVisibleProperty);
        set => SetValue(IsPaneToggleButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the built-in Settings item is displayed in the footer.
    /// </summary>
    public bool IsSettingsVisible
    {
        get => GetValue(IsSettingsVisibleProperty);
        set => SetValue(IsSettingsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets how the navigation pane is presented.
    /// </summary>
    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    /// <summary>
    /// Gets or sets which edge hosts the navigation pane (<see cref="Dock.Left"/> or <see cref="Dock.Right"/>).
    /// </summary>
    public Dock PanePlacement
    {
        get => GetValue(PanePlacementProperty);
        set => SetValue(PanePlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the pane in logical pixels when expanded.
    /// </summary>
    public double OpenPaneLength
    {
        get => GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the pane in logical pixels when collapsed into compact icon mode.
    /// </summary>
    public double CompactPaneLength
    {
        get => GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets custom content displayed in the pane header.
    /// </summary>
    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets custom content displayed in the pane footer.
    /// </summary>
    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    /// <summary>
    /// Gets or sets the title or header content displayed above the main content area.
    /// </summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        return name switch
        {
            "data-open" => IsPaneOpen ? string.Empty : null,
            "data-closed" => !IsPaneOpen ? string.Empty : null,
            "data-pane-placement" => PanePlacement == Dock.Right ? "right" : "left",
            "data-display-mode" => PaneDisplayMode switch
            {
                NavigationViewPaneDisplayMode.Inline => "inline",
                NavigationViewPaneDisplayMode.CompactInline => "compact-inline",
                NavigationViewPaneDisplayMode.Overlay => "overlay",
                NavigationViewPaneDisplayMode.CompactOverlay => "compact-overlay",
                NavigationViewPaneDisplayMode.Top => "top",
                _ => "compact-inline"
            },
            _ => base.GetAttribute(name)
        };
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return _contentHost ?? this;
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _splitView = DefinePart("splitview", new SplitView
        {
            OpenPaneLength = OpenPaneLength,
            CompactPaneLength = CompactPaneLength,
            IsPaneOpen = IsPaneOpen,
            PanePlacement = PanePlacement,
            DisplayMode = ToSplitViewDisplayMode(PaneDisplayMode)
        });

        _splitView.PaneOpened += (_, _) =>
        {
            if (!_isSyncingPaneOpen)
            {
                _isSyncingPaneOpen = true;
                IsPaneOpen = true;
                PaneOpened?.Invoke(this, EventArgs.Empty);
                _isSyncingPaneOpen = false;
            }
        };

        _splitView.PaneClosed += (_, _) =>
        {
            if (!_isSyncingPaneOpen)
            {
                _isSyncingPaneOpen = true;
                IsPaneOpen = false;
                PaneClosed?.Invoke(this, EventArgs.Empty);
                _isSyncingPaneOpen = false;
            }
        };

        View paneRoot = new View { Class = ["navigation-pane-root"], Height = HeightValue.Percent(100) };
        Grid paneLayout = new Grid
        {
            Rows = [GridLength.Auto, GridLength.Star(), GridLength.Auto],
            Class = ["navigation-pane-grid"],
            Height = HeightValue.Percent(100)
        };

        StackPanel paneHeaderSection = new StackPanel { Spacing = 4 };
        _toggleButton = DefinePart("toggle-button", new Button
        {
            Text = "☰",
            Class = ["navigation-toggle-btn"]
        });
        _toggleButton.Click += (_, _) => IsPaneOpen = !IsPaneOpen;

        _paneHeaderHost = DefinePart("pane-header", new View
        {
            Class = ["navigation-pane-header-host"]
        });

        paneHeaderSection.Children.Add(_toggleButton);
        paneHeaderSection.Children.Add(_paneHeaderHost);
        Grid.SetRow(paneHeaderSection, 0);
        paneLayout.Children.Add(paneHeaderSection);

        ScrollView menuScroll = new ScrollView { Class = ["navigation-menu-scroll"] };
        _menuItemsHost = DefinePart("menu-items", new StackPanel
        {
            Spacing = 2,
            Class = ["navigation-menu-items"]
        });
        menuScroll.Content = _menuItemsHost;
        Grid.SetRow(menuScroll, 1);
        paneLayout.Children.Add(menuScroll);

        StackPanel paneFooterSection = new StackPanel { Spacing = 2 };
        _footerItemsHost = DefinePart("footer-items", new StackPanel
        {
            Spacing = 2,
            Class = ["navigation-footer-items"]
        });

        _settingsItem = new NavigationViewItem
        {
            Content = "Settings",
            Tag = "Settings",
            Icon = "⚙",
            Class = ["navigation-settings-item"],
            IsVisible = IsSettingsVisible
        };
        RegisterItem(_settingsItem);
        if (IsSettingsVisible)
        {
            _footerItemsHost.Children.Add(_settingsItem);
        }

        _paneFooterHost = DefinePart("pane-footer", new View
        {
            Class = ["navigation-pane-footer-host"]
        });

        paneFooterSection.Children.Add(_footerItemsHost);
        paneFooterSection.Children.Add(_paneFooterHost);
        Grid.SetRow(paneFooterSection, 2);
        paneLayout.Children.Add(paneFooterSection);

        paneRoot.Children.Add(paneLayout);
        _splitView.Pane = paneRoot;

        View contentArea = new View { Class = ["navigation-content-area"], Height = HeightValue.Percent(100) };
        Grid contentLayout = DefinePart("layout", new Grid
        {
            Rows = [GridLength.Auto, GridLength.Auto, GridLength.Star()]
        });

        _topItemsHost = DefinePart("top-items", new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Class = ["navigation-top-items"],
            IsVisible = PaneDisplayMode == NavigationViewPaneDisplayMode.Top
        });
        Grid.SetRow(_topItemsHost, 0);
        contentLayout.Children.Add(_topItemsHost);

        _headerHost = DefinePart("header", new View
        {
            Class = ["navigation-header-host"]
        });
        Grid.SetRow(_headerHost, 1);
        contentLayout.Children.Add(_headerHost);

        _contentHost = DefinePart(ContentPartName, new View
        {
            Class = ["navigation-content-host"],
            Height = HeightValue.Percent(100)
        });
        Grid.SetRow(_contentHost, 2);
        contentLayout.Children.Add(_contentHost);

        contentArea.Children.Add(contentLayout);
        _splitView.Content = contentArea;

        Children.Add(_splitView);

        UpdateToggleButtonVisibility();
        UpdateSettingsVisibility();
        UpdatePaneLengths();
        UpdateDisplayMode();
        UpdatePaneHeader();
        UpdatePaneFooter();
        UpdateHeader();
        SyncHostChildren();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menuItems.CollectionChanged -= OnMenuItemsChanged;
            _footerMenuItems.CollectionChanged -= OnFooterMenuItemsChanged;
            if (ItemsSource is INotifyCollectionChanged notifySource)
            {
                notifySource.CollectionChanged -= OnItemsSourceCollectionChanged;
            }

            foreach (NavigationViewItemBase item in _menuItems)
            {
                UnregisterItem(item);
            }

            foreach (NavigationViewItemBase item in _footerMenuItems)
            {
                UnregisterItem(item);
            }

            foreach (NavigationViewItemBase item in _realizedSourceItems)
            {
                UnregisterItem(item);
            }

            if (_settingsItem is not null)
            {
                UnregisterItem(_settingsItem);
            }
        }

        base.Dispose(disposing);
    }

    private void RegisterItem(NavigationViewItemBase itemBase)
    {
        if (itemBase is NavigationViewItem item)
        {
            item.Invoked += OnNavItemInvoked;
            item.PropertyChanged += OnNavItemPropertyChanged;
            if (item.MenuItems is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += OnNavItemChildrenChanged;
            }

            foreach (NavigationViewItemBase child in item.MenuItems)
            {
                RegisterItem(child);
            }
        }
    }

    private void UnregisterItem(NavigationViewItemBase itemBase)
    {
        if (itemBase is NavigationViewItem item)
        {
            item.Invoked -= OnNavItemInvoked;
            item.PropertyChanged -= OnNavItemPropertyChanged;
            if (item.MenuItems is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged -= OnNavItemChildrenChanged;
            }

            foreach (NavigationViewItemBase child in item.MenuItems)
            {
                UnregisterItem(child);
            }
        }
    }

    private void OnNavItemInvoked(object? sender, EventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            bool isSettings = ReferenceEquals(item, _settingsItem);
            object? payload = item.DataContext ?? item.Tag ?? item.Content ?? item;
            ItemInvoked?.Invoke(this, new NavigationViewItemInvokedEventArgs(payload, item, isSettings));
            NotifyItemSelected(item);
        }
    }

    private void OnNavItemPropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
    {
        if (e.Property == NavigationViewItem.IsSelectedProperty &&
            sender is NavigationViewItem navItem &&
            navItem.IsSelected &&
            !_isSyncingSelection)
        {
            NotifyItemSelected(navItem);
        }
    }

    private void OnNavItemChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (NavigationViewItemBase oldItem in e.OldItems)
            {
                UnregisterItem(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (NavigationViewItemBase newItem in e.NewItems)
            {
                RegisterItem(newItem);
            }
        }
    }

    private void NotifyItemSelected(NavigationViewItem item)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            UnselectAllExcept(item);
            item.IsSelected = true;

            bool isSettings = ReferenceEquals(item, _settingsItem);
            object? newSelection = item.DataContext ?? item.Tag ?? item.Content ?? item;

            if (!Equals(SelectedItem, newSelection))
            {
                SelectedItem = newSelection;
                SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(newSelection, item, isSettings));
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnSelectedItemChanged(object? oldValue, object? newValue)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            NavigationViewItem? matchingItem = FindItemByPayload(newValue);
            UnselectAllExcept(matchingItem);
            if (matchingItem is not null)
            {
                matchingItem.IsSelected = true;
            }

            bool isSettings = ReferenceEquals(matchingItem, _settingsItem);
            SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(newValue, matchingItem, isSettings));
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void UnselectAllExcept(NavigationViewItem? keepSelected)
    {
        foreach (NavigationViewItemBase item in GetAllRealizedItems())
        {
            if (item is NavigationViewItem navItem && !ReferenceEquals(navItem, keepSelected))
            {
                navItem.IsSelected = false;
            }
        }

        if (_settingsItem is not null && !ReferenceEquals(_settingsItem, keepSelected))
        {
            _settingsItem.IsSelected = false;
        }
    }

    private NavigationViewItem? FindItemByPayload(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        if (payload is NavigationViewItem directItem)
        {
            return directItem;
        }

        foreach (NavigationViewItemBase baseItem in GetAllRealizedItems())
        {
            if (baseItem is NavigationViewItem item)
            {
                if (Equals(item.Tag, payload) || Equals(item.Content, payload) || Equals(item.DataContext, payload))
                {
                    return item;
                }
            }
        }

        if (_settingsItem is not null && (Equals(_settingsItem.Tag, payload) || Equals(_settingsItem.Content, payload)))
        {
            return _settingsItem;
        }

        return null;
    }

    private IEnumerable<NavigationViewItemBase> GetAllRealizedItems()
    {
        IEnumerable<NavigationViewItemBase> primaryItems = ItemsSource is not null
            ? _realizedSourceItems
            : _menuItems;

        foreach (NavigationViewItemBase item in primaryItems)
        {
            yield return item;
            if (item is NavigationViewItem parentItem)
            {
                foreach (NavigationViewItemBase child in parentItem.MenuItems)
                {
                    yield return child;
                }
            }
        }

        foreach (NavigationViewItemBase item in _footerMenuItems)
        {
            yield return item;
            if (item is NavigationViewItem parentItem)
            {
                foreach (NavigationViewItemBase child in parentItem.MenuItems)
                {
                    yield return child;
                }
            }
        }
    }

    private void OnItemsSourceChanged(object? oldSource, object? newSource)
    {
        if (oldSource is INotifyCollectionChanged oldNotify)
        {
            oldNotify.CollectionChanged -= OnItemsSourceCollectionChanged;
        }

        if (newSource is INotifyCollectionChanged newNotify)
        {
            newNotify.CollectionChanged += OnItemsSourceCollectionChanged;
        }

        RebuildRealizedItems();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRealizedItems();
    }

    private void RebuildRealizedItems()
    {
        foreach (NavigationViewItemBase item in _realizedSourceItems)
        {
            UnregisterItem(item);
        }

        _realizedSourceItems.Clear();
        if (ItemsSource is not null)
        {
            foreach (object? dataItem in ItemsSource)
            {
                if (dataItem is NavigationViewItemBase existingItem)
                {
                    _realizedSourceItems.Add(existingItem);
                    RegisterItem(existingItem);
                }
                else if (dataItem is not null)
                {
                    NavigationViewItem created = new NavigationViewItem
                    {
                        DataContext = dataItem,
                        Tag = dataItem
                    };

                    if (ItemTemplate is not null)
                    {
                        created.Content = ItemTemplate.Build(dataItem);
                    }
                    else
                    {
                        System.Reflection.PropertyInfo? nameProp = dataItem.GetType().GetProperty("Name");
                        created.Content = nameProp?.GetValue(dataItem) ?? dataItem;

                        System.Reflection.PropertyInfo? iconProp = dataItem.GetType().GetProperty("Icon") ?? dataItem.GetType().GetProperty("IconData");
                        if (iconProp?.GetValue(dataItem) is object iconVal)
                        {
                            if (iconVal is string iconPath && !string.IsNullOrEmpty(iconPath))
                            {
                                created.Icon = new NativeUI.Controls.Shapes.Path
                                {
                                    Data = Geometry.Parse(iconPath),
                                    Fill = Brushes.Transparent,
                                    Stroke = SvgCurrentColorBrush.Instance,
                                    StrokeThickness = 1.5,
                                    Width = 16,
                                    Height = 16,
                                    Stretch = Stretch.Uniform
                                };
                            }
                            else
                            {
                                created.Icon = iconVal;
                            }
                        }
                    }

                    _realizedSourceItems.Add(created);
                    RegisterItem(created);
                }
            }
        }

        SyncHostChildren();
    }

    private void OnMenuItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (NavigationViewItemBase oldItem in e.OldItems)
            {
                UnregisterItem(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (NavigationViewItemBase newItem in e.NewItems)
            {
                RegisterItem(newItem);
            }
        }

        SyncHostChildren();
    }

    private void OnFooterMenuItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (NavigationViewItemBase oldItem in e.OldItems)
            {
                UnregisterItem(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (NavigationViewItemBase newItem in e.NewItems)
            {
                RegisterItem(newItem);
            }
        }

        SyncHostChildren();
    }

    private void SyncHostChildren()
    {
        if (_menuItemsHost is null || _footerItemsHost is null || _topItemsHost is null)
        {
            return;
        }

        bool isTop = PaneDisplayMode == NavigationViewPaneDisplayMode.Top;

        StackPanel targetHost = isTop ? _topItemsHost : _menuItemsHost;
        StackPanel unusedHost = isTop ? _menuItemsHost : _topItemsHost;

        unusedHost.Children.Clear();
        targetHost.Children.Clear();

        if (ItemsSource is not null)
        {
            foreach (NavigationViewItemBase item in _realizedSourceItems)
            {
                targetHost.Children.Add(item);
            }
        }
        else
        {
            foreach (NavigationViewItemBase item in _menuItems)
            {
                targetHost.Children.Add(item);
            }
        }

        _footerItemsHost.Children.Clear();
        foreach (NavigationViewItemBase item in _footerMenuItems)
        {
            _footerItemsHost.Children.Add(item);
        }

        if (_settingsItem is not null && IsSettingsVisible)
        {
            _footerItemsHost.Children.Add(_settingsItem);
        }
    }

    private void OnIsPaneOpenChanged()
    {
        if (_splitView is not null && !_isSyncingPaneOpen)
        {
            _isSyncingPaneOpen = true;
            _splitView.IsPaneOpen = IsPaneOpen;
            _isSyncingPaneOpen = false;
        }

        InvalidateMeasure();
    }

    private void UpdateToggleButtonVisibility()
    {
        if (_toggleButton is not null)
        {
            _toggleButton.IsVisible = IsPaneToggleButtonVisible && PaneDisplayMode != NavigationViewPaneDisplayMode.Top;
        }
    }

    private void UpdateSettingsVisibility()
    {
        if (_settingsItem is not null)
        {
            _settingsItem.IsVisible = IsSettingsVisible;
        }

        SyncHostChildren();
    }

    private void UpdateDisplayMode()
    {
        bool isTop = PaneDisplayMode == NavigationViewPaneDisplayMode.Top;
        if (_topItemsHost is not null)
        {
            _topItemsHost.IsVisible = isTop;
        }

        if (_headerHost is not null)
        {
            _headerHost.IsVisible = Header is not null;
        }

        if (_splitView is not null)
        {
            _splitView.DisplayMode = ToSplitViewDisplayMode(PaneDisplayMode);
            _splitView.IsPaneOpen = !isTop && IsPaneOpen;
        }

        UpdateToggleButtonVisibility();
        SyncHostChildren();
        InvalidateMeasure();
    }

    private static SplitViewDisplayMode ToSplitViewDisplayMode(NavigationViewPaneDisplayMode mode)
    {
        return mode switch
        {
            NavigationViewPaneDisplayMode.Inline => SplitViewDisplayMode.Inline,
            NavigationViewPaneDisplayMode.CompactInline => SplitViewDisplayMode.CompactInline,
            NavigationViewPaneDisplayMode.Overlay => SplitViewDisplayMode.Overlay,
            NavigationViewPaneDisplayMode.CompactOverlay => SplitViewDisplayMode.CompactOverlay,
            _ => SplitViewDisplayMode.CompactInline
        };
    }

    private void UpdatePanePlacement()
    {
        if (_splitView is not null)
        {
            _splitView.PanePlacement = PanePlacement;
        }
    }

    private void UpdatePaneLengths()
    {
        if (_splitView is not null)
        {
            _splitView.OpenPaneLength = OpenPaneLength;
            _splitView.CompactPaneLength = CompactPaneLength;
        }
    }

    private void UpdatePaneHeader()
    {
        UpdateSlotContent(ref _presentedPaneHeader, _paneHeaderHost, PaneHeader);
        if (_paneHeaderHost is not null)
        {
            _paneHeaderHost.IsVisible = PaneHeader is not null;
        }
    }

    private void UpdatePaneFooter()
    {
        UpdateSlotContent(ref _presentedPaneFooter, _paneFooterHost, PaneFooter);
        if (_paneFooterHost is not null)
        {
            _paneFooterHost.IsVisible = PaneFooter is not null;
        }
    }

    private void UpdateHeader()
    {
        UpdateSlotContent(ref _presentedHeader, _headerHost, Header);
        if (_headerHost is not null)
        {
            _headerHost.IsVisible = Header is not null;
        }
    }

    private static void UpdateSlotContent(ref UIElement? slotStorage, View? host, object? content)
    {
        if (host is null)
        {
            return;
        }

        if (slotStorage is not null)
        {
            host.Children.Remove(slotStorage);
            slotStorage.Dispose();
            slotStorage = null;
        }

        if (content is null)
        {
            return;
        }

        slotStorage = content switch
        {
            UIElement element => element,
            _ => new Text
            {
                Value = Convert.ToString(content, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        host.Children.Add(slotStorage);
    }
}
