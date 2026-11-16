using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a vertical list of selectable items that creates an element for every item. Each item element is hosted by a
/// <see cref="ListItem"/>, the <c>item</c> template part, so <c>list::part(item)</c> styles every item and
/// <c>list::part(item):selected</c> styles the selected ones. CSS <c>overflow</c> and a height let the list scroll.
/// </summary>
public class List : Selector, IStackLayoutNode
{
    private const string ItemPartName = "item";

    /// <summary>
    /// Identifies the <see cref="ItemHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ItemHeightProperty =
        BindableProperty.Create<List, double>(
            nameof(ItemHeight),
            defaultValue: double.NaN,
            propertyChanged: static (owner, _, _) => owner.UpdateItemHeights());

    /// <summary>
    /// Identifies the <see cref="ItemSpacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ItemSpacingProperty =
        BindableProperty.Create<List, double>(
            nameof(ItemSpacing),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <inheritdoc />
    public override string TagName => "list";

    /// <summary>
    /// Gets or sets the height of every item in logical pixels, or <see cref="double.NaN"/> to size each item to its
    /// content. Defaults to <see cref="double.NaN"/>.
    /// </summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the logical pixels between adjacent items. A positive value overrides the CSS <c>gap</c> property.
    /// </summary>
    public double ItemSpacing
    {
        get => GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Vertical;

    double IStackLayoutNode.Spacing => ItemSpacing;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <inheritdoc />
    protected override UIElement CreateItemElement(object? item)
    {
        ListItem container = DefinePart(ItemPartName, new ListItem());
        if (double.IsFinite(ItemHeight))
        {
            container.Height = ItemHeight;
        }

        container.Children.Add(base.CreateItemElement(item));
        return container;
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

    private void UpdateItemHeights()
    {
        double? height = double.IsFinite(ItemHeight) ? ItemHeight : null;
        foreach (UIElement element in ItemElements)
        {
            element.Height = height;
        }
    }
}
