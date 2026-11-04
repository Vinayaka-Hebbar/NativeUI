using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a vertical list of selectable items that creates elements only for the visible items plus
/// <see cref="Overscan"/> items on each side. Every item has the height <see cref="ItemHeight"/>. Each element is hosted by a
/// <see cref="ListItem"/>, the <c>item</c> template part, as in <see cref="List"/>. Selection holds item indices, so it is
/// kept for items without elements. A height and CSS <c>overflow</c> let the list scroll.
/// </summary>
public class VirtualizedList : Selector
{
    private const string ItemPartName = "item";

    static VirtualizedList()
    {
        PositionProperty.OverrideMetadata<VirtualizedList>(NativeUI.Layout.Position.Relative);
    }

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

    private readonly ItemRealizer _realizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedList"/> class.
    /// </summary>
    public VirtualizedList()
    {
        _realizer = new ItemRealizer(this, this, CreateRealizedElement);
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
    public override UIElement? GetItemElement(int index)
    {
        return _realizer.GetElement(index);
    }

    /// <inheritdoc />
    public override int IndexOfItemElement(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _realizer.IndexOf(element);
    }

    /// <inheritdoc />
    protected override void UpdateItemElements(NotifyCollectionChangedEventArgs args)
    {
        _realizer.Reset();
    }

    /// <inheritdoc />
    protected override bool TryGetItemExtent(int index, out double top, out double height)
    {
        top = _realizer.GetItemTop(index);
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
        // The realizer is null while the base constructor sets property defaults.
        if (_realizer is null)
        {
            return;
        }

        _realizer.ItemHeight = Math.Max(1, ItemHeight);
        _realizer.ItemSpacing = ItemSpacing;
        _realizer.Overscan = Math.Max(0, Overscan);
        _realizer.Reset();
    }
}
