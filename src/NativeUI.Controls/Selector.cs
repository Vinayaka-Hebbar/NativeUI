using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents an items control whose items the user can select. The selection holds item indices, which follow their
/// items when <see cref="ItemsControl.Items"/> changes. An item element has <see cref="ElementStates.Selected"/>, selected
/// by <c>:selected</c>, while its item is selected. The control receives keyboard focus and scrolls its own content.
/// </summary>
/// <remarks>
/// A click selects an item. While <see cref="SelectionMode"/> is <see cref="Controls.SelectionMode.Multiple"/>, Command or
/// Control toggles an item, and Shift selects the range from the item clicked before. While the control has focus, the
/// Up and Down arrows, Home, and End select an adjacent, first, or last item, Shift extends the range, and Space toggles
/// the current item.
/// </remarks>
public abstract class Selector : ItemsControl
{
    static Selector()
    {
        FocusableProperty.OverrideMetadata<Selector>(true);
        OverflowYProperty.OverrideMetadata<Selector>(Overflow.Auto);
    }

    /// <summary>
    /// Identifies the <see cref="SelectionChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<SelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<Selector, SelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="SelectionMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<SelectionMode> SelectionModeProperty =
        BindableProperty.Create<Selector, SelectionMode>(
            nameof(SelectionMode),
            defaultValue: SelectionMode.Single,
            propertyChanged: static (owner, _, _) => owner.CoerceSelectionToMode());

    /// <summary>
    /// Identifies the <see cref="SelectedIndex"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Create<Selector, int>(
            nameof(SelectedIndex),
            defaultValue: -1,
            propertyChanged: static (owner, _, newIndex) => owner.OnSelectedIndexPropertyChanged(newIndex));

    /// <summary>
    /// Identifies the <see cref="SelectedItem"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Create<Selector, object?>(
            nameof(SelectedItem),
            defaultValue: null,
            propertyChanged: static (owner, _, newItem) => owner.OnSelectedItemPropertyChanged(newItem));

    private SortedSet<int> _selected = [];
    private List<object?> _selectedItems = [];
    private readonly ItemSelection _selection;
    private int _anchorIndex = -1;
    private int _currentIndex = -1;
    private bool _isSyncingProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="Selector"/> class.
    /// </summary>
    protected Selector()
    {
        _selection = new ItemSelection(this);
        PointerPressed += OnSelectorPointerPressed;
        KeyDown += OnSelectorKeyDown;
    }

    /// <summary>
    /// Occurs after the selection changes, whether the user or code changed it.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>
    /// Gets or sets how many items the user can select. Defaults to <see cref="Controls.SelectionMode.Single"/>.
    /// Changing the mode deselects every item that the new mode does not allow, keeping the first selected item.
    /// </summary>
    public SelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the index of the first selected item, or <c>-1</c> when no item is selected. Setting an index replaces
    /// the selection with that item, and <c>-1</c> deselects every item.
    /// </summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the first selected item, or <see langword="null"/> when no item is selected. Setting an item replaces
    /// the selection with the first equal item in <see cref="ItemsControl.Items"/>.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets the indices of the selected items in ascending order.
    /// </summary>
    public IReadOnlyList<int> SelectedIndices => [.. _selected];

    /// <summary>
    /// Gets the selected items in index order.
    /// </summary>
    public IReadOnlyList<object?> SelectedItems => _selectedItems.AsReadOnly();

    /// <summary>
    /// Gets the operations that change the selection by item index.
    /// </summary>
    public IItemSelection Selection => _selection;

    /// <summary>
    /// Scrolls so that the item at <paramref name="index"/> is visible, placed as <paramref name="alignment"/> specifies.
    /// Nothing scrolls before the item has been laid out.
    /// </summary>
    /// <param name="index">The index of the item in <see cref="ItemsControl.Items"/>.</param>
    /// <param name="alignment">Where the item is placed in the visible area.</param>
    public void ScrollToIndex(int index, ScrollAlignment alignment = ScrollAlignment.Nearest)
    {
        if (index < 0 || index >= Items.Count || !TryGetItemExtent(index, out double top, out double height))
        {
            return;
        }

        double target = GetAlignedOffset(top, height, ViewportHeight, VerticalOffset, alignment);
        if (target != VerticalOffset)
        {
            ScrollTo(HorizontalOffset, target);
        }
    }

    /// <summary>
    /// Returns the scroll offset along one axis that places the range from <paramref name="start"/> with length
    /// <paramref name="length"/> as <paramref name="alignment"/> specifies.
    /// </summary>
    /// <param name="start">The start of the range within the scrolled content.</param>
    /// <param name="length">The length of the range.</param>
    /// <param name="viewport">The visible length.</param>
    /// <param name="offset">The current scroll offset.</param>
    /// <param name="alignment">Where the range is placed in the visible area.</param>
    protected static double GetAlignedOffset(double start, double length, double viewport, double offset, ScrollAlignment alignment)
    {
        return alignment switch
        {
            ScrollAlignment.Start => start,
            ScrollAlignment.End => start + length - viewport,
            ScrollAlignment.Center => start + (length / 2) - (viewport / 2),
            _ when start < offset => start,
            _ when start + length > offset + viewport => start + length - viewport,
            _ => offset
        };
    }

    /// <summary>
    /// Applies the selected state of every item to its element, after the control created item elements again.
    /// </summary>
    protected void RefreshItemStates()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            UpdateItemState(i);
        }
    }

    /// <summary>
    /// Scrolls so that the first item equal to <paramref name="item"/> is visible, placed as <paramref name="alignment"/>
    /// specifies.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="alignment">Where the item is placed in the visible area.</param>
    public void ScrollToItem(object? item, ScrollAlignment alignment = ScrollAlignment.Nearest)
    {
        ScrollToIndex(Items.IndexOf(item), alignment);
    }

    /// <summary>
    /// Returns whether the item at <paramref name="index"/> is selected.
    /// </summary>
    protected bool IsSelectedIndex(int index)
    {
        return _selected.Contains(index);
    }

    /// <summary>
    /// Returns the vertical position of the item at <paramref name="index"/> within the scrolled content and its height.
    /// By default the item element's last layout provides them. A virtualized control computes them from its item height.
    /// </summary>
    /// <returns><see langword="true"/> when the position is known.</returns>
    protected virtual bool TryGetItemExtent(int index, out double top, out double height)
    {
        if (GetItemElement(index) is UIElement element && element.ActualHeight > 0)
        {
            top = element.Bounds.Y - Bounds.Y - ComputedStyle.BorderWidth + VerticalOffset;
            height = element.ActualHeight;
            return true;
        }

        top = 0;
        height = 0;
        return false;
    }

    /// <summary>
    /// Applies the selected state of the item at <paramref name="index"/> to its element, when the element exists.
    /// </summary>
    protected void UpdateItemState(int index)
    {
        if (GetItemElement(index) is UIElement element)
        {
            ApplySelectedState(element, _selected.Contains(index));
        }
    }

    /// <summary>
    /// Applies the selected state of an item to its element. By default this sets or clears
    /// <see cref="ElementStates.Selected"/>. A control whose item elements expose selection overrides it.
    /// </summary>
    protected virtual void ApplySelectedState(UIElement element, bool isSelected)
    {
        if (element.States.HasFlag(ElementStates.Selected) != isSelected)
        {
            element.SetState(ElementStates.Selected, isSelected);
            element.InvalidateStyle();
        }
    }

    /// <summary>
    /// Returns the index that a navigation key moves the current item to, or <c>-1</c> when the key does not navigate.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="currentIndex">The current item index, or <c>-1</c> when there is none.</param>
    protected virtual int GetNavigationIndex(Key key, int currentIndex)
    {
        int last = Items.Count - 1;
        return key switch
        {
            Key.ArrowDown => currentIndex < 0 ? 0 : Math.Min(last, currentIndex + 1),
            Key.ArrowUp => currentIndex < 0 ? last : Math.Max(0, currentIndex - 1),
            Key.Home => 0,
            Key.End => last,
            _ => -1
        };
    }

    /// <summary>
    /// Returns the indices of the items from <paramref name="startIndex"/> to <paramref name="endIndex"/> in display order,
    /// inclusive and in either direction. By default the display order is the item order.
    /// </summary>
    protected virtual IEnumerable<int> GetRangeIndices(int startIndex, int endIndex)
    {
        for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
        {
            yield return i;
        }
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        base.OnItemsChanged(args);

        SortedSet<int> next = [];
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (int index in _selected)
                {
                    next.Add(index < args.NewStartingIndex ? index : index + args.NewItems!.Count);
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                int removedEnd = args.OldStartingIndex + args.OldItems!.Count;
                foreach (int index in _selected)
                {
                    if (index < args.OldStartingIndex)
                    {
                        next.Add(index);
                    }
                    else if (index >= removedEnd)
                    {
                        next.Add(index - args.OldItems.Count);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                foreach (int index in _selected)
                {
                    if (index != args.NewStartingIndex)
                    {
                        next.Add(index);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Move:
                foreach (int index in _selected)
                {
                    next.Add(GetIndexAfterMove(index, args.OldStartingIndex, args.NewStartingIndex));
                }

                break;
            default:
                foreach (object? item in _selectedItems)
                {
                    int index = Items.IndexOf(item);
                    if (index >= 0)
                    {
                        next.Add(index);
                    }
                }

                break;
        }

        _anchorIndex = Math.Min(_anchorIndex, Items.Count - 1);
        _currentIndex = Math.Min(_currentIndex, Items.Count - 1);
        SetSelection(next);
        RefreshItemStates();
    }

    private static int GetIndexAfterMove(int index, int oldIndex, int newIndex)
    {
        if (index == oldIndex)
        {
            return newIndex;
        }

        if (oldIndex < newIndex && index > oldIndex && index <= newIndex)
        {
            return index - 1;
        }

        if (newIndex < oldIndex && index >= newIndex && index < oldIndex)
        {
            return index + 1;
        }

        return index;
    }

    /// <summary>
    /// Replaces the selection, updates item states and the selection properties, and raises
    /// <see cref="SelectionChangedEvent"/> when items were selected or deselected.
    /// </summary>
    private void SetSelection(SortedSet<int> next)
    {
        List<object?> previousItems = _selectedItems;
        SortedSet<int> previous = _selected;
        _selected = next;
        _selectedItems = [];
        foreach (int index in next)
        {
            _selectedItems.Add(Items[index]);
        }

        foreach (int index in previous)
        {
            if (index < Items.Count)
            {
                UpdateItemState(index);
            }
        }

        foreach (int index in next)
        {
            UpdateItemState(index);
        }

        SyncSelectionProperties();

        List<object?> removed = [];
        foreach (object? item in previousItems)
        {
            if (!_selectedItems.Contains(item))
            {
                removed.Add(item);
            }
        }

        List<object?> added = [];
        foreach (object? item in _selectedItems)
        {
            if (!previousItems.Contains(item))
            {
                added.Add(item);
            }
        }

        if (removed.Count > 0 || added.Count > 0)
        {
            RaiseEvent(SelectionChangedEvent, new SelectionChangedEventArgs(removed, added));
        }
    }

    private void SyncSelectionProperties()
    {
        _isSyncingProperties = true;
        try
        {
            SelectedIndex = _selected.Count > 0 ? _selected.Min : -1;
            SelectedItem = _selected.Count > 0 ? Items[_selected.Min] : null;
        }
        finally
        {
            _isSyncingProperties = false;
        }
    }

    private void OnSelectedIndexPropertyChanged(int index)
    {
        if (_isSyncingProperties)
        {
            return;
        }

        if (index >= 0 && index < Items.Count && SelectionMode != SelectionMode.None)
        {
            SetSelection([index]);
        }
        else
        {
            SetSelection([]);
        }
    }

    private void OnSelectedItemPropertyChanged(object? item)
    {
        if (!_isSyncingProperties)
        {
            OnSelectedIndexPropertyChanged(Items.IndexOf(item));
        }
    }

    private void CoerceSelectionToMode()
    {
        if (SelectionMode == SelectionMode.None)
        {
            SetSelection([]);
        }
        else if (SelectionMode == SelectionMode.Single && _selected.Count > 1)
        {
            SetSelection([_selected.Min]);
        }
    }

    private void SelectOnly(int index)
    {
        SetSelection(SelectionMode == SelectionMode.None ? [] : [index]);
    }

    private void SelectRange(int startIndex, int endIndex)
    {
        if (SelectionMode != SelectionMode.Multiple)
        {
            SelectOnly(endIndex);
            return;
        }

        SetSelection([.. GetRangeIndices(startIndex, endIndex)]);
    }

    private void Toggle(int index)
    {
        SortedSet<int> next = SelectionMode == SelectionMode.Multiple ? new SortedSet<int>(_selected) : [];
        if (_selected.Contains(index))
        {
            next.Remove(index);
        }
        else if (SelectionMode != SelectionMode.None)
        {
            next.Add(index);
        }

        SetSelection(next);
    }

    private void OnSelectorPointerPressed(object? sender, PointerEventArgs args)
    {
        if (!IsEnabled || args.Source is not UIElement source)
        {
            return;
        }

        int index = IndexOfItemElement(source);
        if (index < 0)
        {
            return;
        }

        bool isToggle = (args.Modifiers & (InputModifiers.Meta | InputModifiers.Control)) != InputModifiers.None;
        bool isRange = args.Modifiers.HasFlag(InputModifiers.Shift);
        if (SelectionMode == SelectionMode.Multiple && isRange && _anchorIndex >= 0)
        {
            SelectRange(_anchorIndex, index);
        }
        else if (SelectionMode == SelectionMode.Multiple && isToggle)
        {
            Toggle(index);
            _anchorIndex = index;
        }
        else
        {
            SelectOnly(index);
            _anchorIndex = index;
        }

        _currentIndex = index;
    }

    private void OnSelectorKeyDown(object? sender, KeyEventArgs args)
    {
        if (!ReferenceEquals(args.Source, this) || !IsEnabled || Items.Count == 0 || SelectionMode == SelectionMode.None)
        {
            return;
        }

        bool isCommand = (args.Modifiers & (InputModifiers.Meta | InputModifiers.Control)) != InputModifiers.None;
        if (args.Key == Key.Space && !isCommand)
        {
            if (_currentIndex >= 0)
            {
                Toggle(_currentIndex);
                _anchorIndex = _currentIndex;
            }

            args.Handled = true;
            return;
        }

        if (isCommand && SelectionMode == SelectionMode.Multiple && args.Key == Key.Character &&
            string.Equals(args.Text, "a", StringComparison.OrdinalIgnoreCase))
        {
            SelectRange(0, Items.Count - 1);
            args.Handled = true;
            return;
        }

        int next = GetNavigationIndex(args.Key, _currentIndex >= 0 ? _currentIndex : SelectedIndex);
        if (next < 0)
        {
            return;
        }

        if (args.Modifiers.HasFlag(InputModifiers.Shift) && SelectionMode == SelectionMode.Multiple)
        {
            SelectRange(_anchorIndex >= 0 ? _anchorIndex : next, next);
        }
        else
        {
            SelectOnly(next);
            _anchorIndex = next;
        }

        _currentIndex = next;
        ScrollToIndex(next);
        args.Handled = true;
    }

    /// <summary>
    /// Implements <see cref="IItemSelection"/> on the selection of its selector.
    /// </summary>
    private sealed class ItemSelection(Selector owner) : IItemSelection
    {
        public bool IsSelected(int index)
        {
            return owner._selected.Contains(index);
        }

        public void Select(int index)
        {
            ValidateIndex(index);
            if (owner.SelectionMode == SelectionMode.Multiple)
            {
                owner.SetSelection(new SortedSet<int>(owner._selected) { index });
            }
            else
            {
                owner.SelectOnly(index);
            }
        }

        public void Deselect(int index)
        {
            ValidateIndex(index);
            SortedSet<int> next = new(owner._selected);
            next.Remove(index);
            owner.SetSelection(next);
        }

        public void Toggle(int index)
        {
            ValidateIndex(index);
            owner.Toggle(index);
        }

        public void SelectRange(int startIndex, int endIndex)
        {
            ValidateIndex(startIndex);
            ValidateIndex(endIndex);
            owner.SelectRange(startIndex, endIndex);
        }

        public void Clear()
        {
            owner.SetSelection([]);
        }

        private void ValidateIndex(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, owner.Items.Count);
        }
    }
}
