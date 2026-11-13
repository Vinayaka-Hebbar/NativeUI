using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Popover"/> with a custom element. The element has <c>data-open</c> while the popover is open
/// and <c>data-closed</c> while it is closed, which the compiled CSS for <c>:popover-open</c> selects. The target element
/// has <c>aria-haspopup="dialog"</c> and <c>aria-expanded</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomPopover : HtmlDomView
{
    private readonly Popover _popover;
    private UIElement? _target;

    internal HtmlDomPopover(Popover popover, JSObject? element, string tagName)
        : base(popover, element, tagName)
    {
        _popover = popover;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateTarget();
        UpdateOpenState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Popover.TargetProperty))
        {
            UpdateTarget();
            UpdateOpenState();
        }
        else if (ReferenceEquals(args.Property, Popover.IsOpenProperty))
        {
            UpdateOpenState();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        SetTargetAttributes(_target, isOpen: null);
        _target = null;
        base.OnDisposing();
    }

    private void UpdateTarget()
    {
        SetTargetAttributes(_target, isOpen: null);
        _target = _popover.Target;
    }

    private void UpdateOpenState()
    {
        SetAttribute("data-open", _popover.GetAttribute("data-open"));
        SetAttribute("data-closed", _popover.GetAttribute("data-closed"));
        SetTargetAttributes(_target, _popover.IsOpen);
    }

    /// <summary>
    /// Writes the popup attributes on the target's element, or removes them when <paramref name="isOpen"/> is <see langword="null"/>.
    /// </summary>
    private static void SetTargetAttributes(UIElement? target, bool? isOpen)
    {
        if (target?.NativeHandle is not HtmlDomElement targetElement)
        {
            return;
        }

        targetElement.SetAttribute("aria-haspopup", isOpen is null ? null : "dialog");
        targetElement.SetAttribute("aria-expanded", isOpen switch
        {
            true => "true",
            false => "false",
            null => null
        });
    }
}
