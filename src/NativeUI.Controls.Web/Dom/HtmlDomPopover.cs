using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Popover"/> with a custom element. The element has <c>data-open</c> while the popover is open
/// and <c>data-closed</c> while it is closed, which the compiled CSS for <c>:popover-open</c> selects, and the motion
/// attributes of its content part. The target element
/// has <c>data-popup-open</c> while the popover is open, and <c>aria-haspopup="dialog"</c>, <c>aria-expanded</c>, and
/// <c>aria-controls</c> with the id of the content element.
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
        _popover.ContentPart.MotionChanged += OnMotionChanged;
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
        _popover.ContentPart.MotionChanged -= OnMotionChanged;
        SetTargetAttributes(_target, isOpen: null, contentId: null);
        _target = null;
        base.OnDisposing();
    }

    private void UpdateTarget()
    {
        SetTargetAttributes(_target, isOpen: null, contentId: null);
        _target = _popover.Target;
    }

    private void OnMotionChanged(object? sender, EventArgs args)
    {
        SetAttribute("data-starting-style", _popover.GetAttribute("data-starting-style"));
        SetAttribute("data-ending-style", _popover.GetAttribute("data-ending-style"));
    }

    private void UpdateOpenState()
    {
        SetAttribute("data-open", _popover.GetAttribute("data-open"));
        SetAttribute("data-closed", _popover.GetAttribute("data-closed"));
        SetTargetAttributes(_target, _popover.IsOpen, (_popover.ContentPart.NativeHandle as HtmlDomPortal)?.ElementId);
    }

    /// <summary>
    /// Writes the popup attributes on the target's element, or removes them when <paramref name="isOpen"/> is <see langword="null"/>.
    /// </summary>
    private static void SetTargetAttributes(UIElement? target, bool? isOpen, string? contentId)
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
        targetElement.SetAttribute("aria-controls", isOpen is null ? null : contentId);
        targetElement.SetAttribute("data-popup-open", target.GetAttribute("data-popup-open"));
    }
}
