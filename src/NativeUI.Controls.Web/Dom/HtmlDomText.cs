using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes read-only text, selection settings, and accessibility attributes with a span element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomText : HtmlDomElement
{
    private readonly Text _text;

    internal HtmlDomText(Text text, JSObject? element) : base(text, element, "span")
    {
        _text = text;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("role", "text");
        SetAttribute("aria-label", _text.AriaLabel);
        UpdateSelection();
        SetTextContent(_text.Value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Text.ValueProperty))
        {
            SetTextContent(_text.Value);
        }
        else if (ReferenceEquals(args.Property, Text.AriaLabelProperty))
        {
            SetAttribute("aria-label", _text.AriaLabel);
        }
        else if (ReferenceEquals(args.Property, Text.IsTextSelectionEnabledProperty))
        {
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        SetStyleProperty("user-select", _text.IsTextSelectionEnabled ? null : "none");
    }
}
