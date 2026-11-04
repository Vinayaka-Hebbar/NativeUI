using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Toggle"/> with a custom element that has the ARIA <c>button</c> role. <c>aria-pressed</c>
/// carries <see cref="Toggle.IsPressed"/>, which the browser CSS for <c>:pressed</c> and <c>:unpressed</c> selects.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomToggle : HtmlDomControl
{
    private readonly Toggle _toggle;

    internal HtmlDomToggle(Toggle toggle, JSObject? element, string tagName)
        : base(toggle, element, tagName)
    {
        _toggle = toggle;
    }

    /// <inheritdoc />
    protected override string Role => "button";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdatePressed();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Toggle.IsPressedProperty))
        {
            UpdatePressed();
        }
    }

    private void UpdatePressed()
    {
        SetAttribute("aria-pressed", _toggle.IsPressed ? "true" : "false");
    }
}
