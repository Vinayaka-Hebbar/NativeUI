using System.Collections.Specialized;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a view with a div element and owns the DOM elements for its children.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomView : HtmlDomElement
{
    private readonly View _view;
    private readonly List<HtmlDomElement> _children = [];

    internal HtmlDomView(View view, JSObject? element) : base(view, element, "div")
    {
        _view = view;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SubscribeToChildren();
        SynchronizeChildren();
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        UnsubscribeFromChildren();

        foreach (HtmlDomElement child in _children)
        {
            child.Dispose();
        }

        _children.Clear();
        base.OnDisposing();
    }

    private void SubscribeToChildren()
    {
        if (_view.Children is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnChildrenChanged;
        }
    }

    private void UnsubscribeFromChildren()
    {
        if (_view.Children is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnChildrenChanged;
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        SynchronizeChildren();
    }

    private void SynchronizeChildren()
    {
        UIElement[] desiredChildren = [.. _view.Children];
        HashSet<UIElement> desiredSet = new(desiredChildren, ReferenceEqualityComparer.Instance);
        if (desiredSet.Count != desiredChildren.Length)
        {
            throw new InvalidOperationException("A view cannot mount the same child more than once.");
        }

        foreach (HtmlDomElement child in _children.ToArray())
        {
            if (!desiredSet.Contains(child.Source))
            {
                child.Dispose();
                _children.Remove(child);
            }
        }

        for (int i = 0; i < desiredChildren.Length; i++)
        {
            UIElement source = desiredChildren[i];
            if (i < _children.Count && ReferenceEquals(_children[i].Source, source))
            {
                continue;
            }

            int previousIndex = _children.FindIndex(child => ReferenceEquals(child.Source, source));
            bool created = previousIndex < 0;
            HtmlDomElement child;
            if (created)
            {
                child = Create(source, Element is not null);
            }
            else
            {
                child = _children[previousIndex];
                _children.RemoveAt(previousIndex);
            }

            HtmlDomElement? reference = i < _children.Count ? _children[i] : null;
            _children.Insert(i, child);
            try
            {
                if (created)
                {
                    child.Attach(this);
                }

                InsertBefore(child, reference);
            }
            catch
            {
                if (created)
                {
                    _children.Remove(child);
                    child.Dispose();
                }

                throw;
            }
        }
    }
}
