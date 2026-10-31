using System.Collections.Specialized;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a view with a div element and owns the DOM elements for its children.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomView : HtmlDomElement
{
    private static int s_lastViewScopeId;

    private readonly View _view;
    private readonly List<HtmlDomElement> _children = [];
    private string? _viewScopeId;
    private string? _injectedSheetId;

    internal HtmlDomView(View view, JSObject? element) : base(view, element, "div")
    {
        _view = view;
    }

    /// <summary>
    /// Gets the scope identifier for this view's stylesheet, or null if no stylesheet is scoped to this view.
    /// </summary>
    public string? ViewScopeId
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _viewScopeId;
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateScopedStyleSheet();
        SubscribeToChildren();
        SynchronizeChildren();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, View.StyleSheetProperty))
        {
            UpdateScopedStyleSheet();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        RemoveScopedStyleSheet();
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

    private void UpdateScopedStyleSheet()
    {
        if (_view.StyleSheet is StyleSheet sheet && sheet.Rules.Count > 0)
        {
            _viewScopeId ??= $"v{Interlocked.Increment(ref s_lastViewScopeId)}";
            SetAttribute(StyleSheetCss.ViewScopeAttribute, _viewScopeId);

            _injectedSheetId ??= $"NativeUI.ViewStyleSheet.{_viewScopeId}";
            string css = StyleSheetCss.WriteScoped(sheet, _viewScopeId);
            if (OperatingSystem.IsBrowser())
            {
                DomInterop.InjectStyleSheet(_injectedSheetId, css);
            }
        }
        else
        {
            RemoveScopedStyleSheet();
        }
    }

    private void RemoveScopedStyleSheet()
    {
        if (_injectedSheetId is not null)
        {
            if (OperatingSystem.IsBrowser())
            {
                DomInterop.RemoveStyleSheet(_injectedSheetId);
            }

            _injectedSheetId = null;
        }

        if (_viewScopeId is not null)
        {
            if (!IsDisposed)
            {
                SetAttribute(StyleSheetCss.ViewScopeAttribute, null);
            }

            _viewScopeId = null;
        }
    }
}
