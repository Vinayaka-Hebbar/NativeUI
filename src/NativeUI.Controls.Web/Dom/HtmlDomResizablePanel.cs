using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ResizablePanel"/> with CSS flex layout declarations.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomResizablePanel : HtmlDomView
{
    private readonly ResizablePanel _panel;

    internal HtmlDomResizablePanel(ResizablePanel panel, JSObject? element, string tagName)
        : base(panel, element, tagName)
    {
        _panel = panel;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdatePanelLayout();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, ResizablePanel.SizeProperty) ||
            ReferenceEquals(args.Property, ResizablePanel.DefaultSizeProperty) ||
            ReferenceEquals(args.Property, ResizablePanel.MinSizeProperty) ||
            ReferenceEquals(args.Property, ResizablePanel.MaxSizeProperty) ||
            ReferenceEquals(args.Property, ResizablePanel.IsCollapsedProperty) ||
            ReferenceEquals(args.Property, ResizablePanel.CollapsedSizeProperty))
        {
            UpdatePanelLayout();
        }
    }

    private void UpdatePanelLayout()
    {
        if (Element is null)
        {
            return;
        }

        if (_panel.IsCollapsed)
        {
            DomInterop.SetStyleProperty(Element, "display", "none");
        }
        else
        {
            DomInterop.RemoveStyleProperty(Element, "display");
            double effectiveSize = _panel.EffectiveSize;
            DomInterop.SetStyleProperty(Element, "flex-grow", effectiveSize.ToString(CultureInfo.InvariantCulture));
            DomInterop.SetStyleProperty(Element, "flex-shrink", "1");
            DomInterop.SetStyleProperty(Element, "flex-basis", "0px");

            Orientation orientation = (_panel.Parent as Resizable)?.Orientation ?? Orientation.Horizontal;
            if (orientation == Orientation.Horizontal)
            {
                if (_panel.MinSize > 0)
                {
                    DomInterop.SetStyleProperty(Element, "min-width", $"{_panel.MinSize.ToString(CultureInfo.InvariantCulture)}%");
                }
                else
                {
                    DomInterop.RemoveStyleProperty(Element, "min-width");
                }

                if (_panel.MaxSize < 100)
                {
                    DomInterop.SetStyleProperty(Element, "max-width", $"{_panel.MaxSize.ToString(CultureInfo.InvariantCulture)}%");
                }
                else
                {
                    DomInterop.RemoveStyleProperty(Element, "max-width");
                }

                DomInterop.RemoveStyleProperty(Element, "min-height");
                DomInterop.RemoveStyleProperty(Element, "max-height");
            }
            else
            {
                if (_panel.MinSize > 0)
                {
                    DomInterop.SetStyleProperty(Element, "min-height", $"{_panel.MinSize.ToString(CultureInfo.InvariantCulture)}%");
                }
                else
                {
                    DomInterop.RemoveStyleProperty(Element, "min-height");
                }

                if (_panel.MaxSize < 100)
                {
                    DomInterop.SetStyleProperty(Element, "max-height", $"{_panel.MaxSize.ToString(CultureInfo.InvariantCulture)}%");
                }
                else
                {
                    DomInterop.RemoveStyleProperty(Element, "max-height");
                }

                DomInterop.RemoveStyleProperty(Element, "min-width");
                DomInterop.RemoveStyleProperty(Element, "max-width");
            }
        }

        SetAttribute("data-collapsed", _panel.GetAttribute("data-collapsed"));
    }
}
