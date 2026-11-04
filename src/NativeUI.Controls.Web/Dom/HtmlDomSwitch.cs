using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Switch"/> with a custom element that has the ARIA <c>switch</c> role. <c>aria-checked</c>
/// carries <see cref="Switch.IsChecked"/>, which the browser CSS for <c>:checked</c> and <c>:unchecked</c> selects.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSwitch : HtmlDomControl
{
    private readonly Switch _switch;

    internal HtmlDomSwitch(Switch toggleSwitch, JSObject? element, string tagName)
        : base(toggleSwitch, element, tagName)
    {
        _switch = toggleSwitch;
    }

    /// <inheritdoc />
    protected override string Role => "switch";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateChecked();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Switch.IsCheckedProperty))
        {
            UpdateChecked();
        }
    }

    private void UpdateChecked()
    {
        SetAttribute("aria-checked", _switch.IsChecked ? "true" : "false");
    }
}
