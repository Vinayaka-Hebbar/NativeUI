using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Displays menu items on a secondary press at the pointer position or on the Menu key or Shift+F10 at its target.
/// </summary>
public class ContextMenu : DropdownMenu
{
    private Point? _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMenu"/> class.
    /// </summary>
    public ContextMenu()
    {
        Align = Alignment.Start;
    }

    /// <inheritdoc />
    public override string TagName => "contextmenu";

    /// <inheritdoc />
    protected override void AttachTargetInput(UIElement target)
    {
        target.PointerPressed += OnTargetPointerPressed;
        target.KeyDown += OnTargetKeyDown;
    }

    /// <inheritdoc />
    protected override void DetachTargetInput(UIElement target)
    {
        target.PointerPressed -= OnTargetPointerPressed;
        target.KeyDown -= OnTargetKeyDown;
    }

    /// <inheritdoc />
    private protected override void ShowContent()
    {
        if (_position is Point position)
        {
            _portal.Show(position);
        }
        else
        {
            base.ShowContent();
        }
    }

    private void OnTargetPointerPressed(object? sender, PointerEventArgs args)
    {
        if (args.Handled || Target is not { IsEffectivelyEnabled: true } || !args.Buttons.HasFlag(PointerButtons.Secondary))
        {
            return;
        }

        args.Handled = true;
        _position = args.Position;
        if (IsOpen)
        {
            ShowContent();
            FocusContent();
        }
        else
        {
            IsOpen = true;
        }
    }

    private void OnTargetKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || Target is not { IsEffectivelyEnabled: true })
        {
            return;
        }

        if (args.Key != Key.ContextMenu && !(args.Key == Key.F10 && args.Modifiers == InputModifiers.Shift))
        {
            return;
        }

        args.Handled = true;
        _position = null;
        if (IsOpen)
        {
            ShowContent();
            FocusContent();
        }
        else
        {
            IsOpen = true;
        }
    }
}
