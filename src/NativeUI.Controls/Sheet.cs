using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Presents content against an edge of the hosted viewport in a modal dialog.
/// The template exposes a full-viewport <c>popup</c> and an edge-aligned <c>panel</c> part.
/// </summary>
public class Sheet : ContentControl
{
    /// <summary>
    /// Identifies whether the sheet is open.
    /// </summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Create<Sheet, bool>(
            nameof(IsOpen),
            propertyChanged: static (owner, _, _) => owner.UpdateOpenState());

    /// <summary>
    /// Identifies the edge against which the content is placed.
    /// </summary>
    public static readonly BindableProperty<Dock> PlacementProperty =
        BindableProperty.Create<Sheet, Dock>(
            nameof(Placement),
            defaultValue: Dock.Right,
            propertyChanged: static (owner, _, _) => owner.UpdatePlacement());

    /// <summary>
    /// Identifies the accessible title of the sheet.
    /// </summary>
    public static readonly BindableProperty<string?> TitleProperty =
        BindableProperty.Create<Sheet, string?>(nameof(Title));

    private Dialog _popup = null!;
    private View _content = null!;
    private UIElement? _previousFocus;

    /// <summary>
    /// Creates a sheet and connects its dialog lifecycle.
    /// </summary>
    public Sheet()
    {
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _popup.TransitionStatusChanged += OnTransitionStatusChanged;
    }

    /// <summary>
    /// Occurs when the sheet opens.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Occurs when the sheet closes, before its exit transition finishes.
    /// </summary>
    public event EventHandler? Closed;

    /// <inheritdoc />
    public override string TagName => "sheet";

    /// <summary>
    /// Gets or sets whether the sheet is open. Supports two-way binding.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the viewport edge. Defaults to <see cref="Dock.Right"/>.
    /// CSS supplies the content width for side sheets or height for top and bottom sheets.
    /// </summary>
    public Dock Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets the accessible name of the dialog. Visible headings belong in <see cref="ContentControl.Content"/>.
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        if (name == "data-side")
        {
            return Placement switch
            {
                Dock.Left => "left",
                Dock.Top => "top",
                Dock.Bottom => "bottom",
                _ => "right"
            };
        }

        if (name is "data-open" or "data-closed" or "data-starting-style" or "data-ending-style")
        {
            return _popup.GetAttribute(name);
        }

        return base.GetAttribute(name);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _popup = DefinePart("popup", new Dialog
        {
            HasOwnWindow = false,
            Width = WidthValue.Auto,
            Left = 0,
            Right = 0,
            Top = 0,
            Bottom = 0,
            Focusable = true
        });
        KeyboardNavigation.SetTabNavigation(_popup, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetIsTabStop(_popup, false);

        _content = DefinePart("panel", new View
        {
            Position = NativeUI.Layout.Position.Fixed
        });
        _popup.Content.Add(_content);
        Children.Add(_popup);
        UpdatePlacement();
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return _content;
    }

    /// <summary>
    /// Gets the dialog that owns the modal lifecycle and transition state.
    /// </summary>
    internal Dialog PopupPart => _popup;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (IsOpen && args.Key == Key.Escape)
        {
            args.Handled = true;
            IsOpen = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (IsOpen && args.Buttons.HasFlag(PointerButtons.Primary) &&
            args.Source is UIElement source && !_content.IsAncestorOf(source))
        {
            args.Handled = true;
            IsOpen = false;
        }
    }

    /// <inheritdoc />
    internal override void AttachToVisualTree(UIElement root)
    {
        if (ReferenceEquals(VisualRoot, root))
        {
            return;
        }

        base.AttachToVisualTree(root);
        if (IsOpen && ReferenceEquals(VisualRoot, root))
        {
            FocusContent();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        IsOpen = false;
        _previousFocus = null;
        base.OnDetachedFromVisualTree();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsOpen = false;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            _popup.TransitionStatusChanged -= OnTransitionStatusChanged;
        }

        base.Dispose(disposing);
    }

    private void UpdatePlacement()
    {
        _content.Left = Placement == Dock.Right ? null : 0;
        _content.Right = Placement == Dock.Left ? null : 0;
        _content.Top = Placement == Dock.Bottom ? null : 0;
        _content.Bottom = Placement == Dock.Top ? null : 0;
        InvalidateStyle();
    }

    private void UpdateOpenState()
    {
        if (!IsOpen)
        {
            _popup.Close();
            return;
        }

        _previousFocus = InputManager.Current.PrimaryKeyboardDevice.FocusedElement;
        _popup.Show();
        if (IsOpen && _popup.IsOpen)
        {
            FocusContent();
            if (IsOpen && _popup.IsOpen)
            {
                Opened?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void FocusContent()
    {
        if (!_content.MoveFocus(FocusNavigationDirection.First))
        {
            _popup.Focus();
        }
    }

    private void OnPopupClosing(object? sender, EventArgs args)
    {
        UIElement? previous = _previousFocus;
        _previousFocus = null;
        if (_popup.States.HasFlag(ElementStates.FocusWithin) && previous is not null &&
            ReferenceEquals(previous.VisualRoot, VisualRoot))
        {
            previous.Focus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs args)
    {
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTransitionStatusChanged(object? sender, EventArgs args)
    {
        InvalidateStyle();
    }
}
