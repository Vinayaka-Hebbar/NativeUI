using System.Collections.Specialized;
using System.Globalization;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents the path to the current page as a row of items. Each item is hosted by a <see cref="BreadcrumbItem"/>, the
/// <c>item</c> part, with a <c>separator</c> part before every item but the first. The last item is the current page.
/// A click on another item, or Enter or Space while it has focus, raises <see cref="ItemClickEvent"/>.
/// </summary>
public class Breadcrumb : ItemsControl, IStackLayoutNode
{
    private const string ItemPartName = "item";
    private const string SeparatorPartName = "separator";

    static Breadcrumb()
    {
        WidthProperty.OverrideMetadata<Breadcrumb>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <see cref="ItemClick"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<ItemClickEventArgs> ItemClickEvent =
        RoutedEvent.Register<Breadcrumb, ItemClickEventArgs>(nameof(ItemClick), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="Separator"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> SeparatorProperty =
        BindableProperty.Create<Breadcrumb, string>(
            nameof(Separator),
            defaultValue: "/",
            propertyChanged: static (owner, _, _) => owner.RebuildItems());

    /// <summary>
    /// Identifies the <see cref="SeparatorTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> SeparatorTemplateProperty =
        BindableProperty.Create<Breadcrumb, DataTemplate?>(
            nameof(SeparatorTemplate),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.RebuildItems());

    /// <summary>
    /// Occurs when the user clicks an item that is not the current page.
    /// </summary>
    public event EventHandler<ItemClickEventArgs> ItemClick
    {
        add => AddHandler(ItemClickEvent, value);
        remove => RemoveHandler(ItemClickEvent, value);
    }

    /// <inheritdoc />
    public override string TagName => "breadcrumb";

    /// <summary>
    /// Gets or sets the text between items. Defaults to <c>/</c>. <see cref="SeparatorTemplate"/> takes precedence.
    /// </summary>
    public string Separator
    {
        get => GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    /// <summary>
    /// Gets or sets the template that builds the element between items, such as an icon. It receives no data item.
    /// </summary>
    public DataTemplate? SeparatorTemplate
    {
        get => GetValue(SeparatorTemplateProperty);
        set => SetValue(SeparatorTemplateProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Horizontal;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <inheritdoc />
    protected override UIElement CreateItemElement(object? item)
    {
        BreadcrumbItem container = DefinePart(ItemPartName, new BreadcrumbItem());
        UIElement separator = SeparatorTemplate is DataTemplate template
            ? template.Build(null)
            : new Text { Value = Separator, IsTextSelectionEnabled = false };
        container.Children.Add(DefinePart(SeparatorPartName, separator));
        container.Children.Add(base.CreateItemElement(item));
        ClickGesture.Attach(container, () => OnItemClicked(container));
        return container;
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        base.OnItemsChanged(args);
        UpdateItemStates();
    }

    private void RebuildItems()
    {
        UpdateItemElements(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        UpdateItemStates();
    }

    /// <summary>
    /// Hides the separator of the first item and marks the last item as the current page.
    /// </summary>
    private void UpdateItemStates()
    {
        int last = Items.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            if (GetItemElement(i) is BreadcrumbItem container)
            {
                container.IsCurrent = i == last;
                container.Children[0].IsVisible = i > 0;
            }
        }
    }

    private void OnItemClicked(BreadcrumbItem container)
    {
        int index = IndexOfItemElement(container);
        if (index >= 0 && !container.IsCurrent)
        {
            RaiseEvent(ItemClickEvent, new ItemClickEventArgs(Items[index], index));
        }
    }
}
