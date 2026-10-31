using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Owns a mounted element's DOM reference, property synchronization, and browser operations.
/// Operations must run on the browser's UI thread.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomElement : INativePeer
{
    private static readonly Dictionary<int, HtmlDomElement> s_attachedElements = [];
    private static int s_lastNodeId;

    private readonly JSObject? _element;
    private HtmlDomElement? _parent;
    private int _nodeId;
    private Style? _inlineStyle;
    private bool _attached;
    private string _className = string.Empty;
    private string? _text;
    private bool _disposed;

    internal HtmlDomElement(UIElement source, JSObject? element, string tagName)
    {
        Source = source;
        _element = element;
        TagName = tagName;
    }

    /// <summary>
    /// Gets the HTML tag represented by this proxy, such as <c>span</c> or <c>div</c>.
    /// </summary>
    public string TagName { get; }

    /// <summary>
    /// Gets the current CSS class names.
    /// </summary>
    public string ClassName
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _element is not null ? DomInterop.GetClassName(_element) : _className;
        }
    }

    /// <summary>
    /// Gets the current text content.
    /// </summary>
    public string TextContent
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _element is not null ? DomInterop.GetTextContent(_element) : _text ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets an attribute value, or null when the attribute is absent.
    /// </summary>
    public string? GetAttribute(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_element is null)
        {
            return null;
        }

        return DomInterop.GetAttribute(_element, name);
    }

    /// <summary>
    /// Gets an inline CSS declaration, or null when it is absent.
    /// </summary>
    public string? GetStyleProperty(string propertyName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (_element is null)
        {
            return null;
        }

        string value = DomInterop.GetStyleProperty(_element, propertyName);
        return value.Length == 0 ? null : value;
    }

    internal JSObject? Element => _element;

    /// <summary>
    /// Gets the identifier reported for pointer events targeting this element, or zero when it is not attached.
    /// </summary>
    internal int NodeId => _nodeId;

    /// <summary>
    /// Finds an attached element by the identifier reported for a pointer event.
    /// </summary>
    internal static HtmlDomElement? FindByNodeId(int nodeId)
    {
        return s_attachedElements.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// Gets the shared control whose properties this proxy synchronizes.
    /// </summary>
    protected internal UIElement Source { get; }

    internal static HtmlDomElement Create(UIElement source, bool useBrowserInterop)
    {
        if (source.NativePeer is not null)
        {
            throw new InvalidOperationException("The control already owns a native peer.");
        }

        string tagName = HtmlTagMapping.GetTag(source);
        JSObject? element = useBrowserInterop && OperatingSystem.IsBrowser() ? DomInterop.CreateElement(tagName) : null;
        return source switch
        {
            Text text => new HtmlDomText(text, element),
            View view => new HtmlDomView(view, element),
            _ => new HtmlDomElement(source, element, tagName)
        };
    }

    internal void Attach(HtmlDomElement? parent = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_attached)
        {
            throw new InvalidOperationException("The DOM element is already attached.");
        }

        if (Source.NativePeer is not null)
        {
            throw new InvalidOperationException("The control already owns a native peer.");
        }

        _parent = parent;
        Source.NativePeer = this;
        _nodeId = ++s_lastNodeId;
        s_attachedElements[_nodeId] = this;
        if (_element is not null)
        {
            DomInterop.SetNodeId(_element, _nodeId);
        }

        _attached = true;

        Source.PropertyChanged += OnElementPropertyChanged;
        ReplaceInlineStyle();
        Initialize();
    }

    /// <summary>
    /// Sets an attribute, or removes it when <paramref name="value"/> is null.
    /// </summary>
    public void SetAttribute(string name, string? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_element is null)
        {
            return;
        }

        if (value is null)
        {
            DomInterop.RemoveAttribute(_element, name);
        }
        else
        {
            DomInterop.SetAttribute(_element, name, value);
        }
    }

    /// <summary>
    /// Replaces the space-separated CSS class names.
    /// </summary>
    public void SetClassName(string className)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(className);
        if (_element is null)
        {
            _className = className;
            return;
        }

        DomInterop.SetClassName(_element, className);
        _className = className;
    }

    /// <summary>
    /// Sets an inline CSS property, or removes it when <paramref name="value"/> is null.
    /// </summary>
    public void SetStyleProperty(string propertyName, string? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (_element is null)
        {
            return;
        }

        if (value is null)
        {
            DomInterop.RemoveStyleProperty(_element, propertyName);
        }
        else
        {
            DomInterop.SetStyleProperty(_element, propertyName, value);
        }
    }

    /// <summary>
    /// Replaces the text content of a leaf DOM element.
    /// </summary>
    public void SetTextContent(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (_element is null)
        {
            _text = text;
            return;
        }

        DomInterop.SetTextContent(_element, text);
        _text = text;
    }

    /// <summary>
    /// Appends a child element, moving it from its previous DOM position if necessary.
    /// </summary>
    public void AppendChild(HtmlDomElement child)
    {
        InsertBefore(child, null);
    }

    /// <summary>
    /// Inserts or moves a child before another child, or appends it when <paramref name="reference"/> is null.
    /// </summary>
    public void InsertBefore(HtmlDomElement child, HtmlDomElement? reference)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);
        ObjectDisposedException.ThrowIf(child._disposed, child);
        if (reference is not null)
        {
            ObjectDisposedException.ThrowIf(reference._disposed, reference);
        }

        if (reference is not null && !ReferenceEquals(reference._parent, this))
        {
            throw new InvalidOperationException("The reference element is not a child of this element.");
        }

        for (HtmlDomElement? ancestor = this; ancestor is not null; ancestor = ancestor._parent)
        {
            if (ReferenceEquals(ancestor, child))
            {
                throw new InvalidOperationException("A DOM element cannot contain itself or an ancestor.");
            }
        }

        if (_element is not null && child._element is not null)
        {
            DomInterop.InsertBefore(_element, child._element, reference?._element);
        }

        child._parent = this;

    }

    /// <summary>
    /// Removes a child element from this element without disposing its proxy.
    /// </summary>
    public void RemoveChild(HtmlDomElement child)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);
        ObjectDisposedException.ThrowIf(child._disposed, child);
        if (!ReferenceEquals(child._parent, this))
        {
            throw new InvalidOperationException("The element is not a child of this element.");
        }

        if (_element is not null && child._element is not null)
        {
            DomInterop.RemoveChild(_element, child._element);
        }

        child._parent = null;

    }

    /// <summary>
    /// Unsubscribes property changes, detaches the DOM element, and releases the owned browser reference.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _attached = false;
        Source.PropertyChanged -= OnElementPropertyChanged;
        if (_inlineStyle is not null)
        {
            _inlineStyle.Changed -= OnInlineStyleChanged;
            _inlineStyle = null;
        }

        try
        {
            OnDisposing();
        }
        finally
        {
            ReleaseElement();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases control-specific subscriptions and mounted children before the DOM element is detached.
    /// </summary>
    protected virtual void OnDisposing()
    {
    }

    /// <summary>
    /// Applies initial control values after the DOM element is attached.
    /// </summary>
    protected virtual void Initialize()
    {
        SetAttribute("id", Source.Id);
        SetClassName(Source.Class);
        if (Source.StyleResolver is StyleSheet styleSheet)
        {
            SetAttribute(StyleSheetCss.TypeAttribute, StyleSheetCss.GetTypeTokens(styleSheet, Source));
        }

        UpdateVisibility();
    }

    /// <summary>
    /// Applies a common property change; derived proxies also handle their control-specific properties.
    /// </summary>
    protected virtual void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, UIElement.IdProperty))
        {
            SetAttribute("id", Source.Id);
        }
        else if (ReferenceEquals(args.Property, UIElement.ClassProperty))
        {
            SetClassName(Source.Class);
        }
        else if (ReferenceEquals(args.Property, UIElement.IsVisibleProperty))
        {
            UpdateVisibility();
        }
        else if (ReferenceEquals(args.Property, UIElement.StyleProperty))
        {
            ReplaceInlineStyle();
        }
    }

    private void OnElementPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (_attached)
        {
            OnPropertyChanged(args);
        }
    }

    private void UpdateVisibility()
    {
        SetStyleProperty("display", Source.IsVisible ? null : "none");
    }

    private void ReplaceInlineStyle()
    {
        if (_inlineStyle is not null)
        {
            _inlineStyle.Changed -= OnInlineStyleChanged;
        }

        _inlineStyle = Source.Style;
        if (_inlineStyle is not null)
        {
            _inlineStyle.Changed += OnInlineStyleChanged;
        }

        UpdateInlineStyle();
    }

    private void OnInlineStyleChanged(object? sender, EventArgs args)
    {
        if (_attached)
        {
            UpdateInlineStyle();
        }
    }

    private void UpdateInlineStyle()
    {
        foreach ((string property, string? value) in StyleCssDeclarations.Get(_inlineStyle))
        {
            SetStyleProperty(property, value);
        }
    }

    private void ReleaseElement()
    {
        try
        {
            if (_element is not null)
            {
                DomInterop.RemoveElement(_element);
            }

        }
        finally
        {
            _element?.Dispose();
            s_attachedElements.Remove(_nodeId);
            _nodeId = 0;

            if (ReferenceEquals(Source.NativePeer, this))
            {
                Source.NativePeer = null;
            }

            _parent = null;
        }
    }

}
