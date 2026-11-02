using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Media.Animation;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Owns a mounted element's DOM reference, property synchronization, and browser operations.
/// Operations must run on the browser's UI thread.
/// </summary>
[SupportedOSPlatform("browser")]
public class HtmlDomElement : INativeHandle, IAnimationPeer
{
    private static readonly Dictionary<int, HtmlDomElement> s_attachedElements = [];
    private static int s_lastNodeId;

    private readonly JSObject? _element;
    private HtmlDomElement? _parent;
    private int _nodeId;
    private Style? _inlineStyle;
    private bool _attached;
    private HtmlResizeObserver? _resizeObserver;
    private string _className = string.Empty;
    private string? _text;
    private bool _disposed;

    /// <summary>
    /// Gets the inline <c>display</c> value while the element is visible, or null to leave it to stylesheets.
    /// </summary>
    protected virtual string? VisibleDisplay => null;

    /// <summary>
    /// Gets a value indicating whether this proxy has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

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
    /// Gets the owning native-ui element, including while this element is detached.
    /// </summary>
    protected JSObject? RootElement { get; private set; }

    /// <summary>
    /// Gets the DOM element of the parent proxy, or null when there is none.
    /// </summary>
    internal JSObject? ParentElement => _parent?._element;

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

    internal static HtmlDomElement Create(UIElement source, JSObject? rootElement)
    {
        if (source.NativeHandle is not null)
        {
            throw new InvalidOperationException("The control already owns a native handle.");
        }

        string tagName = HtmlTagMapping.GetTag(source);
        JSObject? element = rootElement is not null ? DomInterop.CreateElement(tagName) : null;
        HtmlDomElement proxy = source switch
        {
            CanvasView canvasView => new HtmlDomCanvas(canvasView, element),
            Button button => new HtmlDomButton(button, element),
            Input input => new HtmlDomInput(input, element),
            ProgressBar progressBar => new HtmlDomProgressBar(progressBar, element),
            Dialog dialog => new HtmlDomDialog(dialog, element, tagName),
            Portal portal => new HtmlDomPortal(portal, element, tagName),
            StackPanel stackPanel => new HtmlDomStackPanel(stackPanel, element),
            Grid grid => new HtmlDomGrid(grid, element),
            Text text => new HtmlDomText(text, element),
            View view => new HtmlDomView(view, element),
            _ => new HtmlDomElement(source, element, tagName)
        };

        proxy.RootElement = rootElement;
        return proxy;
    }

    internal void Attach(HtmlDomElement? parent = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_attached)
        {
            throw new InvalidOperationException("The DOM element is already attached.");
        }

        if (Source.NativeHandle is not null)
        {
            throw new InvalidOperationException("The control already owns a native handle.");
        }

        _parent = parent;
        _nodeId = ++s_lastNodeId;
        s_attachedElements[_nodeId] = this;
        if (_element is not null)
        {
            DomInterop.SetNodeId(_element, _nodeId);
        }

        _attached = true;
        Source.NativeHandle = this;

        Source.PropertyChanged += OnElementPropertyChanged;
        ReplaceInlineStyle();
        Initialize();
    }

    /// <summary>
    /// Sets <see cref="StyleSheetCss.TypeAttribute"/> to the tokens required by the stylesheets applying to this element,
    /// or removes the attribute when they require none.
    /// </summary>
    internal virtual void UpdateTypeTokens()
    {
        SetAttribute(StyleSheetCss.TypeAttribute, StyleSheetCss.GetTypeTokens(Source));
    }

    /// <inheritdoc />
    void IAnimationPeer.Animate(AnimationSpec spec, Animation animation)
    {
        if (_element is null)
        {
            animation.Complete();
            return;
        }

        JSObject? handle = null;
        handle = DomInterop.Animate(
            _element,
            spec.Name,
            spec.DurationMilliseconds,
            spec.Easing.ToCssString(),
            spec.Iterations,
            () =>
            {
                handle?.Dispose();
                animation.Complete();
            });

        if (handle is not null)
        {
            animation.SetCancelHandler(() => DomInterop.CancelAnimation(handle));
        }
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
            _inlineStyle.PropertyChanged -= OnInlineStyleChanged;
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
        SetClassName(Source.Class.ToString());
        UpdateTypeTokens();
        UpdateVisibility();
        _parent?.ApplyAttachedProperties(this);
    }

    /// <summary>
    /// Applies the attached properties this element's panel declares to <paramref name="child"/>, when the child
    /// attaches and whenever one of its attached properties changes. Elements that are not panels declare none.
    /// </summary>
    internal virtual void ApplyAttachedProperties(HtmlDomElement child)
    {
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
            SetClassName(Source.Class.ToString());
        }
        else if (ReferenceEquals(args.Property, UIElement.IsVisibleProperty))
        {
            UpdateVisibility();
        }
        else if (ReferenceEquals(args.Property, UIElement.StyleProperty))
        {
            ReplaceInlineStyle();
        }
        else if (args.Property.IsAttached)
        {
            _parent?.ApplyAttachedProperties(this);
        }
    }

    private void OnElementPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (_attached)
        {
            OnPropertyChanged(args);
        }
    }

    /// <summary>
    /// Applies <see cref="UIElement.IsVisible"/> to the inline <c>display</c> value.
    /// </summary>
    protected void UpdateVisibility()
    {
        SetStyleProperty("display", Source.IsVisible ? VisibleDisplay : "none");
    }

    private void ReplaceInlineStyle()
    {
        if (_inlineStyle is not null)
        {
            _inlineStyle.PropertyChanged -= OnInlineStyleChanged;
        }

        _inlineStyle = Source.Style;
        if (_inlineStyle is not null)
        {
            _inlineStyle.PropertyChanged += OnInlineStyleChanged;
        }

        UpdateInlineStyle();
    }

    private void OnInlineStyleChanged(object? sender, BindablePropertyChangedEventArgs args)
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
            _resizeObserver?.Dispose();
            _resizeObserver = null;

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

            if (ReferenceEquals(Source.NativeHandle, this))
            {
                Source.NativeHandle = null;
            }

            _parent = null;
        }
    }

    /// <inheritdoc />
    void INativeHandle.OnAttachView(UIElement element)
    {
        UIElement.BoundsProperty.StartObservation(element);
    }

    /// <inheritdoc />
    void INativeHandle.OnDetachView(UIElement element)
    {
        UIElement.BoundsProperty.StopObservation(element);
    }

    /// <inheritdoc />
    ResizeObserver? INativeHandle.CreateSizeObserver(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_element is null || RootElement is null)
        {
            return null;
        }

        _resizeObserver ??= new HtmlResizeObserver(RootElement, _element);
        _resizeObserver.Observe();
        return _resizeObserver;
    }

    /// <inheritdoc cref="INativeHandle.GetBounds" />
    public Rect? GetBounds()
    {
        if (_element is not null && _attached)
        {
            using JSObject bounds = DomInterop.GetBounds(_element);
            return new Rect(
                bounds.GetPropertyAsDouble("x"),
                bounds.GetPropertyAsDouble("y"),
                bounds.GetPropertyAsDouble("width"),
                bounds.GetPropertyAsDouble("height"));
        }

        return null;
    }

    internal bool IsObservingSize => Source.IsSizeObserved;
}

internal sealed class HtmlResizeObserver : ResizeObserver
{
    private readonly JSObject _rootElement;
    private readonly JSObject _element;
    private bool _observing;

    public HtmlResizeObserver(JSObject rootElement, JSObject element)
    {
        _rootElement = rootElement;
        _element = element;
    }

    public override void Observe()
    {
        if (_observing)
        {
            return;
        }

        if (OperatingSystem.IsBrowser())
        {
            DomInterop.ObserveResize(_rootElement, _element);
        }

        _observing = true;
    }

    public override void Unobserve()
    {
        if (!_observing)
        {
            return;
        }

        if (OperatingSystem.IsBrowser())
        {
            DomInterop.UnobserveResize(_rootElement, _element);
        }

        _observing = false;
    }

    public override void Dispose()
    {
        Unobserve();
    }
}
