using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Button"/> with a native HTML <c>&lt;button type="button"&gt;</c> element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomButton : HtmlDomView
{
    private readonly Button _button;

    internal HtmlDomButton(Button button, JSObject? element)
        : base(button, element, "button")
    {
        _button = button;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("type", "button");
        UpdateEnabledState();
        UpdateText();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, UIElement.IsEffectivelyEnabledProperty))
        {
            UpdateEnabledState();
        }
        else if (ReferenceEquals(args.Property, Button.TextProperty))
        {
            UpdateText();
        }
    }

    private void UpdateEnabledState()
    {
        SetAttribute("disabled", _button.IsEffectivelyEnabled ? null : "disabled");
        SetAttribute("aria-disabled", _button.IsEffectivelyEnabled ? null : "true");
    }

    private void UpdateText()
    {
        if (_button.Children.Count == 0)
        {
            SetTextContent(_button.Text ?? string.Empty);
        }
    }
}
