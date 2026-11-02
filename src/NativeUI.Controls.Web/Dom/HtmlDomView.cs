using System.Collections.Specialized;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a view with a div element and owns the DOM elements for its children.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomView : HtmlDomElement, IScrollPeer
{
    private static int s_lastViewScopeId;

    private readonly View _view;
    private readonly List<HtmlDomElement> _children = [];
    private readonly Dictionary<Portal, HtmlDomElement> _portals = [];
    private string? _viewScopeId;
    private AdoptedStyleSheet? _styleSheet;

    internal HtmlDomView(View view, JSObject? element, string tagName = "div") : base(view, element, tagName)
    {
        _view = view;
    }

    /// <summary>
    /// Gets the scope identifier for this view's stylesheet, or null if no stylesheet is scoped to this view.
    /// </summary>
    internal string? ViewScopeId
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _viewScopeId;
        }
    }

    /// <summary>
    /// Updates this view and all its descendants. A view's stylesheet determines the tokens required by its entire subtree.
    /// </summary>
    internal override void UpdateTypeTokens()
    {
        base.UpdateTypeTokens();
        foreach (HtmlDomElement child in _children)
        {
            child.UpdateTypeTokens();
        }

        foreach (HtmlDomElement portal in _portals.Values)
        {
            portal.UpdateTypeTokens();
        }
    }

    /// <inheritdoc />
    void IScrollPeer.ScrollTo(double x, double y)
    {
        if (Element is JSObject element)
        {
            DomInterop.ScrollTo(element, x, y);
        }
    }

    /// <inheritdoc />
    ScrollMetrics? IScrollPeer.GetScrollMetrics()
    {
        if (Element is not JSObject element)
        {
            return null;
        }

        using JSObject metrics = DomInterop.GetScrollMetrics(element);
        return new ScrollMetrics(
            new Point(metrics.GetPropertyAsDouble("scrollLeft"), metrics.GetPropertyAsDouble("scrollTop")),
            new Size(metrics.GetPropertyAsDouble("scrollWidth"), metrics.GetPropertyAsDouble("scrollHeight")),
            new Size(metrics.GetPropertyAsDouble("clientWidth"), metrics.GetPropertyAsDouble("clientHeight")));
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (_view is ScrollView)
        {
            SetAttribute(StyleSheetCss.ScrollViewAttribute, string.Empty);
        }

        UpdateStyleSheet();
        SubscribeToChildren();
        SynchronizeChildren();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, View.StyleSheetProperty))
        {
            UpdateStyleSheet();
            UpdateTypeTokens();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _styleSheet?.Dispose();
        _styleSheet = null;
        UnsubscribeFromChildren();

        foreach (HtmlDomElement child in _children)
        {
            child.Dispose();
        }

        _children.Clear();

        foreach (HtmlDomElement portal in _portals.Values)
        {
            portal.Dispose();
        }

        _portals.Clear();
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

    /// <summary>
    /// Creates and removes the proxies of portal children, which mount themselves outside this element.
    /// </summary>
    private void SynchronizePortals()
    {
        foreach (Portal portal in _portals.Keys.ToArray())
        {
            if (!_view.Children.Contains(portal))
            {
                _portals.Remove(portal, out HtmlDomElement? removed);
                removed?.Dispose();
            }
        }

        foreach (UIElement child in _view.Children)
        {
            if (child is Portal portal && !_portals.ContainsKey(portal))
            {
                HtmlDomElement proxy = Create(portal, RootElement);
                _portals[portal] = proxy;
                proxy.Attach(this);
            }
        }
    }

    private void SynchronizeChildren()
    {
        SynchronizePortals();

        UIElement[] desiredChildren = [.. _view.Children.Where(static child => child is not Portal)];
        HashSet<UIElement> desiredSet = new(desiredChildren, ReferenceEqualityComparer.Instance);
        if (desiredSet.Count != desiredChildren.Length)
        {
            throw new InvalidOperationException("A view cannot mount the same child more than once.");
        }

        RemoveStaleChildren(desiredSet);

        for (int i = 0; i < desiredChildren.Length; i++)
        {
            UIElement source = desiredChildren[i];
            if (i < _children.Count && ReferenceEquals(_children[i].Source, source))
            {
                continue;
            }

            int previousIndex = _children.FindIndex(child => ReferenceEquals(child.Source, source));
            if (previousIndex < 0)
            {
                InsertNewChild(source, i);
            }
            else
            {
                MoveChild(previousIndex, i);
            }
        }
    }

    private void RemoveStaleChildren(HashSet<UIElement> desiredSet)
    {
        foreach (HtmlDomElement child in _children.ToArray())
        {
            if (!desiredSet.Contains(child.Source))
            {
                _children.Remove(child);
                child.Dispose();
            }
        }
    }

    private void InsertNewChild(UIElement source, int index)
    {
        HtmlDomElement child = Create(source, RootElement);
        HtmlDomElement? reference = GetChildAtOrDefault(index);
        _children.Insert(index, child);
        try
        {
            child.Attach(this);
            InsertBefore(child, reference);
        }
        catch
        {
            _children.Remove(child);
            child.Dispose();
            throw;
        }
    }

    private void MoveChild(int previousIndex, int index)
    {
        HtmlDomElement child = _children[previousIndex];
        _children.RemoveAt(previousIndex);
        HtmlDomElement? reference = GetChildAtOrDefault(index);
        _children.Insert(index, child);
        InsertBefore(child, reference);
    }

    private HtmlDomElement? GetChildAtOrDefault(int index)
    {
        return index < _children.Count ? _children[index] : null;
    }

    private void UpdateStyleSheet()
    {
        switch (_view.StyleSheet)
        {
            case CssStyleSheet compiled:
                ClearViewScope();
                ReplaceStyleSheet(compiled.Id, compiled.RawCss);
                break;
            case StyleSheet { Rules.Count: > 0 } sheet:
                string viewScopeId = EnsureViewScope();
                ReplaceStyleSheet($"NativeUI.ViewStyleSheet.{viewScopeId}", StyleSheetCss.WriteScoped(sheet, viewScopeId));
                break;
            default:
                ClearViewScope();
                _styleSheet?.Dispose();
                _styleSheet = null;
                break;
        }
    }

    /// <summary>
    /// Releases the previous stylesheet only after adopting the new one, which keeps a shared identifier adopted.
    /// </summary>
    private void ReplaceStyleSheet(string id, string css)
    {
        if (Element is null)
        {
            return;
        }

        AdoptedStyleSheet? previous = _styleSheet;
        _styleSheet = AdoptedStyleSheet.Adopt(id, css);
        previous?.Dispose();
    }

    private string EnsureViewScope()
    {
        if (_viewScopeId is null)
        {
            _viewScopeId = $"v{++s_lastViewScopeId}";
            SetAttribute(StyleSheetCss.ViewScopeAttribute, _viewScopeId);
        }

        return _viewScopeId;
    }

    private void ClearViewScope()
    {
        if (_viewScopeId is not null)
        {
            SetAttribute(StyleSheetCss.ViewScopeAttribute, null);
            _viewScopeId = null;
        }
    }
}
