using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Represents a composite view or container element that manages child <see cref="UIElement"/> instances,
/// their parent-child relationships, <c>DataContext</c> and style invalidation propagation, and disposal lifecycle.
/// </summary>
public class View : UIElement
{
    /// <summary>
    /// Identifies the <see cref="StyleSheet"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<StyleSheet?> StyleSheetProperty =
        BindableProperty.Create<View, StyleSheet?>(
            nameof(StyleSheet),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    private readonly ObservableCollection<UIElement> _children = [];
    private readonly HashSet<UIElement> _attachedChildren = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="View"/> class.
    /// </summary>
    public View()
    {
        _children.CollectionChanged += OnChildrenCollectionChanged;
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
    protected internal override void UpdateInheritedProperties()
    {
        base.UpdateInheritedProperties();
        foreach (UIElement child in _children)
        {
            child.UpdateInheritedProperties();
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
