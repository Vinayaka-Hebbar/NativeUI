using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Tooltip"/> with a custom element. The element has <c>data-open</c> while the tooltip is open
/// and <c>data-closed</c> while it is closed, and mirrors the placement and motion attributes of its content part. The
/// content part has <c>role="tooltip"</c>. While the tooltip is open, the target element has <c>data-popup-open</c> and
/// <c>aria-describedby</c> with the id of the content element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomTooltip : HtmlDomView
{
    private readonly Tooltip _tooltip;
    private UIElement? _target;

    internal HtmlDomTooltip(Tooltip tooltip, JSObject? element, string tagName)
        : base(tooltip, element, tagName)
    {
        _tooltip = tooltip;
        _tooltip.ContentPart.TransitionStatusChanged += OnTransitionStatusChanged;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (_tooltip.ContentPart.NativeHandle is HtmlDomElement contentElement)
        {
            contentElement.SetAttribute("role", "tooltip");
        }

        UpdateTarget();
        UpdateOpenState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Tooltip.TargetProperty))
        {
            UpdateTarget();
            UpdateOpenState();
        }
        else if (ReferenceEquals(args.Property, Tooltip.IsOpenProperty) ||
            ReferenceEquals(args.Property, Tooltip.SideProperty) ||
            ReferenceEquals(args.Property, Tooltip.AlignProperty))
        {
            UpdateOpenState();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _tooltip.ContentPart.TransitionStatusChanged -= OnTransitionStatusChanged;
        SetTargetAttributes(_target, isOpen: false, contentId: null);
        _target = null;
        base.OnDisposing();
    }

    private void UpdateTarget()
    {
        SetTargetAttributes(_target, isOpen: false, contentId: null);
        _target = _tooltip.Target;
    }

    private void OnTransitionStatusChanged(object? sender, EventArgs args)
    {
        SetAttribute("data-starting-style", _tooltip.GetAttribute("data-starting-style"));
        SetAttribute("data-ending-style", _tooltip.GetAttribute("data-ending-style"));
        SetAttribute("data-side", _tooltip.GetAttribute("data-side"));
        SetAttribute("data-align", _tooltip.GetAttribute("data-align"));
    }

    private void UpdateOpenState()
    {
        SetAttribute("data-open", _tooltip.GetAttribute("data-open"));
        SetAttribute("data-closed", _tooltip.GetAttribute("data-closed"));
        SetAttribute("data-side", _tooltip.GetAttribute("data-side"));
        SetAttribute("data-align", _tooltip.GetAttribute("data-align"));
        SetTargetAttributes(_target, _tooltip.IsOpen, (_tooltip.ContentPart.NativeHandle as HtmlDomPortal)?.ElementId);
    }

    private static void SetTargetAttributes(UIElement? target, bool isOpen, string? contentId)
    {
        if (target?.NativeHandle is not HtmlDomElement targetElement)
        {
            return;
        }

        targetElement.SetAttribute("aria-describedby", isOpen ? contentId : null);
        targetElement.SetAttribute("data-popup-open", target.GetAttribute("data-popup-open"));
    }
}
