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
/// propagates <see cref="ActualTheme"/> and <see cref="StyleResolver"/>, routes mouse/keyboard text selection
/// and clipboard operations, and renders its visual tree using <see cref="VisualTreeRenderer"/> and <see cref="SkiaSurface"/>.
/// </summary>
public sealed class Window : IDisposable
{
    private const ulong StyleMaskTitled = 1UL << 0;
    private const ulong StyleMaskClosable = 1UL << 1;
    private const ulong StyleMaskMiniaturizable = 1UL << 2;
    private const ulong StyleMaskResizable = 1UL << 3;
    private const ulong BackingStoreBuffered = 2UL;
    private const long MousePointerId = 1;

    private readonly VisualTreeRenderer _renderer = new(SkiaTextShaper.Shared);
    private readonly PointerInputRouter _pointerRouter = new();
    private readonly IPlatformSettings _platformSettings;
    private readonly IClipboard _clipboard;
    private UIElement? _content;
    private Theme _theme = Theme.Default;
    private IStyleResolver? _styleResolver;
    private Color? _background;
    private nint _nsWindow;
    private nint _nsImageView;
    private string _title = "NativeUI";
    private Text? _activeSelectionText;
    private LayoutBox? _activeSelectionBox;
    private int _selectionAnchorPosition;
    private bool _isDraggingSelection;
    private Point? _pointerPosition;
    private PointerButtons _pointerButtons;
    private PointerModifiers _pointerModifiers;
    private int _frameBatchDepth;
    private bool _isFrameDirty;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Window"/> class.
    /// </summary>
    /// <param name="platformSettings">Optional platform settings service; defaults to <see cref="MacOSPlatformSettings.Instance"/>.</param>
    /// <param name="clipboard">Optional clipboard service; defaults to <see cref="MacOSClipboard.Instance"/>.</param>
    public Window(IPlatformSettings? platformSettings = null, IClipboard? clipboard = null)
    {
        _platformSettings = platformSettings ?? MacOSPlatformSettings.Instance;
        _clipboard = clipboard ?? MacOSClipboard.Instance;
        _platformSettings.ThemeChanged += OnPlatformThemeChanged;
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
    /// Gets or sets the requested <see cref="Core.Theme"/> for this window (<see cref="Theme.Default"/> follows the OS appearance).
    /// </summary>
    public Theme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value)
            {
                return;
            }

            _theme = value;
            SyncContentThemeAndStyle();
            InvalidateFrame();
        }
    }

    /// <summary>
    /// Gets the resolved <see cref="Core.Theme"/> (<see cref="Theme.Light"/> or <see cref="Theme.Dark"/>) active in this window.
    /// </summary>
    public Theme ActualTheme => _theme != Theme.Default ? _theme : _platformSettings.GetSystemTheme();

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
            SyncContentThemeAndStyle();
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

    /// <summary>
    /// Gets the native Objective-C <c>NSWindow*</c> pointer when shown on macOS, or <see cref="nint.Zero"/> in headless mode.
    /// </summary>
    public nint NativeHandle => _nsWindow;

    /// <summary>
    /// Gets a value indicating whether this window requests the I-beam cursor, either over selectable text or during a selection drag.
    /// </summary>
    public bool IsTextCursorActive { get; private set; }

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
            }

            _activeSelectionText = null;
            _activeSelectionBox = null;
            _content = value;
            if (_content is not null)
            {
                SyncContentThemeAndStyle();
                _content.VisualInvalidated += OnContentVisualInvalidated;
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
            AppKitInterop.EnsureFrameworksLoaded();
            SyncContentThemeAndStyle();

            AppKitInterop.CGRect rect = new(120, 120, Width, Height);
            ulong styleMask = StyleMaskTitled | StyleMaskClosable | StyleMaskMiniaturizable | StyleMaskResizable;

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
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("makeFirstResponder:"), _nsImageView);
            _ = AppKitInterop.SendMessage(_nsWindow, AppKitInterop.GetSelector("center"));
            AppKitInterop.SendMessageVoid(_nsWindow, AppKitInterop.GetSelector("makeKeyAndOrderFront:"), nint.Zero);
        }

        InvalidateFrame();
    }

    /// <summary>
    /// Handles a pointer press at <paramref name="viewportPoint"/> (in logical window coordinates)
    /// by routing pointer events and beginning character selection or performing double-click word selection.
    /// </summary>
    public void HandlePointerPressed(
        Point viewportPoint,
        int clickCount = 1,
        PointerModifiers modifiers = PointerModifiers.None)
    {
        if (_content is null || LastLayout is null)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            StorePointerState(viewportPoint, PointerButtons.Primary, modifiers);
            UIElement? hoveredElement = HitTest(viewportPoint);
            UpdateHoverState(hoveredElement);
            hoveredElement?.RaiseEvent(
                UIElement.PointerPressedEvent,
                new PointerEventArgs(MousePointerId, viewportPoint, PointerButtons.Primary, modifiers, clickCount));
            BeginSelection(hoveredElement, viewportPoint, clickCount);
            UpdateCursor();
        }
        finally
        {
            EndFrameBatch();
        }
    }

    private void BeginSelection(UIElement? hoveredElement, Point viewportPoint, int clickCount)
    {
        ClearAllTextSelections(_content!);
        _activeSelectionText = null;
        _activeSelectionBox = null;
        _isDraggingSelection = false;

        if (hoveredElement is not Text { IsTextSelectionEnabled: true } textControl ||
            FindBoxForElement(LastLayout!, textControl) is not LayoutBox hitBox ||
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
        PointerModifiers modifiers = PointerModifiers.None)
    {
        if (_content is null)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            PointerButtons buttons = isPressed ? PointerButtons.Primary : PointerButtons.None;
            StorePointerState(viewportPoint, buttons, modifiers);
            UIElement? hoveredElement = HitTest(viewportPoint);
            UpdateHoverState(hoveredElement);
            hoveredElement?.RaiseEvent(
                UIElement.PointerMovedEvent,
                new PointerEventArgs(MousePointerId, viewportPoint, buttons, modifiers));

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
        if (!_isDraggingSelection || _activeSelectionText is null || _activeSelectionBox?.TextLayout is not ITextLayout textLayout)
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
    public void HandlePointerReleased(Point viewportPoint, PointerModifiers modifiers = PointerModifiers.None)
    {
        if (_content is null)
        {
            return;
        }

        BeginFrameBatch();
        try
        {
            StorePointerState(viewportPoint, PointerButtons.None, modifiers);
            UIElement? hoveredElement = HitTest(viewportPoint);
            UpdateHoverState(hoveredElement);
            hoveredElement?.RaiseEvent(
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
        BeginFrameBatch();
        try
        {
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
    /// Reapplies the requested cursor when AppKit asks the view to update it.
    /// </summary>
    public void HandleCursorUpdate()
    {
        UpdateCursor();
    }

    /// <summary>
    /// Processes keyboard shortcuts such as <c>Cmd+C</c> / <c>Ctrl+C</c> (copy selected text to <see cref="Clipboard"/>)
    /// and <c>Cmd+A</c> / <c>Ctrl+A</c> (select all).
    /// </summary>
    public async ValueTask<bool> HandleKeyDownAsync(string key, bool commandOrControl)
    {
        if (_content is null || string.IsNullOrEmpty(key))
        {
            return false;
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

        using SkiaSurface surface = new(pixelWidth, pixelHeight);
        SkiaDrawingContext drawingContext = new(surface.Canvas, SkiaTextShaper.Shared);
        LastLayout = _renderer.Render(drawingContext, _content, new Size(Width, Height), ScaleFactor, _background);

        if (_activeSelectionText is not null && LastLayout is not null)
        {
            _activeSelectionBox = FindBoxForElement(LastLayout, _activeSelectionText);
        }

        RenderCount++;

        SKImage snapshot = surface.Snapshot();
        if (_nsImageView != nint.Zero && OperatingSystem.IsMacOS())
        {
            PresentSnapshotToNativeView(snapshot);
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
        _platformSettings.ThemeChanged -= OnPlatformThemeChanged;
        HotReloadManager.HotReloaded -= OnHotReloaded;

        if (_nsImageView != nint.Zero)
        {
            AppKitInterop.RemovePointerTracking(_nsImageView);
            AppKitInterop.UnregisterWindowView(_nsImageView);
        }

        if (_content is not null)
        {
            _content.VisualInvalidated -= OnContentVisualInvalidated;
            _content = null;
        }

        _pointerRouter.Reset();
        _pointerPosition = null;
        _isDraggingSelection = false;
        _activeSelectionText = null;
        _activeSelectionBox = null;
        _renderer.Dispose();
        _nsImageView = nint.Zero;
        _nsWindow = nint.Zero;
    }

    /// <summary>
    /// Finds the topmost visible element at <paramref name="point"/>, searching children in reverse paint order.
    /// </summary>
    private static UIElement? HitTest(LayoutBox box, Point point)
    {
        if (box.Element is not UIElement { IsVisible: true } element)
        {
            return null;
        }

        for (int i = box.Children.Count - 1; i >= 0; i--)
        {
            UIElement? childMatch = HitTest(box.Children[i], point);
            if (childMatch is not null)
            {
                return childMatch;
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
        if (element is Text { IsVisible: true, IsTextSelectionEnabled: true } textControl)
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

    private void SyncContentThemeAndStyle()
    {
        if (_content is null)
        {
            return;
        }

        if (_styleResolver is not null)
        {
            _content.StyleResolver = _styleResolver;
        }

        _content.SetInheritedTheme(ActualTheme);
    }

    private void OnPlatformThemeChanged(object? sender, Theme newSystemTheme)
    {
        if (_disposed || _theme != Theme.Default)
        {
            return;
        }

        SyncContentThemeAndStyle();
        InvalidateFrame();
    }

    private void OnHotReloaded(Type[]? updatedTypes)
    {
        if (_disposed || _content is null)
        {
            return;
        }

        SyncContentThemeAndStyle();
        InvalidateFrame();
    }

    private void OnContentVisualInvalidated(object? sender, EventArgs e)
    {
        InvalidateFrame();
    }

    private UIElement? HitTest(Point position)
    {
        return LastLayout is null ? null : HitTest(LastLayout, position);
    }

    private void UpdateHoverState(UIElement? hoveredElement)
    {
        Point position = _pointerPosition ?? Point.Zero;
        _pointerRouter.UpdateHover(hoveredElement, MousePointerId, position, _pointerButtons, _pointerModifiers);
    }

    private void StorePointerState(Point position, PointerButtons buttons, PointerModifiers modifiers)
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
        _pointerModifiers = PointerModifiers.None;
        _isDraggingSelection = false;
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        Cursor cursor = _isDraggingSelection
            ? Cursor.IBeam
            : (_pointerRouter.HoveredElement?.ComputedStyle.Cursor ?? Cursor.Default);

        if (_pointerRouter.HoveredElement is Text { IsTextSelectionEnabled: true } && cursor == Cursor.Default)
        {
            cursor = Cursor.IBeam;
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
            UpdateHoverState(HitTest(position));
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

        if (_frameBatchDepth > 0)
        {
            _isFrameDirty = true;
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
            RenderInvalidatedFrame();
        }
    }

    private void RenderInvalidatedFrame()
    {
        using SKImage? snapshot = RenderFrame();
    }

    private void PresentSnapshotToNativeView(SKImage snapshot)
    {
        using SKData? encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
        {
            return;
        }

        nint nsData = AppKitInterop.CreateNSData(encoded.AsSpan());
        nint nsImageClass = AppKitInterop.GetClass("NSImage");
        nint allocImg = AppKitInterop.SendMessage(nsImageClass, AppKitInterop.GetSelector("alloc"));
        nint nsImage = AppKitInterop.SendMessageImageInitWithData(
            allocImg,
            AppKitInterop.GetSelector("initWithData:"),
            nsData);

        if (nsImage != nint.Zero)
        {
            AppKitInterop.SendMessageSetSize(
                nsImage,
                AppKitInterop.GetSelector("setSize:"),
                new AppKitInterop.CGSize(Width, Height));
            AppKitInterop.SendMessageVoid(_nsImageView, AppKitInterop.GetSelector("setImage:"), nsImage);
        }
    }
}
