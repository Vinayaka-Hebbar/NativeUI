using System.Globalization;
using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Groups menu items beneath an optional header without adding a keyboard navigation stop.
/// </summary>
[ContentProperty(nameof(Items))]
public class MenuGroup : Control
{
    /// <summary>
    /// Identifies the <see cref="Header"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Create<MenuGroup, object?>(nameof(Header), defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateHeader());

    private View? _headerHost;
    private View? _itemsHost;
    private UIElement? _presentedHeader;

    /// <inheritdoc />
    public override string TagName => "menugroup";

    /// <summary>
    /// Gets or sets the header, presented as an element or as nonselectable text.
    /// </summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets the collection of menu items, separators, and nested groups.
    /// </summary>
    public IList<UIElement> Items => (_itemsHost ?? this).Children;

    /// <summary>
    /// Gets the header part used to label the group for assistive technologies.
    /// </summary>
    internal View? HeaderPart => _headerHost;

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _headerHost = DefinePart(new View(), "header");
        _itemsHost = DefinePart(new View(), "items");
        Children.Add(_headerHost);
        Children.Add(_itemsHost);
        UpdateHeader();
    }

    /// <inheritdoc />
    protected override void OnParentChanged()
    {
        base.OnParentChanged();
        if (Parent is null)
        {
            CloseSubmenus(Items);
        }
    }

    private static void CloseSubmenus(IEnumerable<UIElement> items)
    {
        foreach (UIElement child in items)
        {
            if (child is MenuItem item)
            {
                item.CloseSubmenu();
            }
            else if (child is MenuGroup group)
            {
                CloseSubmenus(group.Items);
            }
        }
    }

    private void UpdateHeader()
    {
        if (_headerHost is null)
        {
            return;
        }

        if (_presentedHeader is not null)
        {
            _headerHost.Children.Remove(_presentedHeader);
        }

        _presentedHeader = Header switch
        {
            null => null,
            UIElement element => element,
            object value => new Text
            {
                Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };
        if (_presentedHeader is not null)
        {
            _headerHost.Children.Add(_presentedHeader);
        }

        _headerHost.IsVisible = _presentedHeader is not null;
    }
}
