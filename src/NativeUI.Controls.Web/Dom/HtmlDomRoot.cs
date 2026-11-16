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
    private KeyboardDevice? _keyboard;
    private OverlayManager? _overlayManager;
    private HtmlDomElement? _rootDom;
    private JSObject? _container;
    private JSObject? _rootElement;
    private AdoptedStyleSheet? _styleSheet;
    private IDisposable? _animationRoot;
    private int? _clockRequestId;
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
        using IDisposable? animationRoot = _animationRoot;
        _animationRoot = null;
        try
        {
            Application.Current.CurrentThemeChanged -= OnCurrentThemeChanged;
            AnimationClock.FrameRequested -= OnFrameRequested;
            if (_root is not null)
            {
                _root.VisualInvalidated -= OnVisualInvalidated;
            }

            CancelClockTick();

            // Disconnecting the native-ui element removes its pointer listeners.
            if (_rootElement is not null)
            {
                DomInterop.RemoveElement(_rootElement);
            }

            _styleSheet?.Dispose();
            _styleSheet = null;
            _pointerRouter.Reset();
            _overlayManager?.Dispose();
            _root?.DetachFromVisualTree(_root, _root.Parent);
            _rootDom?.Dispose();
        }
        finally
        {
            _keyboard = null;
            _overlayManager = null;
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
        if (_disposed || _rootDom is null || _root is null)
        {
            return;
        }

        UIElement root = _root;
        if (pointerEvent.Kind == PointerEventKind.Press && _keyboard is not null)
        {
            _keyboard.MostRecentInput = FocusState.Pointer;
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

        if (pointerEvent.Kind == PointerEventKind.Press)
        {
            _overlayManager?.HandlePointerPressed(hoveredElement);
        }

        if (!ReferenceEquals(_root, root) ||
            (hoveredElement is not null && !root.IsAncestorOf(hoveredElement)))
        {
            return;
        }

        UIElement? captured = InputManager.Current.GetPointerCapture(root, pointerEvent.PointerId);
        if (pointerEvent.Kind == PointerEventKind.Cancel)
        {
            captured?.ReleasePointerCapture(pointerEvent.PointerId);
            return;
        }

        UIElement? inputElement = captured ?? hoveredElement?.GetInputTarget();
        if (routedEvent is not null && inputElement is not null)
        {
            inputElement.RaiseEvent(routedEvent, new PointerEventArgs(
                pointerEvent.PointerId,
                pointerEvent.Position,
                pointerEvent.Buttons,
                pointerEvent.Modifiers,
                pointerEvent.ClickCount));
        }

        if (pointerEvent.Kind == PointerEventKind.Release)
        {
            InputManager.Current.GetPointerCapture(root, pointerEvent.PointerId)?.ReleasePointerCapture(pointerEvent.PointerId);
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
        if (ReferenceEquals(root.VisualRoot, root))
        {
            throw new InvalidOperationException("The element is already a hosted visual root.");
        }

        _root = root;
        try
        {
            _keyboard = InputManager.Current.PrimaryKeyboardDevice;
            _overlayManager = new OverlayManager(root);
            root.AttachToVisualTree(root);
            _animationRoot = AnimationClock.AttachRoot(root);
            if (useBrowserInterop)
            {
                root.InvalidateStyle();
                DomInterop.InjectStyleSheet(StyleSheetCss.BaseStyleSheet.Id, StyleSheetCss.BaseStyleSheet.RawCss);
                UpdateStyleSheet();
            }

            if (_container is not null && useBrowserInterop)
            {
                _rootElement = DomInterop.CreateElement("native-ui");
                DomInterop.SetAttribute(_rootElement, StyleSheetCss.ScopeAttribute, _scopeId.ToString(CultureInfo.InvariantCulture));
                DomInterop.SetAttribute(_rootElement, "data-platform", Application.Current.Platform);
                DomInterop.SetPointerDispatcher(_rootElement, OnPointerEvent);
                DomInterop.SetScrollDispatcher(_rootElement, OnScroll);
                DomInterop.SetInputDispatcher(_rootElement, OnInput);
                DomInterop.SetFormDispatcher(_rootElement, DispatchFormEvent);
                DomInterop.SetLabelDispatcher(_rootElement, DispatchLabelEvent);
                DomInterop.SetFocusDispatcher(_rootElement, OnFocus);
                DomInterop.SetKeyDispatcher(_rootElement, OnKeyEvent);
                DomInterop.SetCloseDispatcher(_rootElement, OnPortalClosed);
                DomInterop.SetResizeDispatcher(_rootElement, OnResize);
                AnimationClock.FrameRequested += OnFrameRequested;
                root.VisualInvalidated += OnVisualInvalidated;

                Application.Current.CurrentThemeChanged += OnCurrentThemeChanged;
                UpdateThemeAttribute();
            }

            _rootDom = HtmlDomElement.Create(root, _rootElement);
            _rootDom.Attach();
            if (_container is not null && _rootElement is not null && _rootDom.Element is JSObject element)
            {
                DomInterop.AppendChild(_rootElement, element);
                DomInterop.AppendChild(_container, _rootElement);
                ScheduleClockTick();
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
                Modifiers = (InputModifiers)pointerEvent.GetPropertyAsInt32("modifiers"),
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

    /// <summary>Routes native label activation without duplicating pointer activation.</summary>
    internal void DispatchLabelEvent(int nodeId)
    {
        if (!_disposed && HtmlDomElement.FindByNodeId(nodeId)?.Source is Label label && ReferenceEquals(label.VisualRoot, _root))
        {
            label.Activate();
        }
    }

    /// <summary>Routes native submission, reset, and non-pointer button activation through shared controls.</summary>
    internal void DispatchFormEvent(int formNodeId, int kind, int submitterNodeId)
    {
        if (_disposed || HtmlDomElement.FindByNodeId(formNodeId)?.Source is not Form form ||
            !ReferenceEquals(form.VisualRoot, _root))
        {
            return;
        }

        Button? submitter = HtmlDomElement.FindByNodeId(submitterNodeId)?.Source as Button;
        if (submitter is not null && !ReferenceEquals(Form.GetForm(submitter), form))
        {
            return;
        }

        if (kind == 1)
        {
            form.ResetForm();
        }
        else if (kind == 2 && submitter is not null)
        {
            submitter.PerformClick();
        }
        else if (kind == 0)
        {
            form.RequestSubmit(submitter);
        }
    }

    /// <summary>
    /// Records a browser focus change in the <see cref="KeyboardDevice"/>. The browser owns focus on Web. Focus that matches
    /// <c>:focus-visible</c> counts as keyboard focus, and other focus as pointer focus.
    /// </summary>
    private void OnFocus(int nodeId, bool isFocused, bool isVisible)
    {
        if (_keyboard is null || HtmlDomElement.FindByNodeId(nodeId)?.Source is not UIElement element ||
            !ReferenceEquals(element.VisualRoot, _root))
        {
            return;
        }

        if (isFocused)
        {
            _keyboard.HandleGotFocus(element, isVisible ? FocusState.Keyboard : FocusState.Pointer);
        }
        else
        {
            _keyboard.HandleLostFocus(element);
        }
    }

    /// <summary>
    /// Reads the <c>KeyEventEntry</c> forwarded by the <c>native-ui</c> element, raises the key event on the element that
    /// has focus or on the root, and releases its browser reference.
    /// </summary>
    /// <returns><see langword="true"/> when a handler handled the event, which prevents the browser's default action.</returns>
    private bool OnKeyEvent(JSObject keyEvent)
    {
        using (keyEvent)
        {
            if (_disposed || _root is null)
            {
                return false;
            }

            string key = keyEvent.GetPropertyAsString("key") ?? string.Empty;
            (Key logicalKey, string? text) = MapKey(key);
            InputModifiers modifiers = (InputModifiers)keyEvent.GetPropertyAsInt32("modifiers");
            bool isDown = keyEvent.GetPropertyAsBoolean("isDown");
            KeyEventArgs args = new(
                logicalKey,
                text,
                modifiers,
                keyEvent.GetPropertyAsBoolean("isRepeat"));
            UIElement target = HtmlDomElement.FindByNodeId(keyEvent.GetPropertyAsInt32("nodeId"))?.Source ?? _root;
            if (_keyboard is not null)
            {
                _keyboard.MostRecentInput = FocusState.Keyboard;
            }

            if (isDown && !target.IsEffectivelyEnabled)
            {
                return logicalKey != Key.Tab;
            }

            target.RaiseEvent(isDown ? UIElement.KeyDownEvent : UIElement.KeyUpEvent, args);
            if (args.Handled)
            {
                return true;
            }

            if (isDown && logicalKey == Key.Tab && _keyboard is not null)
            {
                UIElement focused = _keyboard.FocusedElement ?? target;
                if (KeyboardNavigation.HasActiveMode(focused))
                {
                    FocusNavigationDirection direction = modifiers.HasFlag(InputModifiers.Shift)
                        ? FocusNavigationDirection.Previous
                        : FocusNavigationDirection.Next;
                    _keyboard.MoveFocus(_root, direction);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Maps a DOM <c>KeyboardEvent.key</c> value to a logical key. A single character is <see cref="Key.Character"/>, and
    /// a named key without a <see cref="Key"/> member, such as a modifier key, is <see cref="Key.None"/>.
    /// </summary>
    private static (Key Key, string? Text) MapKey(string key)
    {
        if (key == " ")
        {
            return (Key.Space, key);
        }

        if (key.Length == 1 || (key.Length == 2 && char.IsSurrogatePair(key, 0)))
        {
            return (Key.Character, key);
        }

        return key switch
        {
            "Tab" => (Key.Tab, null),
            "Enter" => (Key.Enter, null),
            "Escape" => (Key.Escape, null),
            "Backspace" => (Key.Backspace, null),
            "Delete" => (Key.Delete, null),
            "Insert" => (Key.Insert, null),
            "ArrowLeft" => (Key.ArrowLeft, null),
            "ArrowUp" => (Key.ArrowUp, null),
            "ArrowRight" => (Key.ArrowRight, null),
            "ArrowDown" => (Key.ArrowDown, null),
            "Home" => (Key.Home, null),
            "End" => (Key.End, null),
            "PageUp" => (Key.PageUp, null),
            "PageDown" => (Key.PageDown, null),
            "ContextMenu" => (Key.ContextMenu, null),
            _ when Enum.TryParse(key, ignoreCase: false, out Key functionKey) && functionKey is >= Key.F1 and <= Key.F12 => (functionKey, null),
            _ => (Key.None, null)
        };
    }

    private static void OnPortalClosed(int nodeId)
    {
        if (HtmlDomElement.FindByNodeId(nodeId) is HtmlDomPortal portal)
        {
            portal.OnBrowserClosed();
        }
    }

    private static void OnResize(JSObject batch)
    {
        using (batch)
        {
            int length = batch.GetPropertyAsInt32("length");
            for (int i = 0; i < length; i++)
            {
                using JSObject? entry = batch.GetPropertyAsJSObject(i.ToString());
                if (entry is null)
                {
                    continue;
                }

                int nodeId = entry.GetPropertyAsInt32("nodeId");
                double width = entry.GetPropertyAsDouble("width");
                double height = entry.GetPropertyAsDouble("height");
                HtmlDomElement? node = HtmlDomElement.FindByNodeId(nodeId);
                if (node?.Source is UIElement element)
                {
                    Rect current = element.Bounds;
                    element.UpdateRenderedBounds(new Rect(current.X, current.Y, width, height));
                }
            }
        }
    }

    /// <summary>
    /// Requests the next screen refresh for frame targets. The browser runs CSS animations itself, so this root never
    /// attaches as a clock driver and style animations stay off.
    /// </summary>
    private void OnFrameRequested(UIElement root)
    {
        if (ReferenceEquals(root, _root))
        {
            ScheduleClockTick();
        }
    }

    private void ScheduleClockTick()
    {
        if (_disposed || _rootDom is null || _root is null || !AnimationClock.HasActiveAnimations(_root))
        {
            CancelClockTick();
            return;
        }

        _clockRequestId ??= DomInterop.RequestAnimationFrame(OnClockFrame);
    }

    private void CancelClockTick()
    {
        if (_clockRequestId is int requestId)
        {
            DomInterop.CancelAnimationFrame(requestId);
            _clockRequestId = null;
        }
    }

    private void OnVisualInvalidated(object? sender, EventArgs e)
    {
        ScheduleClockTick();
    }

    private void OnClockFrame()
    {
        _clockRequestId = null;
        if (_disposed || _rootDom is null || _root is null)
        {
            return;
        }

        try
        {
            AnimationClock.Tick(_root);
        }
        finally
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
