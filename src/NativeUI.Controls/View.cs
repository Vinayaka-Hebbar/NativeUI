using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Represents a composite view or container element that manages child <see cref="UIElement"/> instances,
/// their parent-child relationships, <c>DataContext</c> and style invalidation propagation, and disposal lifecycle.
/// </summary>
public class View : UIElement, IView, IScrollContainer
{
    /// <summary>
    /// Identifies the <see cref="StyleSheet"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<StyleSheet?> StyleSheetProperty =
        BindableProperty.Create<View, StyleSheet?>(
            nameof(StyleSheet),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    /// <summary>
    /// Identifies the <see cref="ClipToBounds"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> ClipToBoundsProperty =
        BindableProperty.Create<View, bool>(
            nameof(ClipToBounds),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    private readonly ObservableCollection<UIElement> _children = [];
    private readonly HashSet<UIElement> _attachedChildren = new(ReferenceEqualityComparer.Instance);
    private ScrollMetrics _scroll;

    /// <summary>
    /// Initializes a new instance of the <see cref="View"/> class.
    /// </summary>
    public View()
    {
        _children.CollectionChanged += OnChildrenCollectionChanged;
    }

    /// <summary>
    /// Occurs after the user or <see cref="ScrollTo"/> changes the scroll offset. On Web, the browser raises this asynchronously.
    /// A scroll offset that layout limits after content shrinks does not raise it.
    /// </summary>
    public event EventHandler? ScrollChanged;

    /// <summary>
    /// Gets the distance scrolled from the left of the content.
    /// </summary>
    public double HorizontalOffset => ReadScrollMetrics().Offset.X;

    /// <summary>
    /// Gets the distance scrolled from the top of the content.
    /// </summary>
    public double VerticalOffset => ReadScrollMetrics().Offset.Y;

    /// <summary>
    /// Gets the width of the content, including padding, as of the last layout.
    /// </summary>
    public double ExtentWidth => ReadScrollMetrics().Extent.Width;

    /// <summary>
    /// Gets the height of the content, including padding, as of the last layout.
    /// </summary>
    public double ExtentHeight => ReadScrollMetrics().Extent.Height;

    /// <summary>
    /// Gets the visible width, including padding, as of the last layout.
    /// </summary>
    public double ViewportWidth => ReadScrollMetrics().Viewport.Width;

    /// <summary>
    /// Gets the visible height, including padding, as of the last layout.
    /// </summary>
    public double ViewportHeight => ReadScrollMetrics().Viewport.Height;

    /// <summary>
    /// Scrolls to the given offset, limited to the scrollable range. Scrolling requires a non-visible <c>overflow</c>
    /// and a height smaller than the content.
    /// </summary>
    public void ScrollTo(double x, double y)
    {
        if (NativePeer is IScrollPeer peer)
        {
            peer.ScrollTo(x, y);
            return;
        }

        Point offset = LimitScrollOffset(new Point(x, y), _scroll);
        if (offset == _scroll.Offset)
        {
            return;
        }

        _scroll = _scroll with { Offset = offset };
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>
    /// Applies a user scroll on the axes that <c>overflow</c> allows, and returns whether the offset changed.
    /// </summary>
    internal bool TryScrollBy(double deltaX, double deltaY)
    {
        ComputedStyle style = ComputedStyle;
        Point previous = _scroll.Offset;
        ScrollTo(
            style.IsUserScrollableX ? previous.X + deltaX : previous.X,
            style.IsUserScrollableY ? previous.Y + deltaY : previous.Y);
        return _scroll.Offset != previous;
    }

    /// <summary>
    /// Raises <see cref="ScrollChanged"/> after the platform scrolled this view.
    /// </summary>
    internal void NotifyPlatformScrolled()
    {
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    Point IScrollContainer.UpdateScrollMetrics(Size extent, Size viewport)
    {
        _scroll = new ScrollMetrics(_scroll.Offset, extent, viewport);
        _scroll = _scroll with { Offset = LimitScrollOffset(_scroll.Offset, _scroll) };
        return _scroll.Offset;
    }

    private ScrollMetrics ReadScrollMetrics()
    {
        if (NativePeer is IScrollPeer peer && peer.GetScrollMetrics() is ScrollMetrics metrics)
        {
            _scroll = metrics;
        }

        return _scroll;
    }

    /// <summary>
    /// Limits an offset to the scrollable range. Before the first layout the range is unknown, and layout limits the offset later.
    /// </summary>
    private static Point LimitScrollOffset(Point offset, ScrollMetrics metrics)
    {
        if (metrics.Viewport == default)
        {
            return new Point(Math.Max(0, offset.X), Math.Max(0, offset.Y));
        }

        return new Point(
            Math.Clamp(offset.X, 0, Math.Max(0, metrics.Extent.Width - metrics.Viewport.Width)),
            Math.Clamp(offset.Y, 0, Math.Max(0, metrics.Extent.Height - metrics.Viewport.Height)));
    }

    /// <summary>
    /// Gets or sets a value indicating whether child elements are clipped to this view's bounds.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ClipToBounds
    {
        get => GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    /// <summary>
    /// Gets or sets the scoped <see cref="Styling.StyleSheet"/> that styles this view and its descendants.
    /// Changes to the <see cref="StyleSheet"/> reference trigger style recomputation; changes to its rules
    /// after assignment take effect on the next explicit recomputation.
    /// </summary>
    public StyleSheet? StyleSheet
    {
        get => GetValue(StyleSheetProperty);
        set => SetValue(StyleSheetProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "view";

    /// <summary>
    /// Gets the child <see cref="UIElement"/> collection contained within this view.
    /// </summary>
    public IList<UIElement> Children => _children;

    /// <inheritdoc />
    public override void RecomputeStyle()
    {
        base.RecomputeStyle();
        foreach (UIElement child in _children)
        {
            child.InvalidateStyle();
        }
    }

    /// <inheritdoc />
    protected override IReadOnlyList<ILayoutNode> GetLayoutChildren()
    {
        List<ILayoutNode>? flowChildren = null;
        for (int i = 0; i < _children.Count; i++)
        {
            UIElement child = _children[i];
            if (child is Portal && flowChildren is null)
            {
                flowChildren = [.. _children.Take(i)];
            }
            else if (child is not Portal)
            {
                flowChildren?.Add(child);
            }
        }

        return flowChildren ?? (IReadOnlyList<ILayoutNode>)_children;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        foreach (UIElement child in _children)
        {
            child.SetInheritedDataContext(args.NewValue);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Disposing an open dialog that this view hosts as its owner removes it from the collection.
            foreach (UIElement child in _children.ToArray())
            {
                child.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            InvalidateVisual();
            return;
        }

        // Reset notifications omit the old items, so retain the attached children for detachment.
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (UIElement child in _attachedChildren)
            {
                if (ReferenceEquals(child.Parent, this))
                {
                    child.Parent = null;
                }
            }

            _attachedChildren.Clear();
            foreach (UIElement child in _children)
            {
                _attachedChildren.Add(child);
                child.Parent = this;
            }

            InvalidateVisual();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (object item in e.OldItems)
            {
                if (item is UIElement oldChild && ReferenceEquals(oldChild.Parent, this))
                {
                    _attachedChildren.Remove(oldChild);
                    oldChild.Parent = null;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (object item in e.NewItems)
            {
                if (item is UIElement newChild)
                {
                    _attachedChildren.Add(newChild);
                    newChild.Parent = this;
                }
            }
        }

        InvalidateVisual();
    }
}
