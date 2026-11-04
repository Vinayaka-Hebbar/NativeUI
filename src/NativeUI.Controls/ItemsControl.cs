using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents a control that presents a collection of <see cref="Items"/>. The template part named <c>items</c> hosts the
/// items when the template has one. Otherwise the control itself hosts the items after its template parts. Each item is a
/// direct child of its host, so structural pseudo-classes such as <c>:first-child</c> select items.
/// </summary>
[ContentProperty(nameof(Items))]
public abstract class ItemsControl : Control
{
    /// <summary>
    /// The name of the template part that hosts <see cref="Items"/>.
    /// </summary>
    protected const string ItemsPartName = "items";

    private readonly ObservableCollection<UIElement> _items = [];
    private readonly View _itemsHost;
    private readonly int _itemsStart;
    private int _hostedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemsControl"/> class.
    /// </summary>
    protected ItemsControl()
    {
        _itemsHost = GetTemplateChild<View>(ItemsPartName) ?? this;
        _itemsStart = _itemsHost.Children.Count;
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>
    /// Gets the items that this control presents.
    /// </summary>
    public IList<UIElement> Items => _items;

    /// <summary>
    /// Invoked after <see cref="Items"/> changes and the items host reflects the change.
    /// </summary>
    /// <param name="args">The collection change.</param>
    protected virtual void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IList<UIElement> hostChildren = _itemsHost.Children;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    hostChildren.Insert(_itemsStart + e.NewStartingIndex + i, (UIElement)e.NewItems[i]!);
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    hostChildren.RemoveAt(_itemsStart + e.OldStartingIndex);
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                hostChildren[_itemsStart + e.NewStartingIndex] = (UIElement)e.NewItems![0]!;
                break;
            case NotifyCollectionChangedAction.Move:
                UIElement moved = hostChildren[_itemsStart + e.OldStartingIndex];
                hostChildren.RemoveAt(_itemsStart + e.OldStartingIndex);
                hostChildren.Insert(_itemsStart + e.NewStartingIndex, moved);
                break;
            case NotifyCollectionChangedAction.Reset:
                for (int i = 0; i < _hostedCount; i++)
                {
                    hostChildren.RemoveAt(_itemsStart);
                }

                for (int i = 0; i < _items.Count; i++)
                {
                    hostChildren.Insert(_itemsStart + i, _items[i]);
                }

                break;
        }

        _hostedCount = _items.Count;
        OnItemsChanged(e);
    }
}
