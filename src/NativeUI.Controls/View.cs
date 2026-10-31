using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a composite view or container element that manages child <see cref="UIElement"/> instances,
/// their parent-child relationships, <c>DataContext</c> and style invalidation propagation, and disposal lifecycle.
/// </summary>
public class View : UIElement
{
    private readonly ObservableCollection<UIElement> _children = [];
    private readonly HashSet<UIElement> _attachedChildren = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="View"/> class.
    /// </summary>
    public View()
    {
        _children.CollectionChanged += OnChildrenCollectionChanged;
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
        return _children;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(object? newDataContext)
    {
        base.OnDataContextChanged(newDataContext);
        foreach (UIElement child in _children)
        {
            child.SetInheritedDataContext(newDataContext);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (UIElement child in _children)
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
