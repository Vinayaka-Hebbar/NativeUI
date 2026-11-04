using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Creates elements only for the items that are visible in a scrolling <see cref="Selector"/>, plus an overscan on each
/// side. Every item has the same height. A spacer in normal flow gives the host the height of all items, and each
/// created element is positioned absolutely at its item's offset. The host must be positioned.
/// </summary>
internal sealed class ItemRealizer
{
    private readonly Selector _owner;
    private readonly View _host;
    private readonly Func<int, UIElement> _create;
    private readonly View _spacer = new();
    private readonly Dictionary<int, UIElement> _realized = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemRealizer"/> class and adds its spacer to <paramref name="host"/>.
    /// </summary>
    /// <param name="owner">The control that scrolls and owns the items.</param>
    /// <param name="host">The view that contains the spacer and the created elements.</param>
    /// <param name="create">Creates the element of the item at an index.</param>
    public ItemRealizer(Selector owner, View host, Func<int, UIElement> create)
    {
        _owner = owner;
        _host = host;
        _create = create;
        _spacer.Height = 0;
        host.Children.Add(_spacer);
        owner.ScrollChanged += (_, _) => Update();
        owner.SizeChanged += (_, _) => Update();
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
    /// Returns the created element of the item at <paramref name="index"/>, or <see langword="null"/> when it is not created.
    /// </summary>
    public UIElement? GetElement(int index)
    {
        return _realized.GetValueOrDefault(index);
    }

    /// <summary>
    /// Returns the index of the item whose created element is or contains <paramref name="element"/>, or <c>-1</c>.
    /// </summary>
    public int IndexOf(UIElement element)
    {
        for (UIElement? current = element; current is not null && !ReferenceEquals(current, _host); current = current.Parent)
        {
            foreach ((int index, UIElement realized) in _realized)
            {
                if (ReferenceEquals(realized, current))
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the top of the item at <paramref name="index"/> within the owner's scrolled content.
    /// </summary>
    public double GetItemTop(int index)
    {
        return GetHostTop() + (index * Pitch);
    }

    /// <summary>
    /// Removes every created element and creates the elements of the visible range again.
    /// </summary>
    public void Reset()
    {
        foreach (UIElement element in _realized.Values)
        {
            _host.Children.Remove(element);
        }

        _realized.Clear();
        Update();
    }

    /// <summary>
    /// Sizes the spacer, removes the elements that left the visible range, and creates the elements that entered it.
    /// </summary>
    public void Update()
    {
        int count = _owner.Items.Count;
        _spacer.Height = count == 0 ? 0 : (count * Pitch) - Math.Max(0, ItemSpacing);

        double viewport = _owner.ViewportHeight > 0 ? _owner.ViewportHeight : _owner.ActualHeight;
        double start = _owner.VerticalOffset - GetHostTop();
        int first = Math.Max(0, (int)Math.Floor(start / Pitch) - Overscan);
        int last = Math.Min(count - 1, (int)Math.Ceiling((start + viewport) / Pitch) + Overscan);

        List<int> departed = [];
        foreach (int index in _realized.Keys)
        {
            if (index < first || index > last)
            {
                departed.Add(index);
            }
        }

        foreach (int index in departed)
        {
            _host.Children.Remove(_realized[index]);
            _realized.Remove(index);
        }

        for (int index = first; index <= last; index++)
        {
            if (_realized.ContainsKey(index))
            {
                continue;
            }

            UIElement element = _create(index);
            element.Position = Position.Absolute;
            element.Top = index * Pitch;
            element.Left = 0;
            element.Right = 0;
            element.Height = ItemHeight;
            _realized[index] = element;
            _host.Children.Add(element);
        }
    }

    /// <summary>
    /// Returns the top of the host within the owner's scrolled content, which is zero when the owner hosts the items itself.
    /// </summary>
    private double GetHostTop()
    {
        if (ReferenceEquals(_host, _owner))
        {
            return 0;
        }

        return _host.Bounds.Y - _owner.Bounds.Y - _owner.ComputedStyle.BorderWidth + _owner.VerticalOffset;
    }
}
