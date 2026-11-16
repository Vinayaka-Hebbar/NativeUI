using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Select"/> control with a custom element. The element has <c>role="combobox"</c>,
/// <c>aria-expanded</c>, <c>data-open</c>, <c>data-closed</c>, and <c>data-placeholder</c> attributes matching the
/// select's state, and accessibility attributes for validation.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSelect : HtmlDomControl
{
    private readonly Select _select;

    internal HtmlDomSelect(Select select, JSObject? element, string tagName)
        : base(select, element, tagName)
    {
        _select = select;
    }

    /// <inheritdoc />
    protected override string Role => "combobox";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("aria-haspopup", "listbox");
        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Select.IsDropDownOpenProperty) ||
            ReferenceEquals(args.Property, Selector.SelectedIndexProperty) ||
            ReferenceEquals(args.Property, Select.PlaceholderProperty) ||
            ReferenceEquals(args.Property, Select.RequiredProperty))
        {
            UpdateAttributes();
        }
    }

    private void UpdateAttributes()
    {
        SetAttribute("data-open", _select.GetAttribute("data-open"));
        SetAttribute("data-closed", _select.GetAttribute("data-closed"));
        SetAttribute("data-placeholder", _select.GetAttribute("data-placeholder"));
        SetAttribute("aria-expanded", _select.IsDropDownOpen ? "true" : "false");
        SetAttribute("aria-required", _select.Required ? "true" : null);
    }
}
