using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a shared control tree into the browser DOM and manages its lifetime.
/// Mount and disposal operations must run on the browser's UI thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class HtmlDomRoot : IDisposable
{
    private const string ThemeAttribute = "data-theme";

    private static int s_lastScopeId;

    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PointerInputRouter _pointerRouter = new();
    private readonly int _scopeId = ++s_lastScopeId;
    private UIElement? _root;
    private HtmlDomElement? _rootDom;
    private JSObject? _container;
    private JSObject? _rootElement;
    private AdoptedStyleSheet? _styleSheet;
    private bool _isClockTickScheduled;
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
            Application.Current.CurrentThemeChanged -= OnCurrentThemeChanged;
            AnimationClock.AnimationStarted -= ScheduleClockTick;

            // Disconnecting the native-ui element removes its pointer listeners.
            if (_rootElement is not null)
            {
                DomInterop.RemoveElement(_rootElement);
            }

            _styleSheet?.Dispose();
            _styleSheet = null;
            _pointerRouter.Reset();
            _rootDom?.Dispose();
        }
        finally
        {
            _rootDom = null;
            _root = null;
            _rootElement?.Dispose();
            _rootElement = null;
            _container?.Dispose();
            _container = null;
        }
    }

    /// <summary>
    /// Routes one native pointer event to the shared hover state and routed events.
    /// Browser selection and other default actions are unaffected by <see cref="RoutedEventArgs.Handled"/>.
    /// </summary>
    internal void DispatchPointerEvent(in PointerEventData pointerEvent)
    {
        if (_disposed || _rootDom is null)
        {
            return;
        }

        UIElement? hoveredElement = pointerEvent.Kind is PointerEventKind.Leave or PointerEventKind.Cancel
            ? null
            : HtmlDomElement.FindByNodeId(pointerEvent.NodeId)?.Source;
        _pointerRouter.UpdateHover(
            hoveredElement,
            pointerEvent.PointerId,
            pointerEvent.Position,
            pointerEvent.Buttons,
            pointerEvent.Modifiers);

        RoutedEvent<PointerEventArgs>? routedEvent = pointerEvent.Kind switch
        {
            PointerEventKind.Press => UIElement.PointerPressedEvent,
            PointerEventKind.Release => UIElement.PointerReleasedEvent,
            PointerEventKind.Move => UIElement.PointerMovedEvent,
            _ => null
        };

        if (routedEvent is not null)
        {
            hoveredElement?.RaiseEvent(routedEvent, new PointerEventArgs(
                pointerEvent.PointerId,
                pointerEvent.Position,
                pointerEvent.Buttons,
                pointerEvent.Modifiers,
                pointerEvent.ClickCount));
        }

        if (pointerEvent.Kind == PointerEventKind.Press)
        {
            Portal.DismissForPress(hoveredElement);
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
                root.InvalidateStyle();
                DomInterop.InjectStyleSheet(StyleSheetCss.BaseStyleSheet.Id, StyleSheetCss.BaseStyleSheet.RawCss);
                UpdateStyleSheet();
            }

            _rootDom = HtmlDomElement.Create(root, useBrowserInterop);
            _rootDom.Attach();
            if (_container is not null && _rootDom.Element is JSObject element)
            {
                _rootElement = DomInterop.CreateElement("native-ui");
                DomInterop.SetAttribute(_rootElement, StyleSheetCss.ScopeAttribute, _scopeId.ToString(CultureInfo.InvariantCulture));
                DomInterop.SetAttribute(_rootElement, "data-platform", Application.Current.Platform);
                DomInterop.SetPointerDispatcher(_rootElement, OnPointerEvent);
                DomInterop.SetScrollDispatcher(_rootElement, OnScroll);
                DomInterop.SetInputDispatcher(_rootElement, OnInput);
                DomInterop.SetFocusDispatcher(_rootElement, OnFocus);
                DomInterop.SetCloseDispatcher(_rootElement, OnPortalClosed);
                AnimationClock.AnimationStarted += ScheduleClockTick;
                if (AnimationClock.HasActiveAnimations)
                {
                    ScheduleClockTick();
                }

                Application.Current.CurrentThemeChanged += OnCurrentThemeChanged;
                UpdateThemeAttribute();
                DomInterop.AppendChild(_rootElement, element);
                DomInterop.AppendChild(_container, _rootElement);
            }
        }
        catch
        {
            Unmount();
            throw;
        }
    }

    /// <summary>
    /// Reads the <c>PointerEventEntry</c> forwarded by the <c>native-ui</c> element and releases its browser reference.
    /// </summary>
    private void OnPointerEvent(JSObject pointerEvent)
    {
        using (pointerEvent)
        {
            DispatchPointerEvent(new PointerEventData
            {
                Kind = (PointerEventKind)pointerEvent.GetPropertyAsInt32("kind"),
                NodeId = pointerEvent.GetPropertyAsInt32("nodeId"),
                PointerId = pointerEvent.GetPropertyAsInt32("pointerId"),
                Position = new Point(pointerEvent.GetPropertyAsDouble("x"), pointerEvent.GetPropertyAsDouble("y")),
                Buttons = (PointerButtons)pointerEvent.GetPropertyAsInt32("buttons"),
                Modifiers = (PointerModifiers)pointerEvent.GetPropertyAsInt32("modifiers"),
                ClickCount = pointerEvent.GetPropertyAsInt32("clickCount")
            });
        }
    }

    private static void OnScroll(int nodeId)
    {
        if (HtmlDomElement.FindByNodeId(nodeId)?.Source is View view)
        {
            view.NotifyPlatformScrolled();
        }
    }

    private static void OnInput(int nodeId, string value)
    {
        if (HtmlDomElement.FindByNodeId(nodeId) is HtmlDomInput input)
        {
            input.OnBrowserInput(value);
        }
    }

    private static void OnFocus(int nodeId, bool isFocused)
    {
        if (HtmlDomElement.FindByNodeId(nodeId) is HtmlDomInput input)
        {
            input.OnBrowserFocus(isFocused);
        }
    }

    private static void OnPortalClosed(int nodeId)
    {
        if (HtmlDomElement.FindByNodeId(nodeId) is HtmlDomPortal portal)
        {
            portal.OnBrowserClosed();
        }
    }

    /// <summary>
    /// Requests the next screen refresh for frame targets. The browser runs CSS animations itself, so this root never
    /// attaches as a clock driver and style animations stay off.
    /// </summary>
    private void ScheduleClockTick()
    {
        if (!_isClockTickScheduled)
        {
            _isClockTickScheduled = true;
            DomInterop.RequestAnimationFrame(OnClockFrame);
        }
    }

    private void OnClockFrame()
    {
        _isClockTickScheduled = false;
        if (_disposed || _rootDom is null)
        {
            return;
        }

        AnimationClock.Tick();
        if (AnimationClock.HasActiveAnimations)
        {
            ScheduleClockTick();
        }
    }

    private void OnCurrentThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _root?.InvalidateStyle();
        UpdateThemeAttribute();
    }

    /// <summary>
    /// Sets <c>data-theme</c> on the <c>native-ui</c> element to <see cref="Application.CurrentTheme"/>.
    /// Compiled color-scheme rules and the base stylesheet select this attribute.
    /// </summary>
    private void UpdateThemeAttribute()
    {
        if (_rootElement is not null)
        {
            DomInterop.SetAttribute(_rootElement, ThemeAttribute, Application.Current.CurrentTheme == Theme.Dark ? "dark" : "light");
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

    /// <summary>
    /// Releases the previous stylesheet only after adopting the current one, which keeps a shared identifier adopted.
    /// </summary>
    private void UpdateStyleSheet()
    {
        AdoptedStyleSheet? previous = _styleSheet;
        _styleSheet = AdoptStyleSheet();
        previous?.Dispose();
    }

    private AdoptedStyleSheet? AdoptStyleSheet()
    {
        if (_root?.StyleResolver is StyleSheet styleSheet)
        {
            return AdoptedStyleSheet.Adopt($"NativeUI.StyleSheet.{_scopeId}", StyleSheetCss.Write(styleSheet, _scopeId));
        }

        // HtmlDomView adopts the stylesheet of a root view.
        if (_root is View { StyleSheet: not null })
        {
            return null;
        }

        return _root is not null && CssStyleRegistry.TryGet(_root.StyleSheetId, out CssStyleSheet? sheet) && sheet is not null
            ? AdoptedStyleSheet.Adopt(sheet.Id, sheet.RawCss)
            : null;
    }
}
