using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents the interactive divider between adjacent <see cref="ResizablePanel"/> elements in a <see cref="Resizable"/> container.
/// </summary>
public class ResizableHandle : Control
{
    /// <summary>
    /// The name of the optional thumb indicator template part.
    /// </summary>
    public const string ThumbPartName = "thumb";

    /// <summary>
    /// Identifies the <see cref="Disabled"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> DisabledProperty =
        BindableProperty.Create<ResizableHandle, bool>(
            nameof(Disabled),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    /// <summary>
    /// Identifies the <see cref="KeyboardIncrement"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> KeyboardIncrementProperty =
        BindableProperty.Create<ResizableHandle, double>(
            nameof(KeyboardIncrement),
            defaultValue: 5.0);

    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation?> OrientationProperty =
        BindableProperty.Create<ResizableHandle, Orientation?>(
            nameof(Orientation),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateOrientation());

    /// <summary>
    /// Identifies the <see cref="WithHandle"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> WithHandleProperty =
        BindableProperty.Create<ResizableHandle, bool>(
            nameof(WithHandle),
            defaultValue: false,
            propertyChanged: static (owner, _, withHandle) => owner.OnWithHandleChanged(withHandle));

    /// <summary>
    /// Identifies the <see cref="IsDragging"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsDraggingProperty =
        BindableProperty.Create<ResizableHandle, bool>(
            nameof(IsDragging),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    private long _activePointerId;
    private Point _dragStartPosition;
    private ResizablePanel? _dragPrevPanel;
    private ResizablePanel? _dragNextPanel;
    private double _dragPrevStartSize;
    private double _dragNextStartSize;
    private double _dragTotalMainLength;
    private View? _thumb;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResizableHandle"/> class.
    /// </summary>
    public ResizableHandle()
    {
        Focusable = true;
        TabIndex = 0;

        PointerPressed += OnHandlePointerPressed;
        PointerMoved += OnHandlePointerMoved;
        PointerReleased += OnHandlePointerReleased;
        PointerCaptureLost += OnHandlePointerCaptureLost;

        UpdateOrientation();
    }

    /// <inheritdoc />
    public override string TagName => "resizablehandle";

    /// <summary>
    /// Gets or sets a value indicating whether dragging and resizing are disabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Disabled
    {
        get => GetValue(DisabledProperty);
        set => SetValue(DisabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the percentage amount adjusted on arrow key presses. Defaults to 5.0.
    /// </summary>
    public double KeyboardIncrement
    {
        get => GetValue(KeyboardIncrementProperty);
        set => SetValue(KeyboardIncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets an explicit orientation for this handle, or <see langword="null"/> to inherit from the parent <see cref="Resizable"/>.
    /// </summary>
    public Orientation? Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a visual thumb grip is displayed within the handle. Defaults to <see langword="false"/>.
    /// </summary>
    public bool WithHandle
    {
        get => GetValue(WithHandleProperty);
        set => SetValue(WithHandleProperty, value);
    }

    /// <summary>
    /// Gets whether this handle is actively being dragged.
    /// </summary>
    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>
    /// Gets the effective orientation resolved from local property or parent <see cref="Resizable"/>.
    /// </summary>
    public Orientation EffectiveOrientation
    {
        get
        {
            if (Orientation is Orientation explicitOrientation)
            {
                return explicitOrientation;
            }

            if (Parent is Resizable resizable)
            {
                return resizable.Orientation;
            }

            return Layout.Orientation.Horizontal;
        }
    }

    /// <inheritdoc />
    public override Cursor Cursor => ComputedStyle.Cursor != Cursor.Auto
        ? ComputedStyle.Cursor
        : (EffectiveOrientation == Layout.Orientation.Horizontal ? Cursor.ColumnResize : Cursor.RowResize);

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _thumb = DefinePart(ThumbPartName, new View { IsVisible = false });
        Children.Add(_thumb);
    }

    /// <inheritdoc />
    protected override void OnParentChanged()
    {
        base.OnParentChanged();
        UpdateOrientation();
    }

    /// <summary>
    /// Updates cursor and styling according to current effective orientation.
    /// </summary>
    internal void UpdateOrientation()
    {
        InvalidateStyle();
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-orientation", StringComparison.Ordinal))
        {
            return EffectiveOrientation == Layout.Orientation.Vertical ? "vertical" : "horizontal";
        }

        if (string.Equals(name, "data-dragging", StringComparison.Ordinal))
        {
            return IsDragging ? "true" : "false";
        }

        if (string.Equals(name, "data-disabled", StringComparison.Ordinal))
        {
            return Disabled ? "true" : "false";
        }

        return base.GetAttribute(name);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Handled)
        {
            return;
        }

        if (args.Key == Key.Escape && IsDragging)
        {
            if (_dragPrevPanel is not null)
            {
                _dragPrevPanel.Size = _dragPrevStartSize;
            }

            if (_dragNextPanel is not null)
            {
                _dragNextPanel.Size = _dragNextStartSize;
            }

            ReleasePointerCapture(_activePointerId);
            IsDragging = false;
            _dragPrevPanel = null;
            _dragNextPanel = null;
            InvalidateStyle();
            args.Handled = true;
            return;
        }

        if (Disabled)
        {
            return;
        }

        if (args.Key is Key.ArrowLeft or Key.ArrowUp)
        {
            StepAdjacentPanels(-KeyboardIncrement);
            args.Handled = true;
        }
        else if (args.Key is Key.ArrowRight or Key.ArrowDown)
        {
            StepAdjacentPanels(KeyboardIncrement);
            args.Handled = true;
        }
        else if (args.Key == Key.Home)
        {
            CollapseOrMinPrevious();
            args.Handled = true;
        }
        else if (args.Key == Key.End)
        {
            ExpandOrMaxPrevious();
            args.Handled = true;
        }
    }

    private void OnHandlePointerPressed(object? sender, PointerEventArgs args)
    {
        if (Disabled || !args.Buttons.HasFlag(PointerButtons.Primary))
        {
            return;
        }

        Resizable? resizable = Parent as Resizable;
        if (resizable is null)
        {
            return;
        }

        resizable.GetAdjacentPanels(this, out ResizablePanel? prev, out ResizablePanel? next);
        if (prev is null || next is null)
        {
            return;
        }

        if (CapturePointer(args.PointerId))
        {
            IsDragging = true;
            _activePointerId = args.PointerId;
            _dragStartPosition = args.Position;
            _dragPrevPanel = prev;
            _dragNextPanel = next;
            _dragPrevStartSize = prev.EffectiveSize;
            _dragNextStartSize = next.EffectiveSize;
            _dragTotalMainLength = resizable.GetTotalMainLength();

            InvalidateStyle();
            args.Handled = true;
        }
    }

    private void OnHandlePointerMoved(object? sender, PointerEventArgs args)
    {
        if (!IsDragging || args.PointerId != _activePointerId || _dragPrevPanel is null || _dragNextPanel is null || _dragTotalMainLength <= 0)
        {
            return;
        }

        double deltaPx = EffectiveOrientation == Layout.Orientation.Horizontal
            ? (args.Position.X - _dragStartPosition.X)
            : (args.Position.Y - _dragStartPosition.Y);

        double deltaPercent = (deltaPx / _dragTotalMainLength) * 100.0;
        ApplyPanelResize(_dragPrevPanel, _dragNextPanel, _dragPrevStartSize, _dragNextStartSize, deltaPercent);

        args.Handled = true;
    }

    private void OnHandlePointerReleased(object? sender, PointerEventArgs args)
    {
        if (IsDragging && args.PointerId == _activePointerId)
        {
            ReleasePointerCapture(args.PointerId);
            IsDragging = false;
            _dragPrevPanel = null;
            _dragNextPanel = null;
            InvalidateStyle();
            args.Handled = true;
        }
    }

    private void OnHandlePointerCaptureLost(object? sender, PointerEventArgs args)
    {
        if (IsDragging)
        {
            IsDragging = false;
            _dragPrevPanel = null;
            _dragNextPanel = null;
            InvalidateStyle();
        }
    }

    private void StepAdjacentPanels(double deltaPercent)
    {
        Resizable? resizable = Parent as Resizable;
        if (resizable is null)
        {
            return;
        }

        resizable.GetAdjacentPanels(this, out ResizablePanel? prev, out ResizablePanel? next);
        if (prev is null || next is null)
        {
            return;
        }

        ApplyPanelResize(prev, next, prev.EffectiveSize, next.EffectiveSize, deltaPercent);
    }

    private void CollapseOrMinPrevious()
    {
        Resizable? resizable = Parent as Resizable;
        if (resizable is null)
        {
            return;
        }

        resizable.GetAdjacentPanels(this, out ResizablePanel? prev, out ResizablePanel? next);
        if (prev is null || next is null)
        {
            return;
        }

        double combined = prev.EffectiveSize + next.EffectiveSize;
        double targetPrev = prev.MinSize;
        double targetNext = combined - targetPrev;

        prev.Size = targetPrev;
        next.Size = targetNext;
        (Parent as UIElement)?.InvalidateVisual();
    }

    private void ExpandOrMaxPrevious()
    {
        Resizable? resizable = Parent as Resizable;
        if (resizable is null)
        {
            return;
        }

        resizable.GetAdjacentPanels(this, out ResizablePanel? prev, out ResizablePanel? next);
        if (prev is null || next is null)
        {
            return;
        }

        double combined = prev.EffectiveSize + next.EffectiveSize;
        double targetPrev = Math.Min(prev.MaxSize, combined - next.MinSize);
        double targetNext = combined - targetPrev;

        prev.Size = targetPrev;
        next.Size = targetNext;
        (Parent as UIElement)?.InvalidateVisual();
    }

    private static void ApplyPanelResize(
        ResizablePanel prev,
        ResizablePanel next,
        double startPrev,
        double startNext,
        double deltaPercent)
    {
        double combined = startPrev + startNext;
        double minPrev = Math.Max(prev.MinSize, combined - next.MaxSize);
        double maxPrev = Math.Min(prev.MaxSize, combined - next.MinSize);
        double clampedPrev = Math.Clamp(startPrev + deltaPercent, minPrev, maxPrev);
        double clampedNext = combined - clampedPrev;

        prev.Size = clampedPrev;
        next.Size = clampedNext;
        (prev.Parent as UIElement)?.InvalidateVisual();
    }

    private void OnWithHandleChanged(bool withHandle)
    {
        if (_thumb is not null)
        {
            _thumb.IsVisible = withHandle;
        }
    }
}
