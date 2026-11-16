using System.Text;
using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Represents a native macOS desktop window that hosts a <see cref="UIElement"/> hierarchy,
/// follows <see cref="Application.CurrentTheme"/>, propagates <see cref="StyleResolver"/>, routes mouse/keyboard text selection
/// and clipboard operations, and renders its visual tree using <see cref="VisualTreeRenderer"/> and <see cref="SkiaSurface"/>.
/// </summary>
public sealed class Window : IDisposable, IRenderHost
{
    private const ulong StyleMaskTitled = 1UL << 0;
    private const ulong StyleMaskClosable = 1UL << 1;
    private const ulong StyleMaskMiniaturizable = 1UL << 2;
    private const ulong StyleMaskResizable = 1UL << 3;
    private const ulong BackingStoreBuffered = 2UL;
    private const long MousePointerId = 1;
    private const nint TitleVisibilityHidden = 1;
    private const ulong ChildWindowOrderedAbove = 1;
    private const string EscapeKey = "\u001b";

    // Layout of a window sized to its content is limited by this size, and not by the window's current size.
    private static readonly Size s_contentMeasureViewport = new(4096, 4096);

    private readonly VisualTreeRenderer _renderer = new(SkiaTextShaper.Shared);
    private readonly PointerInputRouter _pointerRouter = new();
    private readonly IClipboard _clipboard;
    private readonly Dictionary<Input, NativeTextField> _textFields = [];
    private readonly Dictionary<Dialog, DialogWindow> _dialogWindows = [];
    private SkiaSurface? _surface;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private nint _displayLink;
    private bool _isFrameScheduled;
    private bool _isAnimationDriver;
    private Dialog? _hostedDialog;
    private nint _inputBlocker;
    private UIElement? _content;
    private KeyboardDevice? _keyboard;
    private OverlayManager? _overlayManager;
    private IDisposable? _animationRoot;
    private IStyleResolver? _styleResolver;
    private Color? _background;
    private nint _nsWindow;
    private nint _nsImageView;
    private string _title = "NativeUI";
    private Text? _activeSelectionText;
    private LayoutBox? _activeSelectionBox;
    private int _selectionAnchorPosition;
    private bool _isDraggingSelection;
    private bool _overlayCapturesPointer;
    private ScrollbarDrag? _scrollbarDrag;
    private Point? _pointerPosition;
    private PointerButtons _pointerButtons;
    private InputModifiers _pointerModifiers;
    private int _frameBatchDepth;
    private bool _isFrameDirty;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Window"/> class.
    /// </summary>
    /// <param name="clipboard">Optional clipboard service; defaults to <see cref="MacOSClipboard.Instance"/>.</param>
    public Window(IClipboard? clipboard = null)
    {
        _clipboard = clipboard ?? MacOSClipboard.Instance;
        Application.Current.CurrentThemeChanged += OnCurrentThemeChanged;
        HotReloadManager.HotReloaded += OnHotReloaded;
    }

    /// <summary>
    /// Gets the <see cref="IClipboard"/> associated with this window.
    /// </summary>
    public IClipboard Clipboard => _clipboard;

    /// <summary>
    /// Gets or sets the window title displayed in the native macOS title bar.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            if (_nsWindow != nint.Zero && OperatingSystem.IsMacOS())
            {
                nint nsTitle = AppKitInterop.CreateNSString(_title);
                AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("setTitle:"), nsTitle);
            }
        }
    }

    /// <summary>
    /// Gets or sets the logical width of the window content area.
    /// </summary>
    public double Width { get; set; } = 640;

    /// <summary>
    /// Gets or sets the logical height of the window content area.
    /// </summary>
    public double Height { get; set; } = 420;

    /// <summary>
    /// Gets or sets the DPI backing scale factor (for example, 2.0f on Retina displays).
    /// </summary>
    public float ScaleFactor { get; set; } = 2.0f;

    /// <summary>
    /// Gets or sets the <see cref="IStyleResolver"/> propagated to the window's content tree.
    /// </summary>
    public IStyleResolver? StyleResolver
    {
        get => _styleResolver ?? Media.StyleResolver.Default;
        set
        {
            if (ReferenceEquals(_styleResolver, value))
            {
                return;
            }

            _styleResolver = value;
            SyncContentStyle();
            InvalidateFrame();
        }
    }

    /// <summary>
    /// Gets or sets the optional window surface clear color.
    /// </summary>
    public Color? Background
    {
        get => _background;
        set
        {
            _background = value;
            InvalidateFrame();
        }
    }

    /// <summary>
    /// Gets the total number of frames rendered by this window.
    /// </summary>
    public int RenderCount { get; private set; }

    /// <summary>
    /// Gets the most recently computed <see cref="LayoutBox"/> tree from the last render pass.
    /// </summary>
    public LayoutBox? LastLayout { get; private set; }

    /// <inheritdoc />
    public IList<IRenderOverlay> Overlays => _renderer.Overlays;

    /// <inheritdoc />
    public void InvalidateRender()
    {
        InvalidateFrame();
    }

    /// <summary>
    /// Gets the native Objective-C <c>NSWindow*</c> pointer when shown on macOS, or <see cref="nint.Zero"/> in headless mode.
    /// </summary>
    public nint NativeHandle => _nsWindow;

    /// <summary>
    /// Gets a value indicating whether this window requests the I-beam cursor, either over selectable text or during a selection drag.
    /// </summary>
    public bool IsTextCursorActive { get; private set; }

    /// <summary>
    /// Gets the overlay manager owned by this window for its current content root.
    /// </summary>
    internal OverlayManager? OverlayManager => _overlayManager;

    /// <summary>
    /// Gets or sets the root <see cref="UIElement"/> content displayed inside this window.
    /// </summary>
    public UIElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            if (value is not null && ReferenceEquals(value.VisualRoot, value))
            {
                throw new InvalidOperationException("The element is already a hosted visual root.");
            }

            BeginFrameBatch();
            try
            {
                ClearPointerState();
            }
            finally
            {
                _isFrameDirty = false;
                _frameBatchDepth--;
            }

            if (_content is not null)
            {
                _content.VisualInvalidated -= OnContentVisualInvalidated;
                _content.RemoveHandler(Hyperlink.RequestNavigateEvent, OnRequestNavigate);
            }

            using IDisposable? previousAnimationRoot = _animationRoot;
            _animationRoot = null;
            RemoveTextFields();
            RemoveDialogWindows();
            _overlayManager?.Dispose();
            _overlayManager = null;
            _content?.DetachFromVisualTree(_content, _content.Parent);
            DetachKeyboard();
            _activeSelectionText = null;
            _activeSelectionBox = null;
            _content = value;
            if (_content is not null)
            {
                _keyboard = InputManager.Current.PrimaryKeyboardDevice;
                _overlayManager = new OverlayManager(_content);
                _content.AttachToVisualTree(_content);
                _keyboard.FocusChanged += OnFocusChanged;
                _animationRoot = AnimationClock.AttachRoot(_content);
                SyncContentStyle();
                _content.VisualInvalidated += OnContentVisualInvalidated;
                _content.AddHandler(Hyperlink.RequestNavigateEvent, OnRequestNavigate);
            }

            InvalidateFrame();
        }
    }

    /// <summary>
    /// Creates and presents the native macOS <c>NSWindow</c> on screen and renders the initial frame.
    /// </summary>
    public void Show()
    {
        if (OperatingSystem.IsMacOS() && _nsWindow == nint.Zero)
        {
            CreateNativeWindow(StyleMaskTitled | StyleMaskClosable | StyleMaskMiniaturizable | StyleMaskResizable);
            UpdateFrame();
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("center"));
            AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("makeKeyAndOrderFront:"), nint.Zero);
        }

        InvalidateFrame();
    }

    /// <summary>
    /// Creates the <c>NSWindow</c> and its rendered image view, without showing the window.
    /// </summary>
    private void CreateNativeWindow(ulong styleMask)
    {
        AppKitInterop.EnsureFrameworksLoaded();

        // Attaching first lets the style sync below start the animations of elements styled before the window existed.
        if (AppKitInterop.IsDisplayLinkSupported())
        {
            AnimationClock.AttachDriver();
            _isAnimationDriver = true;
            AnimationClock.FrameRequested += OnFrameRequested;
        }

        SyncContentStyle();

        AppKitInterop.CGRect rect = new(120, 120, Width, Height);

        nint windowClass = AppKitInterop.GetClass("NSWindow");
        nint allocWin = AppKitInterop.SendMessage(windowClass, AppKitInterop.GetSelector("alloc"));
        _nsWindow = AppKitInterop.SendMessageWindowInit(
            allocWin,
            AppKitInterop.GetSelector("initWithContentRect:styleMask:backing:defer:"),
            rect,
            styleMask,
            BackingStoreBuffered,
            false);

        nint nsTitle = AppKitInterop.CreateNSString(_title);
        AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("setTitle:"), nsTitle);

        nint imageViewClass = AppKitInterop.GetInteractiveImageViewClass();
        nint allocImgView = AppKitInterop.SendMessage(imageViewClass, AppKitInterop.GetSelector("alloc"));
        _nsImageView = AppKitInterop.SendMessageViewInit(
            allocImgView,
            AppKitInterop.GetSelector("initWithFrame:"),
            new AppKitInterop.CGRect(0, 0, Width, Height));

        AppKitInterop.RegisterWindowView(_nsImageView, this);
        AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("setContentView:"), _nsImageView);
        AppKitInterop.InstallPointerTracking(_nsWindow, _nsImageView);
        AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("setAutorecalculatesKeyViewLoop:"), true);
        _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("makeFirstResponder:"), _nsImageView);
        _displayLink = AppKitInterop.CreateDisplayLink(_nsImageView);
        if (_content is not null && AnimationClock.HasActiveAnimations(_content))
        {
            OnFrameRequested(_content);
        }
    }

    /// <summary>
    /// Renders the frame scheduled for this screen refresh.
    /// </summary>
    internal void HandleDisplayRefresh()
    {
        // A tick invalidates only the windows whose content changed, so other windows draw nothing.
        if (_content is not null)
        {
            AnimationClock.Tick(_content);
        }

        if (_isFrameScheduled)
        {
            _isFrameScheduled = false;
            UpdateFrame();
        }

        bool hasActiveAnimations = _content is not null && AnimationClock.HasActiveAnimations(_content);
        if (!_isFrameScheduled && !hasActiveAnimations && _displayLink != nint.Zero)
        {
            AppKitInterop.SendMessageVoid(_displayLink, AppKitInterop.GetSelector("setPaused:"), true);
        }
    }

    private void OnFrameRequested(UIElement root)
    {
        if (_displayLink != nint.Zero && ReferenceEquals(root, _content))
        {
            AppKitInterop.SendMessageVoid(_displayLink, AppKitInterop.GetSelector("setPaused:"), false);
        }
    }

    /// <summary>
    /// Handles a pointer press at <paramref name="viewportPoint"/> (in logical window coordinates)
    /// by routing pointer events and beginning character selection or performing double-click word selection.
    /// </summary>
    public void HandlePointerPressed(
        Point viewportPoint,
        int clickCount = 1,
        InputModifiers modifiers = InputModifiers.None)
    {
        HandlePointerPressed(viewportPoint, PointerButtons.Primary, clickCount, modifiers);
    }

    /// <summary>
    /// Routes a press with its native pointer buttons in logical window coordinates.
    /// </summary>
    /// <param name="viewportPoint">The pointer position relative to the window content.</param>
    /// <param name="buttons">The buttons held during the press.</param>
    /// <param name="clickCount">The number of consecutive clicks.</param>
    /// <param name="modifiers">The modifier keys held during the press.</param>
    public void HandlePointerPressed(
        Point viewportPoint,
        PointerButtons buttons,
        int clickCount = 1,
        InputModifiers modifiers = InputModifiers.None)
    {
        if (_content is null || LastLayout is null)
        {
            return;
        }

        UIElement root = _content;
        OverlayManager? overlayManager = _overlayManager;
        KeyboardDevice? keyboard = _keyboard;
        BeginFrameBatch();
        try
        {
            StorePointerState(viewportPoint, buttons, modifiers);
            if (buttons.HasFlag(PointerButtons.Primary) && DispatchOverlayPointer(OverlayPointerAction.Pressed, viewportPoint))
            {
                overlayManager?.HandlePointerPressed(null);
                _overlayCapturesPointer = true;
                UpdateHoverState(null);
                return;
            }

            if (!ReferenceEquals(_content, root))
            {
                return;
            }

            UIElement? hoveredElement = HitTest(viewportPoint, out ScrollbarGeometry? scrollbar);
            UpdateHoverState(hoveredElement);
            overlayManager?.HandlePointerPressed(hoveredElement);
            UIElement? inputElement = hoveredElement?.GetInputTarget();
            if (!ReferenceEquals(_content, root) ||
                (hoveredElement is not null && inputElement is null) ||
                (inputElement is not null && !IsCurrentHitTarget(root, inputElement)))
            {
                return;
            }

            // A press outside an Input's native field focuses that field through the keyboard device.
            keyboard?.HandlePointerPressed(root, inputElement);
            if (inputElement is null || !inputElement.IsEffectivelyEnabled || !IsCurrentHitTarget(root, inputElement))
            {
                return;
            }

            if (buttons.HasFlag(PointerButtons.Primary) && scrollbar is ScrollbarGeometry hitScrollbar &&
                ReferenceEquals(inputElement, hitScrollbar.View))
            {
                BeginScrollbarDrag(hitScrollbar, viewportPoint);
                return;
            }

            inputElement.RaiseEvent(
                UIElement.PointerPressedEvent,
                new PointerEventArgs(MousePointerId, viewportPoint, buttons, modifiers, clickCount));

            if (buttons.HasFlag(PointerButtons.Primary))
            {
                BeginSelection(root, inputElement, viewportPoint, clickCount);
            }
        }
        finally
        {
            UpdateCursor();
            EndFrameBatch();
        }
    }

    /// <summary>
    /// Starts dragging the thumb of <paramref name="scrollbar"/>. A press on the track outside the thumb first scrolls so
    /// that the thumb is centered on the pointer, then drags from there.
    /// </summary>
    private void BeginScrollbarDrag(ScrollbarGeometry scrollbar, Point viewportPoint)
    {
        View view = scrollbar.View;
        bool isVertical = scrollbar.Orientation == Orientation.Vertical;
        double pointer = isVertical ? viewportPoint.Y : viewportPoint.X;
        double trackStart = isVertical ? scrollbar.Track.Y : scrollbar.Track.X;
        double trackLength = isVertical ? scrollbar.Track.Height : scrollbar.Track.Width;
        double thumbStart = isVertical ? scrollbar.Thumb.Y : scrollbar.Thumb.X;
        double thumbLength = isVertical ? scrollbar.Thumb.Height : scrollbar.Thumb.Width;
        double range = isVertical ? view.ExtentHeight - view.ViewportHeight : view.ExtentWidth - view.ViewportWidth;
        double travel = trackLength - thumbLength;
        double offsetPerPixel = travel > 0 ? range / travel : 0;

        if (pointer < thumbStart || pointer > thumbStart + thumbLength)
        {
            ScrollAlong(view, isVertical, (pointer - trackStart - (thumbLength / 2)) * offsetPerPixel);
        }

        double startOffset = isVertical ? view.VerticalOffset : view.HorizontalOffset;
        _scrollbarDrag = new ScrollbarDrag(view, isVertical, pointer, startOffset, offsetPerPixel);
    }

    private static void ScrollAlong(View view, bool isVertical, double offset)
    {
        if (isVertical)
        {
            view.ScrollTo(view.HorizontalOffset, offset);
        }
        else
        {
            view.ScrollTo(offset, view.VerticalOffset);
        }
    }

    private bool IsCurrentHitTarget(UIElement root, UIElement target)
    {
        return ReferenceEquals(_content, root) && ReferenceEquals(target.VisualRoot, root) && target.IsHitTestVisibleInTree &&
            root.IsAncestorOf(target);
    }

    private void BeginSelection(UIElement root, UIElement inputElement, Point viewportPoint, int clickCount)
    {
        if (!inputElement.IsEffectivelyEnabled || !IsCurrentHitTarget(root, inputElement))
        {
            return;
        }

        ClearAllTextSelections(root);
        _activeSelectionText = null;
        _activeSelectionBox = null;
        _isDraggingSelection = false;

        if (inputElement is not Text { IsTextSelectionEnabled: true } textControl ||
            FindBoxInFrame(textControl) is not LayoutBox hitBox ||
            hitBox.TextLayout is not ITextLayout textLayout)
        {
            return;
        }

        Point localPoint = new(
            viewportPoint.X - hitBox.ContentBox.X,
            viewportPoint.Y - hitBox.ContentBox.Y);

        TextHitTestResult hit = textLayout.HitTestPoint(localPoint);
        _activeSelectionText = textControl;
        _activeSelectionBox = hitBox;

        if (clickCount >= 3)
        {
            textControl.SelectAll();
            return;
        }

        if (clickCount == 2)
        {
            TextRange wordRange = textLayout.GetWordRangeAt(hit.Range.Start);
            textControl.Select(wordRange.Start, wordRange.Length);
            return;
        }

        _selectionAnchorPosition = hit.TextPosition;
        _isDraggingSelection = true;
    }

    /// <summary>
    /// Handles pointer movement at <paramref name="viewportPoint"/> (in logical window coordinates)
    /// by updating hover, routing pointer events, and extending any active selection drag.
    /// </summary>
    public void HandlePointerMoved(
        Point viewportPoint,
        bool isPressed = true,
        InputModifiers modifiers = InputModifiers.None)
    {
        if (_content is null || _inputBlocker != nint.Zero)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            PointerButtons buttons = isPressed ? _pointerButtons : PointerButtons.None;
            StorePointerState(viewportPoint, buttons, modifiers);

            // A drag that started in the visual tree stays there, even over an overlay.
            if (!isPressed || _overlayCapturesPointer)
            {
                DispatchOverlayPointer(OverlayPointerAction.Moved, viewportPoint);
            }

            if (_overlayCapturesPointer)
            {
                return;
            }

            if (_scrollbarDrag is ScrollbarDrag drag)
            {
                if (isPressed && ReferenceEquals(drag.View.GetInputTarget(), drag.View))
                {
                    double pointer = drag.IsVertical ? viewportPoint.Y : viewportPoint.X;
                    ScrollAlong(drag.View, drag.IsVertical, drag.StartOffset + ((pointer - drag.StartPointer) * drag.OffsetPerPixel));
                    return;
                }

                _scrollbarDrag = null;
            }

            UIElement? hoveredElement = HitTest(viewportPoint);
            UIElement? inputElement = InputManager.Current.GetPointerCapture(_content, MousePointerId) ?? hoveredElement?.GetInputTarget();
            UpdateHoverState(hoveredElement);
            if (inputElement is not null)
            {
                inputElement.RaiseEvent(
                    UIElement.PointerMovedEvent,
                    new PointerEventArgs(MousePointerId, viewportPoint, buttons, modifiers));
            }

            if (isPressed)
            {
                ExtendSelection(viewportPoint);
            }

            UpdateCursor();
        }
        finally
        {
            EndFrameBatch();
        }
    }

    private void ExtendSelection(Point viewportPoint)
    {
        if (!_isDraggingSelection || _activeSelectionText is not { IsEffectivelyEnabled: true } ||
            _activeSelectionBox?.TextLayout is not ITextLayout textLayout)
        {
            return;
        }

        Point localPoint = new(
            viewportPoint.X - _activeSelectionBox.ContentBox.X,
            viewportPoint.Y - _activeSelectionBox.ContentBox.Y);

        TextHitTestResult hit = textLayout.HitTestPoint(localPoint);
        int start = Math.Min(_selectionAnchorPosition, hit.TextPosition);
        int end = Math.Max(_selectionAnchorPosition, hit.TextPosition);
        _activeSelectionText.Select(start, end - start);
    }

    /// <summary>
    /// Routes a pointer release and ends any active selection drag.
    /// </summary>
    public void HandlePointerReleased(Point viewportPoint, InputModifiers modifiers = InputModifiers.None)
    {
        if (_content is null)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            StorePointerState(viewportPoint, PointerButtons.None, modifiers);
            if (_overlayCapturesPointer)
            {
                _overlayCapturesPointer = false;
                DispatchOverlayPointer(OverlayPointerAction.Released, viewportPoint);
                UpdateCursor();
                return;
            }

            if (_scrollbarDrag is not null)
            {
                _scrollbarDrag = null;
                return;
            }

            UIElement? hoveredElement = HitTest(viewportPoint);
            UIElement? inputElement = InputManager.Current.GetPointerCapture(_content, MousePointerId) ?? hoveredElement?.GetInputTarget();
            UpdateHoverState(hoveredElement);
            inputElement?.RaiseEvent(
                UIElement.PointerReleasedEvent,
                new PointerEventArgs(MousePointerId, viewportPoint, PointerButtons.None, modifiers));

            if (_isDraggingSelection)
            {
                ExtendSelection(viewportPoint);
                _isDraggingSelection = false;
            }

            UpdateCursor();
        }
        finally
        {
            UIElement? captured = InputManager.Current.GetPointerCapture(_content, MousePointerId);
            captured?.ReleasePointerCapture(MousePointerId);
            EndFrameBatch();
        }
    }

    /// <summary>
    /// Scrolls the innermost view under the pointer that can move by the given distance, in logical pixels of scroll offset.
    /// </summary>
    public void HandlePointerWheel(Point viewportPoint, double deltaX, double deltaY)
    {
        if (_content is null)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            UIElement? hoveredElement = HitTest(viewportPoint);
            UIElement? inputElement = hoveredElement?.GetInputTarget();
            for (UIElement? element = inputElement; element is not null; element = element.Parent)
            {
                if (element is View view && view.TryScrollBy(deltaX, deltaY))
                {
                    break;
                }
            }
        }
        finally
        {
            EndFrameBatch();
        }
    }

    /// <summary>
    /// Clears hover when the pointer leaves the window. An active selection drag continues until release or cancellation.
    /// </summary>
    public void HandlePointerExited()
    {
        BeginFrameBatch();
        try
        {
            DispatchOverlayPointer(OverlayPointerAction.Exited, default);
            UpdateHoverState(null);
            _pointerPosition = null;
            UpdateCursor();
        }
        finally
        {
            EndFrameBatch();
        }
    }

    /// <summary>
    /// Clears hover and cancels any selection drag, for example when the window stops being the key window.
    /// </summary>
    public void HandlePointerCancelled()
    {
        _overlayCapturesPointer = false;
        BeginFrameBatch();
        try
        {
            if (_content is UIElement root)
            {
                InputManager.Current.ReleasePointerCaptures(root);
            }
            ClearPointerState();
        }
        finally
        {
            EndFrameBatch();
        }
    }

    /// <summary>
    /// Updates the logical content size after the native window is resized and renders the new layout.
    /// </summary>
    public void HandleResized(Size contentSize)
    {
        if (Width == contentSize.Width && Height == contentSize.Height)
        {
            return;
        }

        Width = contentSize.Width;
        Height = contentSize.Height;
        InvalidateFrame();
    }

    /// <summary>
    /// Closes the dialog this window presents, when AppKit closes the window through its title bar.
    /// </summary>
    internal void HandleNativeClose()
    {
        _hostedDialog?.Close();
    }

    /// <summary>
    /// Closes the dialog this window presents when it is modal, and returns whether it did.
    /// </summary>
    internal bool TryCloseModalDialog()
    {
        if (_hostedDialog is not { IsModal: true } dialog)
        {
            return false;
        }

        dialog.Close();
        return true;
    }

    /// <summary>
    /// Reapplies the requested cursor when AppKit asks the view to update it.
    /// </summary>
    public void HandleCursorUpdate()
    {
        UpdateCursor();
    }

    /// <summary>
    /// Routes a key press to the focused element through <see cref="UIElement.KeyDownEvent"/>, then performs the default
    /// action of a press that no handler handled: Tab and Shift+Tab move focus, Escape closes a modal dialog, and
    /// <c>Cmd+C</c> / <c>Ctrl+C</c> and <c>Cmd+A</c> / <c>Ctrl+A</c> copy and select rendered text. While an
    /// <see cref="Input"/> has focus, its native field receives keys instead, including these shortcuts.
    /// </summary>
    /// <param name="key">The characters of the key, ignoring every modifier except Shift.</param>
    /// <param name="commandOrControl">Whether the Command or Control key is held.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <param name="isRepeat">Whether the press repeats because the key is held down.</param>
    /// <returns><see langword="true"/> when a handler or a default action handled the press.</returns>
    public async ValueTask<bool> HandleKeyDownAsync(
        string key,
        bool commandOrControl,
        InputModifiers modifiers = InputModifiers.None,
        bool isRepeat = false)
    {
        if (_content is null || _keyboard is null || string.IsNullOrEmpty(key) || _inputBlocker != nint.Zero)
        {
            return false;
        }

        foreach (IRenderOverlay overlay in Overlays)
        {
            if (overlay.HandleKey(key, modifiers))
            {
                return true;
            }
        }

        if (_textFields.Keys.Any(static input => input.IsFocused))
        {
            return false;
        }

        (Key logicalKey, string? text) = MapKey(key);
        if (_keyboard.RaiseKeyEvent(_content, UIElement.KeyDownEvent, new KeyEventArgs(logicalKey, text, modifiers, isRepeat)))
        {
            return true;
        }

        if (logicalKey == Key.Tab && !commandOrControl)
        {
            _ = _keyboard.MoveFocus(_content, modifiers.HasFlag(InputModifiers.Shift) ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next);
            return true;
        }

        if (logicalKey == Key.Escape && TryCloseModalDialog())
        {
            return true;
        }

        if (commandOrControl && string.Equals(key, "c", StringComparison.OrdinalIgnoreCase))
        {
            string selected = CollectSelectedText(_content);
            if (!string.IsNullOrEmpty(selected))
            {
                await _clipboard.SetTextAsync(selected).ConfigureAwait(false);
                return true;
            }
        }
        else if (commandOrControl && string.Equals(key, "a", StringComparison.OrdinalIgnoreCase))
        {
            if (_activeSelectionText is { IsTextSelectionEnabled: true })
            {
                _activeSelectionText.SelectAll();
                return true;
            }

            SelectAllSelectableText(_content);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Routes a key release to the focused element through <see cref="UIElement.KeyUpEvent"/>. While an
    /// <see cref="Input"/> has focus, its native field receives the release instead.
    /// </summary>
    /// <param name="key">The characters of the key, ignoring every modifier except Shift.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when a handler handled the release.</returns>
    public bool HandleKeyUp(string key, InputModifiers modifiers = InputModifiers.None)
    {
        if (_content is null || _keyboard is null || string.IsNullOrEmpty(key) || _inputBlocker != nint.Zero ||
            _textFields.Keys.Any(static input => input.IsFocused))
        {
            return false;
        }

        (Key logicalKey, string? text) = MapKey(key);
        return _keyboard.RaiseKeyEvent(_content, UIElement.KeyUpEvent, new KeyEventArgs(logicalKey, text, modifiers));
    }

    /// <summary>
    /// Maps the characters of an AppKit key event to a logical key. AppKit reports keys without text as characters in
    /// the Unicode private use area, such as <c>NSUpArrowFunctionKey</c>.
    /// </summary>
    private static (Key Key, string? Text) MapKey(string key)
    {
        Key logicalKey = key switch
        {
            "\t" or "\u0019" => Key.Tab,
            "\r" or "\u0003" => Key.Enter,
            " " => Key.Space,
            EscapeKey => Key.Escape,
            "\u007f" => Key.Backspace,
            "\uF728" => Key.Delete,
            "\uF727" => Key.Insert,
            "\uF700" => Key.ArrowUp,
            "\uF701" => Key.ArrowDown,
            "\uF702" => Key.ArrowLeft,
            "\uF703" => Key.ArrowRight,
            "\uF729" => Key.Home,
            "\uF72B" => Key.End,
            "\uF72C" => Key.PageUp,
            "\uF72D" => Key.PageDown,
            "\uF735" => Key.ContextMenu,
            [char c] when c is >= '\uF704' and <= '\uF70F' => Key.F1 + (c - '\uF704'),
            _ => Key.Character
        };

        return logicalKey switch
        {
            Key.Character => (Key.Character, key),
            Key.Space => (Key.Space, key),
            _ => (logicalKey, null)
        };
    }

    /// <summary>
    /// Keeps keyboard events in this window's content view while a rendered element has focus, or while no element has
    /// focus. A focused <see cref="Input"/> keeps its native field as the first responder.
    /// </summary>
    private void OnFocusChanged(UIElement? previousRoot, UIElement? root)
    {
        if (!ReferenceEquals(root, _content) && (root is not null || !ReferenceEquals(previousRoot, _content)))
        {
            return;
        }

        bool isNativeFieldFocused = _keyboard?.FocusedElement is Input input && _textFields.ContainsKey(input);
        if (!isNativeFieldFocused && _nsWindow != nint.Zero && _nsImageView != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("makeFirstResponder:"), _nsImageView);
        }
    }

    private void DetachKeyboard()
    {
        if (_keyboard is not null)
        {
            _keyboard.FocusChanged -= OnFocusChanged;
            _keyboard = null;
        }
    }

    /// <summary>
    /// Renders a single frame of <see cref="Content"/> onto a <see cref="SkiaSurface"/>,
    /// updates <see cref="LastLayout"/>, and presents to the native <c>NSWindow</c> if visible.
    /// </summary>
    public SKImage? RenderFrame()
    {
        if (_content is null)
        {
            return null;
        }

        int pixelWidth = Math.Max(1, (int)Math.Round(Width * ScaleFactor));
        int pixelHeight = Math.Max(1, (int)Math.Round(Height * ScaleFactor));

        if (_surface is null || _surface.PixelWidth != pixelWidth || _surface.PixelHeight != pixelHeight)
        {
            _surface?.Dispose();
            _surface = new SkiaSurface(pixelWidth, pixelHeight);
        }

        // The canvas outlives the frame, and a CanvasView handler can leave saved states on it.
        _surface.Canvas.RestoreToCount(1);
        SkiaDrawingContext drawingContext = new(_surface.Canvas, SkiaTextShaper.Shared);
        Size layoutViewport = _hostedDialog is not null ? s_contentMeasureViewport : new Size(Width, Height);
        LastLayout = _renderer.Render(drawingContext, _content, layoutViewport, ScaleFactor, _background);

        if (_hostedDialog is not null && TryResizeToContent(LastLayout))
        {
            return RenderFrame();
        }

        if (_activeSelectionText is not null && LastLayout is not null)
        {
            _activeSelectionBox = FindBoxInFrame(_activeSelectionText);
        }

        RenderCount++;

        SKImage snapshot = _surface.Snapshot();
        if (_nsImageView != nint.Zero && OperatingSystem.IsMacOS())
        {
            PresentSnapshotToNativeView(snapshot);
            if (LastLayout is not null)
            {
                // Native views changing focus or closing invalidate the tree, which must not render during the updates.
                BeginFrameBatch();
                try
                {
                    SyncTextFields(LastLayout);
                    SyncDialogWindows();
                }
                finally
                {
                    EndFrameBatch();
                }
            }
        }

        return snapshot;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        using IDisposable? animationRoot = _animationRoot;
        _animationRoot = null;
        Application.Current.CurrentThemeChanged -= OnCurrentThemeChanged;
        HotReloadManager.HotReloaded -= OnHotReloaded;

        if (_isAnimationDriver)
        {
            _isAnimationDriver = false;
            AnimationClock.FrameRequested -= OnFrameRequested;
            AnimationClock.DetachDriver();
        }

        if (_displayLink != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(_displayLink, AppKitInterop.GetSelector("invalidate"));
            _ = AppKitInterop.SendMessage(_displayLink, AppKitInterop.GetSelector("release"));
            _displayLink = nint.Zero;
        }

        if (_nsImageView != nint.Zero)
        {
            AppKitInterop.RemovePointerTracking(_nsImageView);
            AppKitInterop.UnregisterWindowView(_nsImageView);
        }

        _overlayManager?.Dispose();
        _overlayManager = null;
        if (_content is not null)
        {
            _content.DetachFromVisualTree(_content, _content.Parent);
            _content.VisualInvalidated -= OnContentVisualInvalidated;
            _content.RemoveHandler(Hyperlink.RequestNavigateEvent, OnRequestNavigate);
            _content = null;
        }

        RemoveTextFields();
        RemoveDialogWindows();
        DetachKeyboard();
        if (_hostedDialog is not null && _nsWindow != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("orderOut:"), nint.Zero);
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("autorelease"));
        }

        _pointerRouter.Reset();
        _pointerPosition = null;
        _isDraggingSelection = false;
        _activeSelectionText = null;
        _activeSelectionBox = null;
        _renderer.Dispose();
        _surface?.Dispose();
        _surface = null;
        _nsImageView = nint.Zero;
        _nsWindow = nint.Zero;
    }

    /// <summary>
    /// Finds the topmost visible element at <paramref name="point"/>, searching children in reverse paint order.
    /// </summary>
    private static UIElement? HitTest(LayoutBox box, Point point)
    {
        if (box.Element is not UIElement { IsHitTestVisible: true } element)
        {
            return null;
        }

        if (box.Style.Transform is { IsIdentity: false } transform)
        {
            point = transform.ToUntransformed(point, box.BorderBox);
        }

        bool clipsChildren = box.Style.ClipsOverflow || element is View { ClipToBounds: true };
        if (!clipsChildren || box.BorderBox.Contains(point))
        {
            for (int i = box.Children.Count - 1; i >= 0; i--)
            {
                UIElement? childMatch = HitTest(box.Children[i], point);
                if (childMatch is not null)
                {
                    return childMatch;
                }
            }
        }

        return box.BorderBox.Contains(point) ? element : null;
    }

    private static LayoutBox? FindBoxForElement(LayoutBox box, UIElement target)
    {
        if (ReferenceEquals(box.Element, target))
        {
            return box;
        }

        foreach (LayoutBox child in box.Children)
        {
            LayoutBox? found = FindBoxForElement(child, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ClearAllTextSelections(UIElement element)
    {
        if (element is Text textControl)
        {
            textControl.ClearSelection();
        }
        else if (element is View view)
        {
            foreach (UIElement child in view.Children)
            {
                ClearAllTextSelections(child);
            }
        }
    }

    private static void SelectAllSelectableText(UIElement element)
    {
        if (element is Text { IsVisible: true, IsTextSelectionEnabled: true, IsEffectivelyEnabled: true } textControl)
        {
            textControl.SelectAll();
        }
        else if (element is View view)
        {
            foreach (UIElement child in view.Children)
            {
                SelectAllSelectableText(child);
            }
        }
    }

    private static string CollectSelectedText(UIElement root)
    {
        StringBuilder sb = new();
        AppendSelectedTextRecursive(root, sb);
        return sb.ToString();
    }

    private static void AppendSelectedTextRecursive(UIElement element, StringBuilder sb)
    {
        if (!element.IsVisible)
        {
            return;
        }

        if (element is Text { HasSelection: true } textControl)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(textControl.SelectedText);
        }
        else if (element is View view)
        {
            foreach (UIElement child in view.Children)
            {
                AppendSelectedTextRecursive(child, sb);
            }
        }
    }

    /// <summary>
    /// Creates, positions, and removes native fields to match the <see cref="Input"/> elements visible in the layout.
    /// </summary>
    private void SyncTextFields(LayoutBox root)
    {
        HashSet<Input> placed = [];
        Rect windowArea = new(0, 0, Width, Height);
        List<LayoutBox> layouts = [.. _renderer.PortalBoxes];
        CollectPositionedElements(root, layouts);
        PlaceTextFields(root, windowArea, 1.0, placed, layouts);

        if (_inputBlocker != nint.Zero)
        {
            // Adding a view that is already a subview moves it above the fields created since.
            AppKitInterop.SendMessageVoid(_nsImageView, AppKitInterop.GetSelector("addSubview:"), _inputBlocker);
        }

        foreach (Input input in _textFields.Keys.ToArray())
        {
            if (!placed.Contains(input))
            {
                _textFields.Remove(input, out NativeTextField? field);
                field?.Dispose();
            }
        }
    }

    /// <summary>
    /// Positions the fields under <paramref name="box"/>. A native field sits above everything Skia draws, so a field that
    /// touches one of <paramref name="covers"/> is hidden instead of showing through it.
    /// </summary>
    private void PlaceTextFields(LayoutBox box, Rect clip, double opacity, HashSet<Input> placed, IReadOnlyList<LayoutBox> covers)
    {
        if (box.Element is not UIElement { IsVisible: true } element)
        {
            return;
        }

        if (element is IWindowHostedElement { HasOwnWindow: true } && !ReferenceEquals(element, _content))
        {
            return;
        }

        // A native field cannot follow a transform, so it stays hidden while one applies.
        if (box.Style.Transform is { IsIdentity: false })
        {
            clip = default;
        }

        double boxOpacity = opacity * box.Style.Opacity;
        if (element is Input input)
        {
            if (!_textFields.TryGetValue(input, out NativeTextField? field))
            {
                field = new NativeTextField(input, _nsImageView, this);
                _textFields[input] = field;
            }

            Rect visible = clip.Intersect(box.BorderBox);
            if (covers.Any(cover => cover.BorderBox.Intersect(visible).Width > 0))
            {
                visible = default;
            }

            field.Place(visible, box.ContentBox, box.Style, boxOpacity, Height);
            placed.Add(input);
            return;
        }

        bool clipsChildren = box.Style.ClipsOverflow || element is View { ClipToBounds: true };
        Rect childClip = clipsChildren ? clip.Intersect(box.BorderBox) : clip;
        foreach (LayoutBox child in box.Children)
        {
            PlaceTextFields(child, childClip, boxOpacity, placed, covers);
        }
    }

    private static void CollectPositionedElements(LayoutBox box, List<LayoutBox> layouts)
    {
        if (box.Element is not UIElement { IsVisible: true })
        {
            return;
        }

        if (box.Style.Position is Position.Absolute or Position.Fixed && box.Style.BackgroundColor is not null)
        {
            layouts.Add(box);
        }

        foreach (LayoutBox child in box.Children)
        {
            CollectPositionedElements(child, layouts);
        }
    }

    /// <summary>
    /// Finds the box of <paramref name="element"/> in the last layout, including open portals.
    /// </summary>
    private LayoutBox? FindBoxInFrame(UIElement element)
    {
        LayoutBox? found = LastLayout is null ? null : FindBoxForElement(LastLayout, element);
        foreach (LayoutBox portalBox in _renderer.PortalBoxes)
        {
            found ??= FindBoxForElement(portalBox, element);
        }

        return found;
    }

    /// <summary>
    /// Matches the window to the size of its laid-out content, and returns whether the size changed.
    /// </summary>
    private bool TryResizeToContent(LayoutBox? layout)
    {
        if (layout is null)
        {
            return false;
        }

        double width = Math.Max(1, Math.Ceiling(layout.BorderBox.Width));
        double height = Math.Max(1, Math.Ceiling(layout.BorderBox.Height));
        if (width == Width && height == Height)
        {
            return false;
        }

        Width = width;
        Height = height;
        if (_nsWindow != nint.Zero)
        {
            AppKitInterop.SendMessageSetSize(_nsWindow, AppKitInterop.GetSelector("setContentSize:"), new AppKitInterop.CGSize(width, height));
        }

        return true;
    }

    /// <summary>
    /// Creates, positions, and closes child windows to match the open <see cref="Dialog"/> elements in the content.
    /// </summary>
    private void SyncDialogWindows()
    {
        HashSet<Dialog> open = [];
        CollectOpenDialogs(_content!, open);

        foreach (Dialog dialog in _dialogWindows.Keys.ToArray())
        {
            if (!open.Contains(dialog))
            {
                RemoveDialogWindow(dialog);
            }
        }

        foreach (Dialog dialog in open)
        {
            bool isNew = !_dialogWindows.TryGetValue(dialog, out DialogWindow? dialogWindow);
            if (dialogWindow is null)
            {
                dialogWindow = new DialogWindow(CreateDialogWindow(dialog));
                _dialogWindows[dialog] = dialogWindow;
            }

            string title = dialog.Title ?? string.Empty;
            if (dialogWindow.Window.Title != title)
            {
                dialogWindow.Window.Title = title;
            }

            PlaceDialogWindow(dialog, dialogWindow);
            if (isNew)
            {
                AppKitInterop.SendMessageVoid(dialogWindow.Window._nsWindow, AppKitInterop.GetSelector("makeKeyAndOrderFront:"), nint.Zero);
            }
        }

        UpdateInputBlocker();
    }

    private static void CollectOpenDialogs(UIElement element, HashSet<Dialog> open)
    {
        if (element is not View { IsVisible: true } view)
        {
            return;
        }

        foreach (UIElement child in view.Children)
        {
            // A dialog keeps its window until its exit animations finish.
            if (child is Dialog { IsVisible: true, HasOwnWindow: true } dialog)
            {
                open.Add(dialog);
            }
            else if (child is not Portal { IsVisible: false } and not Dialog { HasOwnWindow: true })
            {
                CollectOpenDialogs(child, open);
            }
        }
    }

    /// <summary>
    /// Creates a child window that presents <paramref name="dialog"/>. The title bar stays for dragging and closing, and hides
    /// its text because the dialog draws its own title. AppKit moves a child window with its parent.
    /// </summary>
    private Window CreateDialogWindow(Dialog dialog)
    {
        Window window = new(_clipboard)
        {
            ScaleFactor = ScaleFactor,
            Width = 1,
            Height = 1,
            _hostedDialog = dialog
        };
        window.Content = dialog;
        window.CreateNativeWindow(StyleMaskTitled | StyleMaskClosable);
        AppKitInterop.SendMessageVoid(window._nsWindow, AppKitInterop.GetSelector("setTitleVisibility:"), TitleVisibilityHidden);
        AppKitInterop.SendMessageVoid(window._nsWindow, AppKitInterop.GetSelector("setReleasedWhenClosed:"), false);
        window.UpdateFrame();

        _ = AppKitInterop.SendMessage(
            _nsWindow,
            AppKitInterop.GetSelector("addChildWindow:ordered:"),
            window._nsWindow,
            ChildWindowOrderedAbove);
        return window;
    }

    /// <summary>
    /// Moves a dialog's window only when its calculated position changes, so a window the user dragged stays in place across
    /// frames. Calculations use the top-down coordinates of this window's content; the title bar counts as part of the dialog.
    /// </summary>
    private void PlaceDialogWindow(Dialog dialog, DialogWindow dialogWindow)
    {
        nint childWindow = dialogWindow.Window._nsWindow;
        AppKitInterop.CGRect frame = AppKitInterop.GetRect(childWindow, "frame");
        AppKitInterop.CGRect content = AppKitInterop.GetRect(
            _nsWindow,
            "contentRectForFrameRect:",
            AppKitInterop.GetRect(_nsWindow, "frame"));

        double contentTop = content.Origin.Y + content.Size.Height;
        Rect windowArea = new(0, 0, Width, Height);
        Rect limits = windowArea;
        nint screen = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("screen"));
        if (screen != nint.Zero)
        {
            AppKitInterop.CGRect visible = AppKitInterop.GetRect(screen, "visibleFrame");
            limits = new Rect(
                visible.Origin.X - content.Origin.X,
                contentTop - (visible.Origin.Y + visible.Size.Height),
                visible.Size.Width,
                visible.Size.Height);
        }

        Rect? anchor = dialog.Anchor is UIElement element ? FindBoxInFrame(element)?.BorderBox : null;
        Size size = new(frame.Size.Width, frame.Size.Height);
        Point position = PortalPlacement.Compute(dialog, anchor, size, windowArea, limits);
        Rect placement = new(position.X, position.Y, size.Width, size.Height);
        if (dialogWindow.Placement == placement)
        {
            return;
        }

        dialogWindow.Placement = placement;
        AppKitInterop.SendMessageSetPoint(
            childWindow,
            AppKitInterop.GetSelector("setFrameOrigin:"),
            new AppKitInterop.CGPoint(content.Origin.X + position.X, contentTop - position.Y - size.Height));
    }

    /// <summary>
    /// Blocks input to this window's content while a modal dialog is open.
    /// </summary>
    private void UpdateInputBlocker()
    {
        bool isBlocked = _dialogWindows.Keys.Any(static dialog => dialog.IsModal);
        if (isBlocked && _inputBlocker == nint.Zero)
        {
            _inputBlocker = AppKitInterop.CreateInputBlocker(_nsImageView);
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("makeFirstResponder:"), _nsImageView);
        }
        else if (!isBlocked && _inputBlocker != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(_inputBlocker, AppKitInterop.GetSelector("removeFromSuperview"));
            _ = AppKitInterop.SendMessage(_inputBlocker, AppKitInterop.GetSelector("release"));
            _inputBlocker = nint.Zero;
        }
    }

    private void RemoveDialogWindow(Dialog dialog)
    {
        _dialogWindows.Remove(dialog, out DialogWindow? dialogWindow);
        if (dialogWindow is null)
        {
            return;
        }

        nint childWindow = dialogWindow.Window._nsWindow;
        if (_nsWindow != nint.Zero && childWindow != nint.Zero)
        {
            AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("removeChildWindow:"), childWindow);
        }

        dialogWindow.Window.Dispose();
        if (_nsWindow != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("makeKeyWindow"));
        }
    }

    private void RemoveDialogWindows()
    {
        foreach (Dialog dialog in _dialogWindows.Keys.ToArray())
        {
            RemoveDialogWindow(dialog);
        }

        if (_nsWindow != nint.Zero)
        {
            UpdateInputBlocker();
        }
    }

    private void RemoveTextFields()
    {
        foreach (NativeTextField field in _textFields.Values)
        {
            field.Dispose();
        }

        _textFields.Clear();
    }

    private void SyncContentStyle()
    {
        if (_content is null)
        {
            return;
        }

        if (_styleResolver is not null)
        {
            _content.StyleResolver = _styleResolver;
        }

        _content.InvalidateStyle();
    }

    private void OnCurrentThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        SyncContentStyle();
        InvalidateFrame();
    }

    private void OnHotReloaded(Type[]? updatedTypes)
    {
        if (_disposed || _content is null)
        {
            return;
        }

        SyncContentStyle();
        InvalidateFrame();
    }

    private void OnRequestNavigate(object? sender, RoutedEventArgs args)
    {
        if (args.Source is Hyperlink { NavigateUri: Uri uri } && uri.IsAbsoluteUri)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            args.Handled = true;
        }
    }

    private void OnContentVisualInvalidated(object? sender, EventArgs e)
    {
        InvalidateFrame();
    }

    private UIElement? HitTest(Point position)
    {
        return HitTest(position, out _);
    }

    private UIElement? HitTest(Point position, out ScrollbarGeometry? scrollbar)
    {
        scrollbar = null;
        // A dialog in its own window takes no input during its exit animations.
        if (LastLayout is null || _content is { IsHitTestVisible: false } || Overlays.Any(overlay => overlay.ContainsPoint(position)))
        {
            return null;
        }

        for (int i = _renderer.PortalBoxes.Count - 1; i >= 0; i--)
        {
            LayoutBox box = _renderer.PortalBoxes[i];
            // A portal that is closed and still drawn during its exit takes no input.
            if (box.Element is UIElement { IsHitTestVisible: false })
            {
                continue;
            }

            scrollbar = VisualTreeRenderer.HitTestScrollbar(box, position);
            if (scrollbar is ScrollbarGeometry portalScrollbar)
            {
                return portalScrollbar.View;
            }

            UIElement? portalMatch = HitTest(box, position);
            if (portalMatch is not null)
            {
                return portalMatch;
            }
        }

        scrollbar = VisualTreeRenderer.HitTestScrollbar(LastLayout, position);
        return scrollbar is ScrollbarGeometry rootScrollbar ? rootScrollbar.View : HitTest(LastLayout, position);
    }

    /// <summary>
    /// Returns the element under a pointer that has not moved. The hovered element keeps the hover while its layout box
    /// contains the pointer, so a hover style's transform cannot move it away, end the hover, and restart it on every frame.
    /// </summary>
    private UIElement? ResolveHoverTarget(Point position)
    {
        UIElement? hovered = _pointerRouter.HoveredElement;
        if (_content is UIElement root && hovered is not null && IsCurrentHitTarget(root, hovered) &&
            FindBoxInFrame(hovered)?.BorderBox.Contains(position) == true)
        {
            return hovered;
        }

        return HitTest(position);
    }

    /// <summary>
    /// Offers pointer input to overlays from the topmost down, and returns whether one accepted it.
    /// </summary>
    private bool DispatchOverlayPointer(OverlayPointerAction action, Point position)
    {
        bool accepted = false;
        for (int i = Overlays.Count - 1; i >= 0; i--)
        {
            accepted |= Overlays[i].HandlePointer(action, position);
        }

        return accepted;
    }

    private void UpdateHoverState(UIElement? hoveredElement)
    {
        Point position = _pointerPosition ?? Point.Zero;
        _pointerRouter.UpdateHover(hoveredElement, MousePointerId, position, _pointerButtons, _pointerModifiers);
    }

    private void StorePointerState(Point position, PointerButtons buttons, InputModifiers modifiers)
    {
        _pointerPosition = position;
        _pointerButtons = buttons;
        _pointerModifiers = modifiers;
    }

    private void ClearPointerState()
    {
        UpdateHoverState(null);
        _pointerPosition = null;
        _pointerButtons = PointerButtons.None;
        _pointerModifiers = InputModifiers.None;
        _isDraggingSelection = false;
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        UIElement? hovered = _pointerRouter.HoveredElement;
        if (hovered is not null && (_content is null || !IsCurrentHitTarget(_content, hovered)))
        {
            hovered = null;
        }

        Cursor cursor = (_content is UIElement root ? InputManager.Current.GetPointerCapture(root, MousePointerId) : null)?.Cursor ?? hovered?.Cursor ?? Cursor.Auto;

        if (_isDraggingSelection)
        {
            cursor = Cursor.IBeam;
        }
        else if (cursor == Cursor.Auto)
        {
            bool isSelectableText = hovered is Text { IsTextSelectionEnabled: true, IsEffectivelyEnabled: true };
            bool isEnabledInput = hovered is Input { IsEffectivelyEnabled: true };

            cursor = isSelectableText || isEnabledInput ? Cursor.IBeam : Cursor.Default;
        }

        IsTextCursorActive = cursor == Cursor.IBeam;
        if (_nsImageView != nint.Zero && OperatingSystem.IsMacOS())
        {
            AppKitInterop.SetCursor(cursor);
        }
    }

    /// <summary>
    /// Reevaluates the stationary pointer and any selection drag against the most recent layout.
    /// </summary>
    private void RefreshPointerState()
    {
        if (_isDraggingSelection && _activeSelectionText is not { IsVisible: true, IsTextSelectionEnabled: true })
        {
            _isDraggingSelection = false;
        }

        if (_pointerPosition is Point position)
        {
            UpdateHoverState(ResolveHoverTarget(position));
        }

        UpdateCursor();
    }

    private void BeginFrameBatch()
    {
        _frameBatchDepth++;
    }

    private void EndFrameBatch()
    {
        _frameBatchDepth--;
        if (_frameBatchDepth == 0 && _isFrameDirty)
        {
            _isFrameDirty = false;
            InvalidateFrame();
        }
    }

    private void InvalidateFrame()
    {
        if (_content is null)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            IDispatcher dispatcher = Application.Current.Dispatcher;
            if (ReferenceEquals(dispatcher, ImmediateDispatcher.Instance))
            {
                _isFrameDirty = true;
            }
            else
            {
                dispatcher.Invoke(InvalidateFrame);
            }

            return;
        }

        if (_frameBatchDepth > 0)
        {
            _isFrameDirty = true;
            return;
        }

        // A shown window renders at most once per screen refresh; any number of invalidations before it share one frame.
        if (_displayLink != nint.Zero)
        {
            if (!_isFrameScheduled)
            {
                _isFrameScheduled = true;
                AppKitInterop.SendMessageVoid(_displayLink, AppKitInterop.GetSelector("setPaused:"), false);
            }

            return;
        }

        UpdateFrame();
    }

    /// <summary>
    /// Renders and presents a frame immediately, then reevaluates the pointer against its layout.
    /// </summary>
    private void UpdateFrame()
    {
        if (_content is null)
        {
            return;
        }

        RenderInvalidatedFrame();

        // Hover changes caused by the new layout render one further frame; later shifts wait for the next invalidation.
        _frameBatchDepth++;
        try
        {
            RefreshPointerState();
        }
        finally
        {
            _frameBatchDepth--;
        }

        if (_isFrameDirty)
        {
            _isFrameDirty = false;
            if (_displayLink != nint.Zero)
            {
                InvalidateFrame();
            }
            else
            {
                RenderInvalidatedFrame();
            }
        }
    }

    private void RenderInvalidatedFrame()
    {
        using SKImage? snapshot = RenderFrame();
    }

    /// <summary>
    /// Tracks the child window of an open <see cref="Dialog"/> and the placement last applied to it.
    /// </summary>
    private sealed class DialogWindow(Window window)
    {
        public Window Window { get; } = window;

        public Rect? Placement { get; set; }
    }

    private void PresentSnapshotToNativeView(SKImage snapshot)
    {
        using SKPixmap? pixels = snapshot.PeekPixels();
        if (pixels is null)
        {
            return;
        }

        nint nsImage = AppKitInterop.CreateImage(
            pixels.GetPixelSpan(),
            pixels.Width,
            pixels.Height,
            pixels.RowBytes,
            new AppKitInterop.CGSize(Width, Height));
        AppKitInterop.SendMessageVoid(_nsImageView, AppKitInterop.GetSelector("setImage:"), nsImage);
        _ = AppKitInterop.SendMessage(nsImage, AppKitInterop.GetSelector("release"));
    }

    /// <summary>
    /// Holds a drag of a scrollbar thumb: the scrolled view, its axis, the pointer position and scroll offset where the drag
    /// started, and the scroll distance for each logical pixel the pointer moves.
    /// </summary>
    private readonly record struct ScrollbarDrag(View View, bool IsVertical, double StartPointer, double StartOffset, double OffsetPerPixel);
}
