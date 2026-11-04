using System.Collections.Specialized;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Represents a vertical list of selectable items that creates elements only for the visible items plus
/// <see cref="Overscan"/> items on each side. Every item has the height <see cref="ItemHeight"/>. The <c>items</c> part holds
/// the created elements, and a <see cref="ListItem"/>, the <c>item</c> part, hosts each one, as in <see cref="List"/>. The
/// selection keeps items that have no element. A height and CSS <c>overflow</c> let the list scroll.
/// </summary>
public class VirtualizedList : Selector
{
    private const string ItemPartName = "item";

    /// <summary>
    /// Identifies the <see cref="ItemHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ItemHeightProperty =
        BindableProperty.Create<VirtualizedList, double>(
            nameof(ItemHeight),
            defaultValue: 40,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutSettings());

    /// <summary>
    /// Identifies the <see cref="ItemSpacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ItemSpacingProperty =
        BindableProperty.Create<VirtualizedList, double>(
            nameof(ItemSpacing),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutSettings());

    /// <summary>
    /// Identifies the <see cref="Overscan"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> OverscanProperty =
        BindableProperty.Create<VirtualizedList, int>(
            nameof(Overscan),
            defaultValue: 3,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutSettings());

    private readonly VirtualizingStackPanel _panel;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedList"/> class.
    /// </summary>
    public VirtualizedList()
    {
        _panel = (VirtualizingStackPanel)ItemsHost;
        _panel.Attach(this, CreateRealizedElement);
        UpdateLayoutSettings();
    }

    /// <inheritdoc />
    public override string TagName => "virtualizedlist";

    /// <summary>
    /// Gets or sets the height of every item in logical pixels. Defaults to 40. A height that is not a positive number
    /// counts as one logical pixel.
    /// </summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the logical pixels between adjacent items.
    /// </summary>
    public double ItemSpacing
    {
        get => GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of items created beyond each end of the visible range. Defaults to 3.
    /// </summary>
    public int Overscan
    {
        get => GetValue(OverscanProperty);
        set => SetValue(OverscanProperty, value);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        Children.Add(DefinePart(new VirtualizingStackPanel(), ItemsPartName));
    }

    /// <inheritdoc />
    public override UIElement? GetItemElement(int index)
    {
        return _panel.GetElement(index);
    }

    /// <inheritdoc />
    public override int IndexOfItemElement(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _panel.IndexOf(element);
    }

    /// <inheritdoc />
    protected override void UpdateItemElements(NotifyCollectionChangedEventArgs args)
    {
        _panel.Reset();
    }

    /// <inheritdoc />
    protected override bool TryGetItemExtent(int index, out double top, out double height)
    {
        top = _panel.GetItemTop(index);
        height = ItemHeight;
        return true;
    }

    /// <inheritdoc />
    protected override void ApplySelectedState(UIElement element, bool isSelected)
    {
        if (element is ListItem listItem)
        {
            listItem.IsSelected = isSelected;
        }
        else
        {
            base.ApplySelectedState(element, isSelected);
        }
    }

    private UIElement CreateRealizedElement(int index)
    {
        ListItem container = DefinePart(new ListItem(), ItemPartName);
        container.Children.Add(CreateItemElement(Items[index]));
        ApplySelectedState(container, IsSelectedIndex(index));
        return container;
    }

    private void UpdateLayoutSettings()
    {
        // The panel is null while the base constructors set property defaults.
        if (_panel is null)
        {
            return;
        }

        _panel.ItemHeight = Math.Max(1, ItemHeight);
        _panel.ItemSpacing = ItemSpacing;
        _panel.Overscan = Math.Max(0, Overscan);
        _panel.Reset();
    }
}
