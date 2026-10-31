using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Css;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a shared control tree into the browser DOM and manages its lifetime.
/// Mount and disposal operations must run on the browser's UI thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class HtmlDomRoot : IDisposable
{
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private UIElement? _root;
    private HtmlDomElement? _rootDom;
    private JSObject? _container;
    private bool _disposed;

    /// <summary>
    /// Creates a DOM root subscribed to hot reload notifications.
    /// </summary>
    public HtmlDomRoot()
    {
        HotReloadManager.HotReloaded += OnHotReloaded;
    }

    /// <summary>
    /// Gets a task that completes when this root is disposed.
    /// </summary>
    public Task Stopped => _stopped.Task;

    /// <summary>
    /// Mounts the root into a browser container. This type owns DOM references, but does not dispose shared controls.
    /// </summary>
    public void Mount(UIElement root, string containerSelector = "#app")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerSelector);
        Unmount();
        _container = DomInterop.QuerySelector(containerSelector)
            ?? throw new InvalidOperationException($"No DOM container matches '{containerSelector}'.");
        MountCore(root, useBrowserInterop: true);
    }

    /// <summary>
    /// Releases DOM elements and subscriptions while leaving the shared controls available for reuse.
    /// </summary>
    public void Unmount()
    {
        try
        {
            _rootDom?.Dispose();
        }
        finally
        {
            _rootDom = null;
            _root = null;
            _container?.Dispose();
            _container = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        HotReloadManager.HotReloaded -= OnHotReloaded;
        try
        {
            Unmount();
        }
        finally
        {
            _stopped.TrySetResult();
        }
    }

    /// <summary>
    /// Mounts the root without creating browser DOM elements.
    /// </summary>
    internal void MountHeadless(UIElement root)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(root);
        Unmount();
        MountCore(root, useBrowserInterop: false);
    }

    private void MountCore(UIElement root, bool useBrowserInterop)
    {
        _root = root;
        try
        {
            if (useBrowserInterop)
            {
                UpdateStyleSheet();
            }

            _rootDom = HtmlDomElement.Create(root, useBrowserInterop);
            _rootDom.Attach();
            if (_container is not null && _rootDom.Element is JSObject element)
            {
                DomInterop.AppendChild(_container, element);
            }
        }
        catch
        {
            Unmount();
            throw;
        }
    }

    private void OnHotReloaded(Type[]? updatedTypes)
    {
        if (_disposed || _root is null)
        {
            return;
        }

        _root.RecomputeStyle();
        if (_container is not null)
        {
            UpdateStyleSheet();
        }
    }

    private void UpdateStyleSheet()
    {
        if (_root is not null && CssStyleRegistry.TryGet(_root.StyleSheetId, out CssStyleSheet? sheet) && sheet is not null)
        {
            DomInterop.InjectStyleSheet(sheet.Id, sheet.RawCss);
        }
    }
}
