using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes shared radio selection and group tab navigation with an HTML custom element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomRadioButton : HtmlDomControl
{
    private readonly RadioButton _radio;
    private UIElement? _root;

    internal HtmlDomRadioButton(RadioButton radio, JSObject? element, string tagName)
        : base(radio, element, tagName)
    {
        _radio = radio;
    }

    /// <inheritdoc />
    protected override string Role => "radio";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        _root = _radio.VisualRoot;
        if (_root is not null)
        {
            _root.VisualInvalidated += OnVisualInvalidated;
        }

        UpdateChecked();
    }

    /// <inheritdoc />
    protected override void UpdateTabIndex()
    {
        if (Source is RadioGroupItem item)
        {
            SetAttribute("tabindex", item.IsTabStop
                ? item.TabIndex.ToString(CultureInfo.InvariantCulture)
                : "-1");
        }
        else
        {
            base.UpdateTabIndex();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, RadioGroupItem.IsTabStopProperty))
        {
            UpdateTabIndex();
        }

        if (ReferenceEquals(args.Property, RadioButton.IsCheckedProperty) ||
            ReferenceEquals(args.Property, RadioButton.RequiredProperty))
        {
            UpdateChecked();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        if (_root is not null)
        {
            _root.VisualInvalidated -= OnVisualInvalidated;
            _root = null;
        }

        base.OnDisposing();
    }

    private void OnVisualInvalidated(object? sender, EventArgs args)
    {
        UpdateTabIndex();
    }

    private void UpdateChecked()
    {
        SetAttribute("aria-checked", _radio.IsChecked == true ? "true" : "false");
        SetAttribute("aria-required", _radio.Required ? "true" : null);
        UpdateTabIndex();
    }
}
