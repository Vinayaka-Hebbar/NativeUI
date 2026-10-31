using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>
/// Records a single DOM mutation performed by <see cref="WebHost"/> for diagnostics and automated testing.
/// </summary>
/// <param name="Operation">The DOM operation name (for example, <c>CreateElement</c> or <c>SetTextContent</c>).</param>
/// <param name="TargetTag">The HTML tag of the target element.</param>
/// <param name="Detail">The updated value or attribute detail.</param>
public readonly record struct DomMutationRecord(string Operation, string TargetTag, string Detail);

/// <summary>
/// Hosts a <see cref="UIElement"/> hierarchy in the browser HTML DOM via .NET WebAssembly or an interactive
/// local Web server, creating real <c>&lt;div&gt;</c> and <c>&lt;span&gt;</c> elements and performing targeted
/// incremental DOM updates on property changes.
/// </summary>
public sealed class WebHost : IDisposable
{
    private readonly List<DomMutationRecord> _mutationLog = [];
    private readonly List<string> _browserCommandLog = [];
    private readonly List<StreamWriter> _sseClients = [];
    private readonly Dictionary<UIElement, int> _elementIds = [];
    private readonly List<UIElement> _mountedElements = [];
    private readonly bool _useBrowserInterop;
    private UIElement? _rootElement;
    private int _nextNodeId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebHost"/> class.
    /// </summary>
    /// <param name="useBrowserInterop">
    /// When <see langword="true"/> and running on the <c>browser</c> OS platform, invokes <see cref="DomInterop"/>;
    /// otherwise records DOM mutations in C# for headless testing or local browser dev-server streaming.
    /// </param>
    public WebHost(bool useBrowserInterop = true)
    {
        _useBrowserInterop = useBrowserInterop && OperatingSystem.IsBrowser();
    }

    /// <summary>
    /// Gets the chronological log of DOM operations performed by this host.
    /// </summary>
    public IReadOnlyList<DomMutationRecord> MutationLog => _mutationLog;

    /// <summary>
    /// Mounts the specified <paramref name="root"/> view into the browser DOM container matching <paramref name="containerSelector"/>.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public void Mount(UIElement root, string containerSelector = "#app")
    {
        ArgumentNullException.ThrowIfNull(root);
        JSObject? container = _useBrowserInterop ? DomInterop.QuerySelector(containerSelector) : null;
        MountCore(root, container);
    }

    /// <summary>
    /// Mounts the specified <paramref name="root"/> view in headless recording mode for automated testing.
    /// </summary>
    public void MountHeadless(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        MountCore(root, containerDomObject: null);
    }

    /// <summary>
    /// Mounts <paramref name="root"/> and runs the Web host. On Browser WebAssembly, mounts directly into <c>#app</c>;
    /// on desktop during development/debugging, starts an interactive local HTTP server that renders the real HTML DOM
    /// and <c>document.adoptedStyleSheets</c> in the browser and streams live incremental updates.
    /// </summary>
    /// <param name="root">The root <see cref="UIElement"/> to host.</param>
    /// <param name="port">The local HTTP port to listen on when running the development server.</param>
    public void Run(UIElement root, int port = 5173)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (OperatingSystem.IsBrowser())
        {
            Mount(root, "#app");
            return;
        }

        MountCore(root, containerDomObject: null);
        RunDevelopmentServer(port);
    }

    /// <summary>
    /// Clears the recorded <see cref="MutationLog"/> so subsequent incremental updates can be asserted in isolation.
    /// </summary>
    public void ClearMutationLog()
    {
        _mutationLog.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (UIElement element in _mountedElements)
        {
            element.PropertyChanged -= OnElementPropertyChanged;
            if (element.NativePeer is IDisposable disposablePeer)
            {
                disposablePeer.Dispose();
            }

            element.NativePeer = null;
        }

        _mountedElements.Clear();
        _elementIds.Clear();
        _rootElement = null;
    }

    private void MountCore(UIElement root, JSObject? containerDomObject)
    {
        _rootElement = root;
        if (CssStyleRegistry.TryGet(root.GetType().Name, out CssStyleSheet? sheet) && sheet is not null)
        {
            InjectStyleSheet(sheet);
        }

        (JSObject? rootDom, int rootId) = BuildElementSubtree(root);
        if (_useBrowserInterop && OperatingSystem.IsBrowser() && containerDomObject is not null && rootDom is not null)
        {
            DomInterop.AppendChild(containerDomObject, rootDom);
        }

        EmitBrowserCommand(new { op = "mountRoot", id = rootId });
    }

    private (JSObject? DomNode, int NodeId) BuildElementSubtree(UIElement element)
    {
        string htmlTag = GetHtmlTagName(element);
        JSObject? domNode = null;
        int nodeId = ++_nextNodeId;
        _elementIds[element] = nodeId;

        if (_useBrowserInterop && OperatingSystem.IsBrowser())
        {
            domNode = DomInterop.CreateElement(htmlTag);
            element.NativePeer = domNode;
        }

        _mutationLog.Add(new DomMutationRecord("CreateElement", htmlTag, element.Class));
        EmitBrowserCommand(new { op = "createElement", id = nodeId, tag = htmlTag });

        if (!string.IsNullOrEmpty(element.Id))
        {
            SetAttribute(domNode, nodeId, htmlTag, "id", element.Id);
        }

        if (!string.IsNullOrEmpty(element.Class))
        {
            SetClassName(domNode, nodeId, htmlTag, element.Class);
        }

        if (!element.IsVisible)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "display", "none");
        }

        if (element.Style is Style inlineStyle)
        {
            ApplyInlineStyle(domNode, nodeId, htmlTag, inlineStyle);
        }

        if (element is Text textControl)
        {
            SetAttribute(domNode, nodeId, htmlTag, "role", "text");
            if (!string.IsNullOrEmpty(textControl.AriaLabel))
            {
                SetAttribute(domNode, nodeId, htmlTag, "aria-label", textControl.AriaLabel);
            }

            SetTextContent(domNode, nodeId, htmlTag, textControl.Value);
        }

        element.PropertyChanged += OnElementPropertyChanged;
        _mountedElements.Add(element);

        if (element is View containerView)
        {
            foreach (UIElement child in containerView.Children)
            {
                (JSObject? childDom, int childId) = BuildElementSubtree(child);
                if (_useBrowserInterop && OperatingSystem.IsBrowser() && domNode is not null && childDom is not null)
                {
                    DomInterop.AppendChild(domNode, childDom);
                }

                _mutationLog.Add(new DomMutationRecord("AppendChild", htmlTag, GetHtmlTagName(child)));
                EmitBrowserCommand(new { op = "appendChild", parentId = nodeId, childId });
            }
        }

        return (domNode, nodeId);
    }

    private void OnElementPropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
    {
        if (_disposed || sender is not UIElement element)
        {
            return;
        }

        string htmlTag = GetHtmlTagName(element);
        JSObject? domNode = element.NativePeer as JSObject;
        int nodeId = _elementIds.GetValueOrDefault(element);

        if (ReferenceEquals(e.Property, Text.ValueProperty) && element is Text textControl)
        {
            SetTextContent(domNode, nodeId, htmlTag, textControl.Value);
        }
        else if (ReferenceEquals(e.Property, UIElement.ClassProperty))
        {
            SetClassName(domNode, nodeId, htmlTag, element.Class);
        }
        else if (ReferenceEquals(e.Property, UIElement.IdProperty) && element.Id is not null)
        {
            SetAttribute(domNode, nodeId, htmlTag, "id", element.Id);
        }
        else if (ReferenceEquals(e.Property, UIElement.IsVisibleProperty))
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "display", element.IsVisible ? string.Empty : "none");
        }
        else if (ReferenceEquals(e.Property, UIElement.StyleProperty) && element.Style is Style updatedStyle)
        {
            ApplyInlineStyle(domNode, nodeId, htmlTag, updatedStyle);
        }
        else if (ReferenceEquals(e.Property, Text.AriaLabelProperty) && element is Text ariaText && ariaText.AriaLabel is not null)
        {
            SetAttribute(domNode, nodeId, htmlTag, "aria-label", ariaText.AriaLabel);
        }
    }

    private void ApplyInlineStyle(JSObject? domNode, int nodeId, string htmlTag, Style style)
    {
        if (style.Color is ThemeValue<Color> fg)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "color", FormatThemeColor(fg));
        }

        if (style.BackgroundColor is ThemeValue<Color> bg)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "background-color", FormatThemeColor(bg));
        }

        if (style.FontSize is double fontSize)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "font-size", string.Create(CultureInfo.InvariantCulture, $"{fontSize:0.##}px"));
        }

        if (style.FontWeight is FontWeight fontWeight)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "font-weight", ((int)fontWeight).ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(style.FontFamily))
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "font-family", style.FontFamily);
        }

        if (style.TextAlign is TextAlign textAlign)
        {
            string alignText = textAlign switch
            {
                TextAlign.Center => "center",
                TextAlign.Right => "right",
                _ => "left"
            };
            SetStyleProperty(domNode, nodeId, htmlTag, "text-align", alignText);
        }

        if (style.Padding is Thickness padding)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "padding", FormatThickness(padding));
        }

        if (style.Margin is Thickness margin)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "margin", FormatThickness(margin));
        }

        if (style.BorderRadius is double radius)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "border-radius", string.Create(CultureInfo.InvariantCulture, $"{radius:0.##}px"));
        }

        if (style.Opacity is double opacity)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "opacity", opacity.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (style.Width is double width)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "width", string.Create(CultureInfo.InvariantCulture, $"{width:0.##}px"));
        }

        if (style.Height is double height)
        {
            SetStyleProperty(domNode, nodeId, htmlTag, "height", string.Create(CultureInfo.InvariantCulture, $"{height:0.##}px"));
        }
    }

    private static string FormatThemeColor(ThemeValue<Color> colorValue)
    {
        if (colorValue.IsThemeKey)
        {
            return colorValue.HasValue
                ? $"var({colorValue.Key}, {colorValue.Value.ToCssString()})"
                : $"var({colorValue.Key})";
        }

        return colorValue.Value.ToCssString();
    }

    private static string FormatThickness(Thickness t)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{t.Top:0.##}px {t.Right:0.##}px {t.Bottom:0.##}px {t.Left:0.##}px");
    }

    private void InjectStyleSheet(CssStyleSheet sheet)
    {
        if (_useBrowserInterop && OperatingSystem.IsBrowser())
        {
            DomInterop.InjectStyleSheet(sheet.Id, sheet.RawCss);
        }

        _mutationLog.Add(new DomMutationRecord("InjectStyleSheet", "style", sheet.Id));
        EmitBrowserCommand(new { op = "injectStyleSheet", id = sheet.Id, css = sheet.RawCss });
    }

    private void SetTextContent(JSObject? domNode, int nodeId, string tag, string text)
    {
        if (_useBrowserInterop && OperatingSystem.IsBrowser() && domNode is not null)
        {
            DomInterop.SetTextContent(domNode, text);
        }

        _mutationLog.Add(new DomMutationRecord("SetTextContent", tag, text));
        EmitBrowserCommand(new { op = "setTextContent", id = nodeId, text });
    }

    private void SetClassName(JSObject? domNode, int nodeId, string tag, string className)
    {
        if (_useBrowserInterop && OperatingSystem.IsBrowser() && domNode is not null)
        {
            DomInterop.SetClassName(domNode, className);
        }

        _mutationLog.Add(new DomMutationRecord("SetClassName", tag, className));
        EmitBrowserCommand(new { op = "setClassName", id = nodeId, className });
    }

    private void SetAttribute(JSObject? domNode, int nodeId, string tag, string name, string value)
    {
        if (_useBrowserInterop && OperatingSystem.IsBrowser() && domNode is not null)
        {
            DomInterop.SetAttribute(domNode, name, value);
        }

        _mutationLog.Add(new DomMutationRecord("SetAttribute", tag, $"{name}={value}"));
        EmitBrowserCommand(new { op = "setAttribute", id = nodeId, name, value });
    }

    private void SetStyleProperty(JSObject? domNode, int nodeId, string tag, string prop, string value)
    {
        if (_useBrowserInterop && OperatingSystem.IsBrowser() && domNode is not null)
        {
            DomInterop.SetStyleProperty(domNode, prop, value);
        }

        _mutationLog.Add(new DomMutationRecord("SetStyleProperty", tag, $"{prop}:{value}"));
        EmitBrowserCommand(new { op = "setStyleProperty", id = nodeId, prop, value });
    }

    private void EmitBrowserCommand<T>(T payload)
    {
        string json = JsonSerializer.Serialize(payload);
        lock (_browserCommandLog)
        {
            _browserCommandLog.Add(json);
        }

        lock (_sseClients)
        {
            for (int i = _sseClients.Count - 1; i >= 0; i--)
            {
                try
                {
                    _sseClients[i].Write($"data: {json}\n\n");
                    _sseClients[i].Flush();
                }
                catch
                {
                    _sseClients.RemoveAt(i);
                }
            }
        }
    }

    private void RunDevelopmentServer(int port)
    {
        string url = $"http://localhost:{port}/";
        using HttpListener listener = new();
        listener.Prefixes.Add(url);
        listener.Start();

        Console.WriteLine($"[NativeUI.Web] Listening on {url}");
        TryOpenBrowser(url);

        while (!_disposed && listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }

            _ = Task.Run(() => HandleHttpRequest(ctx));
        }
    }

    private void HandleHttpRequest(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url?.AbsolutePath ?? "/";
        if (path == "/api/events")
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            StreamWriter writer = new(ctx.Response.OutputStream, new UTF8Encoding(false));

            string[] snapshot;
            lock (_browserCommandLog)
            {
                snapshot = [.. _browserCommandLog];
            }

            foreach (string cmd in snapshot)
            {
                writer.Write($"data: {cmd}\n\n");
            }

            writer.Flush();
            lock (_sseClients)
            {
                _sseClients.Add(writer);
            }

            return;
        }

        string relativeAsset = path.TrimStart('/');
        if (string.IsNullOrEmpty(relativeAsset))
        {
            relativeAsset = "index.html";
        }

        string contentType = Path.GetExtension(relativeAsset).ToLowerInvariant() switch
        {
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            _ => "text/html; charset=utf-8"
        };

        ServeStaticFile(ctx.Response, relativeAsset, contentType);
    }

    private static void ServeStaticFile(HttpListenerResponse response, string relativeAsset, string contentType)
    {
        string outputAssetPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativeAsset);
        string workingDirAssetPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeAsset);
        string? resolvedPath = File.Exists(outputAssetPath)
            ? outputAssetPath
            : (File.Exists(workingDirAssetPath) ? workingDirAssetPath : null);

        if (resolvedPath is null)
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        response.ContentType = contentType;
        byte[] bytes = File.ReadAllBytes(resolvedPath);
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
        response.OutputStream.Close();
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
            }
            else if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
            }
        }
        catch
        {
            // Ignore browser launch errors in headless environments.
        }
    }

    private static string GetHtmlTagName(UIElement element)
    {
        return element switch
        {
            Text => "span",
            View => "div",
            _ => "div"
        };
    }
}
