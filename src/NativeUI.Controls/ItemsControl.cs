using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents a control that presents a collection of <see cref="Items"/>. The template part named <c>items</c> hosts the
/// item elements when the template has one. Otherwise the control itself hosts them after its template parts. Each item
/// element is a direct child of its host, so structural pseudo-classes such as <c>:first-child</c> select item elements.
/// </summary>
/// <remarks>
/// An item that is a <see cref="UIElement"/> is its own item element. <see cref="ItemTemplate"/> builds the element of any
/// other item, and an item without a template is presented as a <see cref="Text"/> showing its string form.
/// While <see cref="ItemsSource"/> is set, <see cref="Items"/> follows the source and cannot be changed directly. The
/// control subscribes to a source that implements <see cref="INotifyCollectionChanged"/> until the source is replaced.
/// </remarks>
[ContentProperty(nameof(Items))]
public abstract class ItemsControl : Control
{
    /// <summary>
    /// The name of the template part that hosts the item elements.
    /// </summary>
    protected const string ItemsPartName = "items";

    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Create<ItemsControl, IEnumerable?>(
            nameof(ItemsSource),
            defaultValue: null,
            propertyChanged: static (owner, oldSource, newSource) => owner.OnItemsSourceChanged(oldSource, newSource));

    /// <summary>
    /// Identifies the <see cref="ItemTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> ItemTemplateProperty =
        BindableProperty.Create<ItemsControl, DataTemplate?>(
            nameof(ItemTemplate),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.OnItemsCollectionChanged(owner, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));

    private readonly ItemCollection _items;
    private readonly List<UIElement> _itemElements = [];
    private readonly View _itemsHost;
    private readonly int _itemsStart;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemsControl"/> class.
    /// </summary>
    protected ItemsControl()
    {
        _items = new ItemCollection(this);
        _itemsHost = FindItemsHost() ?? this;
        _itemsStart = _itemsHost.Children.Count;
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>
    /// Gets the items that this control presents. The collection is read-only while <see cref="ItemsSource"/> is set.
    /// </summary>
    public IList<object?> Items => _items;

    /// <summary>
    /// Gets or sets the collection that fills <see cref="Items"/>, or <see langword="null"/> to fill <see cref="Items"/> directly.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the template that builds the element of each item that is not a <see cref="UIElement"/>.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Returns the view that hosts the item elements, or <see langword="null"/> when the control hosts them itself. By
    /// default this is the template part named <c>items</c> among the parts that this control defined directly. A control
    /// whose <c>items</c> part lives inside another control, such as a popover, overrides this. The constructor of this
    /// class calls it, so an override reads only what the template has built.
    /// </summary>
    protected virtual View? FindItemsHost()
    {
        return GetTemplateChild<View>(ItemsPartName);
    }

    /// <summary>
    /// Gets the view that hosts the item elements.
    /// </summary>
    protected View ItemsHost => _itemsHost;

    /// <summary>
    /// Gets the index in <see cref="ItemsHost"/> of the first item element. Template parts in the host come before it.
    /// </summary>
    protected int ItemsStart => _itemsStart;

    /// <summary>
    /// Gets the element of each item, in item order, while the control hosts every item element.
    /// </summary>
    protected IReadOnlyList<UIElement> ItemElements => _itemElements;

    /// <summary>
    /// Returns the element of the item at <paramref name="index"/>, or <see langword="null"/> when it is not created.
    /// </summary>
    /// <param name="index">The index of the item in <see cref="Items"/>.</param>
    public virtual UIElement? GetItemElement(int index)
    {
        return index >= 0 && index < _itemElements.Count ? _itemElements[index] : null;
    }

    /// <summary>
    /// Returns the index of the item that <paramref name="element"/> presents, or <c>-1</c> when it presents none.
    /// </summary>
    /// <param name="element">An item element, or an element inside one.</param>
    public virtual int IndexOfItemElement(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        for (UIElement? current = element; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            int index = _itemElements.IndexOf(current);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates the element that presents <paramref name="item"/>: the item itself when it is a <see cref="UIElement"/>, a
    /// tree that <see cref="ItemTemplate"/> builds, or a <see cref="Text"/> showing its string form.
    /// </summary>
    /// <param name="item">The item.</param>
    protected virtual UIElement CreateItemElement(object? item)
    {
        if (item is UIElement element)
        {
            return element;
        }

        if (ItemTemplate is DataTemplate template)
        {
            return template.Build(item);
        }

        return new Text
        {
            Value = Convert.ToString(item, CultureInfo.CurrentCulture) ?? string.Empty,
            IsTextSelectionEnabled = false,
            DataContext = item
        };
    }

    /// <summary>
    /// Updates the item elements after <see cref="Items"/> or <see cref="ItemTemplate"/> changes. By default the host
    /// contains one element for every item. A virtualized control overrides this to create only the visible elements.
    /// </summary>
    /// <param name="args">The change to <see cref="Items"/>. A template change is a reset.</param>
    protected virtual void UpdateItemElements(NotifyCollectionChangedEventArgs args)
    {
        IList<UIElement> hostChildren = _itemsHost.Children;
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (int i = 0; i < args.NewItems!.Count; i++)
                {
                    UIElement created = CreateItemElement(args.NewItems[i]);
                    _itemElements.Insert(args.NewStartingIndex + i, created);
                    PrepareContainerForItemOverride(created, args.NewItems[i]);
                    hostChildren.Insert(Math.Min(_itemsStart + args.NewStartingIndex + i, hostChildren.Count), created);
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                for (int i = 0; i < args.OldItems!.Count; i++)
                {
                    ClearContainerForItemOverride(_itemElements[args.OldStartingIndex], args.OldItems[i]);
                    hostChildren.Remove(_itemElements[args.OldStartingIndex]);
                    _itemElements.RemoveAt(args.OldStartingIndex);
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                UIElement replaced = _itemElements[args.NewStartingIndex];
                ClearContainerForItemOverride(replaced, args.OldItems![0]);
                UIElement replacement = CreateItemElement(args.NewItems![0]);
                PrepareContainerForItemOverride(replacement, args.NewItems[0]);
                _itemElements[args.NewStartingIndex] = replacement;
                hostChildren[hostChildren.IndexOf(replaced)] = replacement;
                break;
            case NotifyCollectionChangedAction.Move:
                UIElement moved = _itemElements[args.OldStartingIndex];
                _itemElements.RemoveAt(args.OldStartingIndex);
                _itemElements.Insert(args.NewStartingIndex, moved);
                hostChildren.Remove(moved);
                hostChildren.Insert(Math.Min(_itemsStart + args.NewStartingIndex, hostChildren.Count), moved);
                break;
            default:
                foreach (UIElement element in _itemElements)
                {
                    ClearContainerForItemOverride(element, null);
                    hostChildren.Remove(element);
                }

                _itemElements.Clear();
                for (int i = 0; i < _items.Count; i++)
                {
                    UIElement created = CreateItemElement(_items[i]);
                    _itemElements.Add(created);
                    PrepareContainerForItemOverride(created, _items[i]);
                    hostChildren.Insert(_itemsStart + i, created);
                }

                break;
        }
    }

    /// <summary>
    /// Prepares a newly created item container before it enters the items host.
    /// </summary>
    /// <param name="element">The item container.</param>
    /// <param name="item">The data or element it presents.</param>
    protected virtual void PrepareContainerForItemOverride(UIElement element, object? item)
    {
    }

    /// <summary>
    /// Releases item-specific associations before a container leaves the items host.
    /// </summary>
    /// <param name="element">The departing container.</param>
    /// <param name="item">The previous item, or null during a complete reset.</param>
    protected virtual void ClearContainerForItemOverride(UIElement element, object? item)
    {
    }

    /// <summary>
    /// Invoked after <see cref="Items"/> changes and the item elements reflect the change.
    /// </summary>
    /// <param name="args">The collection change.</param>
    protected virtual void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && ItemsSource is INotifyCollectionChanged source)
        {
            source.CollectionChanged -= OnSourceCollectionChanged;
        }

        base.Dispose(disposing);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        UpdateItemElements(args);
        OnItemsChanged(args);
    }

    /// <summary>
    /// Invoked after <see cref="ItemsSource"/> changes. The base implementation subscribes to the new source and refills
    /// <see cref="Items"/>, so an override calls it.
    /// </summary>
    /// <param name="oldSource">The previous source.</param>
    /// <param name="newSource">The new source.</param>
    protected virtual void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        if (oldSource is INotifyCollectionChanged oldNotifier)
        {
            oldNotifier.CollectionChanged -= OnSourceCollectionChanged;
        }

        if (newSource is INotifyCollectionChanged newNotifier)
        {
            newNotifier.CollectionChanged += OnSourceCollectionChanged;
        }

        _items.ResetFromSource(newSource);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        _items.ApplySourceChange(args, ItemsSource);
    }

    /// <summary>
    /// Holds <see cref="Items"/>. It rejects direct changes while <see cref="ItemsSource"/> is set and applies the changes
    /// of the source instead.
    /// </summary>
    private sealed class ItemCollection(ItemsControl owner) : ObservableCollection<object?>
    {
        private bool _isApplyingSource;

        public void ResetFromSource(IEnumerable? source)
        {
            _isApplyingSource = true;
            try
            {
                Items.Clear();
                if (source is not null)
                {
                    foreach (object? item in source)
                    {
                        Items.Add(item);
                    }
                }

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
                _isApplyingSource = false;
            }
        }

        public void ApplySourceChange(NotifyCollectionChangedEventArgs args, IEnumerable? source)
        {
            bool hasIndices = args.Action switch
            {
                NotifyCollectionChangedAction.Add => args.NewStartingIndex >= 0,
                NotifyCollectionChangedAction.Remove => args.OldStartingIndex >= 0,
                NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move =>
                    args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0,
                _ => false
            };

            if (!hasIndices || (args.NewItems?.Count ?? 1) > 1 || (args.OldItems?.Count ?? 1) > 1)
            {
                ResetFromSource(source);
                return;
            }

            _isApplyingSource = true;
            try
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        InsertItem(args.NewStartingIndex, args.NewItems![0]);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveItem(args.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        SetItem(args.NewStartingIndex, args.NewItems![0]);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        MoveItem(args.OldStartingIndex, args.NewStartingIndex);
                        break;
                }
            }
            finally
            {
                _isApplyingSource = false;
            }
        }

        protected override void InsertItem(int index, object? item)
        {
            ThrowIfSourceBound();
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ThrowIfSourceBound();
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, object? item)
        {
            ThrowIfSourceBound();
            base.SetItem(index, item);
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            ThrowIfSourceBound();
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void ClearItems()
        {
            ThrowIfSourceBound();
            base.ClearItems();
        }

        private void ThrowIfSourceBound()
        {
            if (!_isApplyingSource && owner.ItemsSource is not null)
            {
                throw new InvalidOperationException("Items cannot be changed while ItemsSource is set.");
            }
        }
    }
}
