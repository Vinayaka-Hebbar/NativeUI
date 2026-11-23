using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a context menu and owns native menu suppression on its target element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomContextMenu : HtmlDomPopover
{
    private readonly ContextMenu _contextMenu;
    private JSObject? _targetEvents;

    internal HtmlDomContextMenu(ContextMenu contextMenu, JSObject? element, string tagName)
        : base(contextMenu, element, tagName)
    {
        _contextMenu = contextMenu;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetTarget(_contextMenu.Target);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Popover.TargetProperty))
        {
            SetTarget(_contextMenu.Target);
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        SetTarget(null);
        base.OnDisposing();
    }

    private void SetTarget(UIElement? target)
    {
        using JSObject? previous = _targetEvents;
        _targetEvents = null;
        if (previous is not null)
        {
            DomInterop.UnobserveContextMenu(previous);
        }

        JSObject? element = (target?.NativeHandle as HtmlDomElement)?.Element;
        if (element is not null)
        {
            _targetEvents = DomInterop.ObserveContextMenu(element);
        }
    }
}
