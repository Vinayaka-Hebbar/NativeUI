using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using LayoutPosition = NativeUI.Layout.Position;

namespace NativeUI.Controls;

/// <summary>
/// Selects one retained page using customizable headers, data templates, and a separate selection indicator.
/// The template exposes strip, items, indicator, and content parts.
/// </summary>
public class TabControl : Selector
{
    /// <summary>Identifies the default template for data-backed pages.</summary>
    public static readonly BindableProperty<DataTemplate?> ContentTemplateProperty =
        BindableProperty.Create<TabControl, DataTemplate?>(
            nameof(ContentTemplate),
            propertyChanged: static (owner, _, _) => owner.UpdateContentTemplates());

    /// <summary>Identifies the edge occupied by the tab headers.</summary>
    public static readonly BindableProperty<Dock> TabStripPlacementProperty =
        BindableProperty.Create<TabControl, Dock>(
            nameof(TabStripPlacement),
            defaultValue: Dock.Top,
            propertyChanged: static (owner, _, _) => owner.UpdatePlacement());

    /// <summary>Identifies the selected tab's page content.</summary>
    public static readonly BindableProperty<object?> SelectedContentProperty =
        BindableProperty.Create<TabControl, object?>(nameof(SelectedContent));

    static TabControl()
    {
        FocusableProperty.OverrideMetadata<TabControl>(false);
        OverflowYProperty.OverrideMetadata<TabControl>(Overflow.Visible);
    }

    /// <summary>Initializes a tab control with automatic keyboard activation.</summary>
    public TabControl()
    {
        SelectionChanged += OnSelectionChanged;
        KeyDown += OnTabKeyDown;
        PointerPressed += OnTabPointerPressed;
        if (Strip is View strip)
        {
            strip.SizeChanged += OnHeaderSizeChanged;
            strip.PropertyChanged += OnStripPropertyChanged;
        }
    }

    /// <inheritdoc />
    public override string TagName => "tabcontrol";

    /// <summary>Gets or sets the default page template for data items.</summary>
    public DataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Gets or sets the edge at which the header strip appears.</summary>
    public Dock TabStripPlacement
    {
        get => GetValue(TabStripPlacementProperty);
        set => SetValue(TabStripPlacementProperty, value);
    }

    /// <summary>Gets the selected tab's content, or null when no tab is selected.</summary>
    public object? SelectedContent => GetValue(SelectedContentProperty);

    private Grid? Root => GetTemplateChild<Grid>("layout");

    internal View? Strip => GetTemplateChild<View>("strip");

    private View? ContentHost => GetTemplateChild<View>("content");

    internal View? Indicator => GetTemplateChild<View>("indicator");

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        Grid layout = DefinePart(new Grid
        {
            Rows = [GridLength.Auto, GridLength.Star()]
        }, "layout");
        View strip = DefinePart(new View { Position = LayoutPosition.Relative }, "strip");
        StackPanel headers = DefinePart(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Style = new Style { Position = LayoutPosition.Relative, ZIndex = 1 }
        }, ItemsPartName);
        KeyboardNavigation.SetTabNavigation(headers, KeyboardNavigationMode.Once);
        strip.Children.Add(DefinePart(new View { Position = LayoutPosition.Absolute, IsVisible = false }, "indicator"));
        strip.Children.Add(headers);
        View content = DefinePart(new View(), "content");
        Grid.SetRow(content, 1);
        layout.Children.Add(strip);
        layout.Children.Add(content);
        Children.Add(layout);
    }

    /// <inheritdoc />
    protected override UIElement CreateItemElement(object? item)
    {
        if (item is TabItem tab)
        {
            return tab;
        }

        return new TabItem
        {
            DataContext = item,
            Header = item is UIElement ? item.GetType().Name : item,
            HeaderTemplate = ItemTemplate,
            Content = item
        };
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(UIElement element, object? item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is not TabItem tab)
        {
            return;
        }

        DataTemplate? previousTemplate = tab.ContentTemplate ?? tab.Owner?.ContentTemplate;
        tab.Owner = this;
        if (!ReferenceEquals(previousTemplate, tab.ContentTemplate ?? ContentTemplate))
        {
            tab.UpdateContentTemplate();
        }

        tab.Page.DataContext = tab.DataContext;
        tab.Page.IsVisible = false;
        ContentHost?.Children.Add(tab.Page);
        tab.PropertyChanged += OnTabPropertyChanged;
        tab.SizeChanged += OnHeaderSizeChanged;
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(UIElement element, object? item)
    {
        if (element is TabItem tab)
        {
            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.SizeChanged -= OnHeaderSizeChanged;
            ContentHost?.Children.Remove(tab.Page);
            tab.Owner = null;
            tab.IsSelected = false;
            tab.IsTabStop = false;
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        base.OnItemsChanged(args);
        if (SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = FindAvailableTab(Math.Max(0, args.OldStartingIndex), 1);
        }

        UpdateSelectedContent();
    }

    /// <inheritdoc />
    protected override void ApplySelectedState(UIElement element, bool isSelected)
    {
        if (element is TabItem tab)
        {
            tab.IsSelected = isSelected;
        }
    }

    /// <inheritdoc />
    public override int IndexOfItemElement(UIElement element)
    {
        int index = base.IndexOfItemElement(element);
        if (index < 0 || GetItemElement(index) is not TabItem tab)
        {
            return -1;
        }

        for (UIElement? current = element; current is not null && !ReferenceEquals(current, tab); current = current.Parent)
        {
            if (current.Focusable)
            {
                return -1;
            }
        }

        return tab.IsEffectivelyEnabled && tab.IsVisibleInTree ? index : -1;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Strip is View strip)
            {
                strip.PropertyChanged -= OnStripPropertyChanged;
                strip.SizeChanged -= OnHeaderSizeChanged;
            }
            foreach (TabItem tab in ItemElements.Cast<TabItem>())
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                tab.SizeChanged -= OnHeaderSizeChanged;
                tab.Owner = null;
            }

        }

        base.Dispose(disposing);
    }

    private TabItem? GetSelectedTab()
    {
        return GetItemElement(SelectedIndex) as TabItem;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.Source, this))
        {
            UpdateSelectedContent();
        }
    }

    private void UpdateSelectedContent()
    {
        TabItem? selected = GetSelectedTab();
        foreach (TabItem tab in ItemElements.OfType<TabItem>().ToArray())
        {
            if (tab.IsDisposed)
            {
                continue;
            }

            bool active = ReferenceEquals(tab, selected);
            tab.Page.IsVisible = active;
            tab.Page.IsEnabled = tab.IsEffectivelyEnabled;
            tab.IsTabStop = active;
        }

        SetValue(SelectedContentProperty, GetSelectedTab()?.Content);
        UpdateIndicator();
    }

    private void UpdateContentTemplates()
    {
        foreach (TabItem tab in ItemElements.OfType<TabItem>())
        {
            if (tab.ContentTemplate is null)
            {
                tab.UpdateContentTemplate();
            }
        }
    }

    private void OnTabPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, BoundsProperty))
        {
            UpdateIndicator();
        }
        else if (ReferenceEquals(args.Property, ContentControl.ContentProperty))
        {
            if (ReferenceEquals(sender, GetSelectedTab()))
            {
                SetValue(SelectedContentProperty, GetSelectedTab()?.Content);
            }
        }
        else if (ReferenceEquals(args.Property, IsEffectivelyEnabledProperty) ||
                 ReferenceEquals(args.Property, IsVisibleProperty) ||
                 ReferenceEquals(args.Property, FocusableProperty))
        {
            TabItem? selected = GetSelectedTab();
            if (selected is not null && !CanSelect(selected))
            {
                SelectedIndex = FindAvailableTab(SelectedIndex, 1);
            }

            UpdateSelectedContent();
        }
    }

    private static bool CanSelect(TabItem tab)
    {
        return !tab.IsDisposed && tab.IsEffectivelyEnabled && tab.IsVisible && tab.Focusable;
    }

    private int FindAvailableTab(int start, int step)
    {
        for (int offset = 0; offset < ItemElements.Count; offset++)
        {
            int index = ((start + offset * step) % ItemElements.Count + ItemElements.Count) % ItemElements.Count;
            if (GetItemElement(index) is TabItem tab && CanSelect(tab))
            {
                return index;
            }
        }

        return -1;
    }

    private void OnTabPointerPressed(object? sender, PointerEventArgs args)
    {
        int index = args.Source is UIElement source ? IndexOfItemElement(source) : -1;
        if (index >= 0 && GetItemElement(index) is TabItem tab)
        {
            tab.Focus(FocusState.Pointer);
        }
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs args)
    {
        if (!IsEffectivelyEnabled || args.Source is not TabItem origin || !ItemElements.Contains(origin))
        {
            return;
        }

        bool vertical = TabStripPlacement is Dock.Left or Dock.Right;
        int step = args.Key switch
        {
            Key.ArrowLeft when !vertical => -1,
            Key.ArrowRight when !vertical => 1,
            Key.ArrowUp when vertical => -1,
            Key.ArrowDown when vertical => 1,
            _ => 0
        };
        int current = base.IndexOfItemElement(origin);
        int next = args.Key switch
        {
            Key.Home => FindAvailableTab(0, 1),
            Key.End => FindAvailableTab(Items.Count - 1, -1),
            Key.Enter or Key.Space => current,
            _ when step != 0 => FindAvailableTab(current + step, step),
            _ => -1
        };
        if (next < 0 || GetItemElement(next) is not TabItem destination || !CanSelect(destination))
        {
            return;
        }

        if (destination.Focus(FocusState.Keyboard) && CanSelect(destination) && ItemElements.Contains(destination))
        {
            SelectedIndex = base.IndexOfItemElement(destination);
            args.Handled = true;
        }
    }

    private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        UpdateIndicator();
    }

    private void OnStripPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, BoundsProperty))
        {
            UpdateIndicator();
        }
    }

    private void UpdateIndicator()
    {
        TabItem? selected = GetSelectedTab();
        if (Indicator is not View indicator || Strip is not View strip)
        {
            return;
        }

        indicator.IsVisible = selected is not null && CanSelect(selected);
        if (selected is null)
        {
            return;
        }

        bool vertical = TabStripPlacement is Dock.Left or Dock.Right;
        indicator.Left = vertical ? null : selected.Bounds.X - strip.Bounds.X;
        indicator.Top = vertical ? selected.Bounds.Y - strip.Bounds.Y : null;
        indicator.Width = vertical ? null : WidthValue.Pixels(selected.ActualWidth);
        indicator.Height = vertical ? selected.ActualHeight : null;
    }

    private void UpdatePlacement()
    {
        bool vertical = TabStripPlacement is Dock.Left or Dock.Right;
        bool trailing = TabStripPlacement is Dock.Bottom or Dock.Right;
        if (Root is not Grid root || Strip is not View strip || ContentHost is not View content)
        {
            return;
        }

        root.Rows = vertical ? [] : trailing
            ? [GridLength.Star(), GridLength.Auto]
            : [GridLength.Auto, GridLength.Star()];
        root.Columns = !vertical ? [] : trailing
            ? [GridLength.Star(), GridLength.Auto]
            : [GridLength.Auto, GridLength.Star()];
        Grid.SetRow(strip, !vertical && trailing ? 1 : 0);
        Grid.SetRow(content, !vertical && !trailing ? 1 : 0);
        Grid.SetColumn(strip, vertical && trailing ? 1 : 0);
        Grid.SetColumn(content, vertical && !trailing ? 1 : 0);
        if (ItemsHost is StackPanel headers)
        {
            headers.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        }
        UpdateIndicator();
    }
}
