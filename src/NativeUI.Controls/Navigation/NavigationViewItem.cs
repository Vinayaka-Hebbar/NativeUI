using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Xaml;
using Path = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls;

/// <summary>
/// Represents an interactive selectable navigation item within a <see cref="NavigationView"/>.
/// </summary>
[ContentProperty(nameof(Content))]
public class NavigationViewItem : NavigationViewItemBase
{
    /// <summary>
    /// Identifies the <see cref="Content"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Create<NavigationViewItem, object?>(
            nameof(Content),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedContent());

    /// <summary>
    /// Identifies the <see cref="ContentTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> ContentTemplateProperty =
        BindableProperty.Create<NavigationViewItem, DataTemplate?>(
            nameof(ContentTemplate),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedContent());

    /// <summary>
    /// Identifies the <see cref="Icon"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> IconProperty =
        BindableProperty.Create<NavigationViewItem, object?>(
            nameof(Icon),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateIcon());

    /// <summary>
    /// Identifies the <see cref="Tag"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> TagProperty =
        BindableProperty.Create<NavigationViewItem, object?>(
            nameof(Tag),
            defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="IsSelected"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Create<NavigationViewItem, bool>(
            nameof(IsSelected),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.OnIsSelectedChanged());

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsExpandedProperty =
        BindableProperty.Create<NavigationViewItem, bool>(
            nameof(IsExpanded),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.OnIsExpandedChanged());

    private readonly ObservableCollection<NavigationViewItemBase> _menuItems = [];
    private readonly IDisposable _gestureSubscription;
    private StackPanel? _itemRow;
    private View? _iconHost;
    private View? _contentHost;
    private Path? _chevron;
    private StackPanel? _childrenHost;
    private UIElement? _presentedContent;
    private UIElement? _presentedIcon;

    static NavigationViewItem()
    {
        CursorProperty.OverrideMetadata<NavigationViewItem>(Cursor.Pointer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class.
    /// </summary>
    public NavigationViewItem()
    {
        _gestureSubscription = ClickGesture.Attach(this, PerformClick);
        _menuItems.CollectionChanged += OnMenuItemsCollectionChanged;
    }

    /// <inheritdoc />
    public override string TagName => "navigationviewitem";

    /// <summary>
    /// Gets or sets the visual or data content of the navigation item.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display <see cref="Content"/>.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon displayed for this item.
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets an arbitrary object value associated with this item.
    /// </summary>
    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this item is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this item's child items are expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets the collection of child navigation items displayed beneath this item.
    /// </summary>
    public IList<NavigationViewItemBase> MenuItems => _menuItems;

    /// <summary>
    /// Gets whether this item has nested child navigation items.
    /// </summary>
    public bool HasItems => _menuItems.Count > 0;

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        return name switch
        {
            "data-selected" => IsSelected ? string.Empty : null,
            "data-expanded" => (HasItems && IsExpanded) ? string.Empty : null,
            "data-collapsed" => (HasItems && !IsExpanded) ? string.Empty : null,
            "data-has-items" => HasItems ? string.Empty : null,
            _ => base.GetAttribute(name)
        };
    }

    /// <summary>
    /// Occurs when this item is invoked by pointer click or programmatic invocation.
    /// </summary>
    public event EventHandler? Invoked;

    /// <summary>
    /// Programmatically invokes this item.
    /// </summary>
    public void PerformClick()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }

        if (HasItems)
        {
            IsExpanded = !IsExpanded;
        }

        Invoked?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _itemRow = DefinePart("item-row", new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Class = ["navigation-item-row"]
        });

        _iconHost = DefinePart("icon", new View
        {
            Class = ["navigation-item-icon"],
            IsVisible = false
        });

        _contentHost = DefinePart("content", new View
        {
            Class = ["navigation-item-content"]
        });

        _chevron = DefinePart("chevron", new Path
        {
            Class = ["navigation-item-chevron"],
            Data = Icons.ChevronRight,
            Fill = SvgCurrentColorBrush.Instance,
            Stretch = Stretch.None,
            Width = 16,
            Height = 16,
            IsVisible = false
        });

        _childrenHost = DefinePart("children", new StackPanel
        {
            Class = ["navigation-item-children"],
            IsVisible = false
        });

        _itemRow.Children.Add(_iconHost);
        _itemRow.Children.Add(_contentHost);
        _itemRow.Children.Add(_chevron);

        Children.Add(_itemRow);
        Children.Add(_childrenHost);

        UpdateIcon();
        UpdatePresentedContent();
        UpdateSubmenuState();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gestureSubscription.Dispose();
            _menuItems.CollectionChanged -= OnMenuItemsCollectionChanged;
        }

        base.Dispose(disposing);
    }

    private void OnIsSelectedChanged()
    {
        SetState(ElementStates.Selected, IsSelected);
        InvalidateStyle();
    }

    private void OnIsExpandedChanged()
    {
        UpdateSubmenuState();
        InvalidateStyle();
    }

    private void OnMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_childrenHost is null)
        {
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (NavigationViewItemBase oldItem in e.OldItems)
            {
                _childrenHost.Children.Remove(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            for (int i = 0; i < e.NewItems.Count; i++)
            {
                NavigationViewItemBase newItem = (NavigationViewItemBase)e.NewItems[i]!;
                _childrenHost.Children.Insert(e.NewStartingIndex + i, newItem);
            }
        }

        UpdateSubmenuState();
    }

    private void UpdateSubmenuState()
    {
        if (_chevron is not null)
        {
            _chevron.IsVisible = HasItems;
        }

        if (_childrenHost is not null)
        {
            _childrenHost.IsVisible = HasItems && IsExpanded;
        }
    }

    private void UpdateIcon()
    {
        if (_iconHost is null)
        {
            return;
        }

        if (_presentedIcon is not null)
        {
            _iconHost.Children.Remove(_presentedIcon);
            _presentedIcon = null;
        }

        if (Icon is null)
        {
            _iconHost.IsVisible = false;
            return;
        }

        _presentedIcon = Icon switch
        {
            UIElement element => element,
            string uri when uri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                            uri.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => new Image
            {
                Source = uri,
                Width = 20,
                Height = 20
            },
            PathGeometry geometry => new Path
            {
                Data = geometry,
                Fill = SvgCurrentColorBrush.Instance,
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform
            },
            _ => new Text
            {
                Value = Convert.ToString(Icon, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        if (_presentedIcon is not null)
        {
            _iconHost.Children.Add(_presentedIcon);
            _iconHost.IsVisible = true;
        }
        else
        {
            _iconHost.IsVisible = false;
        }
    }

    private void UpdatePresentedContent()
    {
        if (_contentHost is null)
        {
            return;
        }

        if (_presentedContent is not null)
        {
            _contentHost.Children.Remove(_presentedContent);
            _presentedContent = null;
        }

        if (Content is null)
        {
            return;
        }

        _presentedContent = Content switch
        {
            UIElement element => element,
            object val when ContentTemplate is DataTemplate template => template.Build(val),
            _ => new Text
            {
                Value = Convert.ToString(Content, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        if (_presentedContent is not null)
        {
            _contentHost.Children.Add(_presentedContent);
        }
    }
}
