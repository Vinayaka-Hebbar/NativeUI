using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="CheckBox"/> with a custom element that has the ARIA <c>checkbox</c> role. <c>aria-checked</c>
/// carries <see cref="CheckBox.IsChecked"/> as <c>true</c>, <c>false</c>, or <c>mixed</c>, which the browser CSS for
/// <c>:checked</c>, <c>:unchecked</c>, and <c>:indeterminate</c> selects.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomCheckBox : HtmlDomControl
{
    private readonly CheckBox _checkBox;

    internal HtmlDomCheckBox(CheckBox checkBox, JSObject? element, string tagName)
        : base(checkBox, element, tagName)
    {
        _checkBox = checkBox;
    }

    /// <inheritdoc />
    protected override string Role => "checkbox";

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
        if (ReferenceEquals(args.Property, CheckBox.IsCheckedProperty) || ReferenceEquals(args.Property, CheckBox.RequiredProperty))
        {
            UpdateChecked();
        }
    }

    private void UpdateChecked()
    {
        string state = _checkBox.IsChecked switch
        {
            true => "true",
            false => "false",
            null => "mixed"
        };
        SetAttribute("aria-checked", state);
        SetAttribute("aria-required", _checkBox.Required ? "true" : null);
    }
}
