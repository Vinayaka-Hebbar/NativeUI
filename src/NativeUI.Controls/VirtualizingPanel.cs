using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Hosts the item elements of a virtualized <see cref="Selector"/> as its <c>items</c> part, stacked vertically with one
/// height for every item. The panel creates elements only for the items in the visible range of the selector, plus
/// <see cref="Overscan"/> items beyond each end. A spacer in normal flow gives the panel the size of all items, and each
/// created element is positioned absolutely at its item's offset. The panel is positioned so that it contains them.
/// </summary>
/// <remarks>
/// Positions are display positions. The selector maps each position to an item index, which lets a sorted table show
/// items in a different order than <see cref="ItemsControl.Items"/>.
/// </remarks>
internal abstract class VirtualizingPanel : View
{
    private readonly View _spacer = new() { Height = 0 };
    private readonly Dictionary<int, UIElement> _elements = [];
    private Selector? _owner;
    private Func<int, UIElement>? _createElement;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizingPanel"/> class.
    /// </summary>
    protected VirtualizingPanel()
    {
        Position = NativeUI.Layout.Position.Relative;
        Children.Add(_spacer);
        SizeChanged += (_, _) => Update();
    }

    /// <summary>
    /// Gets or sets the height of each item in logical pixels.
    /// </summary>
    public double ItemHeight { get; set; } = 40;

    /// <summary>
    /// Gets or sets the logical pixels between adjacent items.
    /// </summary>
    public double ItemSpacing { get; set; }

    /// <summary>
    /// Gets or sets the number of items created beyond each end of the visible range.
    /// </summary>
    public int Overscan { get; set; } = 3;

    /// <summary>
    /// Gets the distance from the top of one item to the top of the next.
    /// </summary>
    public double Pitch => Math.Max(1, ItemHeight + Math.Max(0, ItemSpacing));

    /// <summary>
    /// Gets the selector whose items the panel hosts, or <see langword="null"/> before <see cref="Attach"/>.
    /// </summary>
    protected Selector? Owner => _owner;

    /// <summary>
    /// Gets the spacer whose size is the extent of all items.
    /// </summary>
    protected View Spacer => _spacer;

    /// <summary>
    /// Gets the created elements by display position.
    /// </summary>
    protected IReadOnlyDictionary<int, UIElement> Elements => _elements;

    /// <summary>
    /// Connects the panel to the selector whose items it hosts. The panel follows the selector's scrolling and size.
    /// </summary>
    /// <param name="owner">The selector that scrolls and owns the items.</param>
    /// <param name="createElement">Creates the element of the item at a display position.</param>
    public void Attach(Selector owner, Func<int, UIElement> createElement)
    {
        _owner = owner;
        _createElement = createElement;
        owner.ScrollChanged += (_, _) => Update();
        owner.SizeChanged += (_, _) => Update();
    }

    /// <summary>
    /// Returns the element at <paramref name="position"/>, or <see langword="null"/> when it is not created.
    /// </summary>
    public UIElement? GetElement(int position)
    {
        return _elements.GetValueOrDefault(position);
    }

    /// <summary>
    /// Returns the position whose element is or contains <paramref name="element"/>, or <c>-1</c>.
    /// </summary>
    public int IndexOf(UIElement element)
    {
        for (UIElement? current = element; current is not null && !ReferenceEquals(current, this); current = current.Parent)
        {
            foreach ((int position, UIElement created) in _elements)
            {
                if (ReferenceEquals(created, current))
                {
                    return position;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the top of the item at <paramref name="position"/> within the owner's scrolled content.
    /// </summary>
    public double GetItemTop(int position)
    {
        return GetPanelTop() + (position * Pitch);
    }

    /// <summary>
    /// Removes every created element and creates the elements of the visible range again.
    /// </summary>
    public void Reset()
    {
        foreach (UIElement element in _elements.Values)
        {
            Children.Remove(element);
        }

        _elements.Clear();
        Update();
    }

    /// <summary>
    /// Sizes the spacer, removes the elements that left the visible range, and creates the elements that entered it.
    /// </summary>
    public void Update()
    {
        if (_owner is null || _createElement is null)
        {
            return;
        }

        int count = _owner.Items.Count;
        _spacer.Height = count == 0 ? 0 : (count * Pitch) - Math.Max(0, ItemSpacing);

        double viewport = _owner.ViewportHeight > 0 ? _owner.ViewportHeight : _owner.ActualHeight;
        double start = _owner.VerticalOffset - GetPanelTop();
        int first = Math.Max(0, (int)Math.Floor(start / Pitch) - Overscan);
        int last = Math.Min(count - 1, (int)Math.Ceiling((start + viewport) / Pitch) + Overscan);

        List<int> departed = [];
        foreach (int position in _elements.Keys)
        {
            if (position < first || position > last)
            {
                departed.Add(position);
            }
        }

        foreach (int position in departed)
        {
            Children.Remove(_elements[position]);
            _elements.Remove(position);
        }

        for (int position = first; position <= last; position++)
        {
            if (_elements.ContainsKey(position))
            {
                continue;
            }

            UIElement element = _createElement(position);
            element.Position = NativeUI.Layout.Position.Absolute;
            element.Top = position * Pitch;
            element.Left = 0;
            element.Height = ItemHeight;
            _elements[position] = element;
            Children.Add(element);
        }

        OnElementsUpdated();
    }

    /// <summary>
    /// Invoked after <see cref="Update"/> creates and removes elements. The base implementation stretches each element to
    /// the panel's width.
    /// </summary>
    protected virtual void OnElementsUpdated()
    {
        foreach (UIElement element in _elements.Values)
        {
            element.Right = 0;
            element.Width = null;
        }
    }

    /// <summary>
    /// Returns the top of the panel within the owner's scrolled content. The top is never negative. During the first
    /// layout the panel's bounds can lag the owner's bounds, and the panel's <see cref="UIElement.SizeChanged"/> then
    /// updates the visible range.
    /// </summary>
    protected double GetPanelTop()
    {
        if (_owner is null)
        {
            return 0;
        }

        return Math.Max(0, Bounds.Y - _owner.Bounds.Y - _owner.ComputedStyle.BorderWidth + _owner.VerticalOffset);
    }
}
