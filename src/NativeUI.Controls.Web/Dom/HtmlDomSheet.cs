using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a sheet's placement and transition attributes with its native dialog subtree.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSheet : HtmlDomView
{
    private readonly Sheet _sheet;

    internal HtmlDomSheet(Sheet sheet, JSObject? element, string tagName)
        : base(sheet, element, tagName)
    {
        _sheet = sheet;
        sheet.PopupPart.TransitionStatusChanged += OnTransitionStatusChanged;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Sheet.PlacementProperty) ||
            ReferenceEquals(args.Property, Sheet.IsOpenProperty) ||
            ReferenceEquals(args.Property, Sheet.TitleProperty))
        {
            UpdateAttributes();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _sheet.PopupPart.TransitionStatusChanged -= OnTransitionStatusChanged;
        base.OnDisposing();
    }

    private void OnTransitionStatusChanged(object? sender, EventArgs args)
    {
        UpdateAttributes();
    }

    private void UpdateAttributes()
    {
        if (_sheet.PopupPart.NativeHandle is HtmlDomElement popup)
        {
            popup.SetAttribute("aria-label", _sheet.Title);
        }

        SetAttribute("data-side", _sheet.GetAttribute("data-side"));
        SetAttribute("data-open", _sheet.GetAttribute("data-open"));
        SetAttribute("data-closed", _sheet.GetAttribute("data-closed"));
        SetAttribute("data-starting-style", _sheet.GetAttribute("data-starting-style"));
        SetAttribute("data-ending-style", _sheet.GetAttribute("data-ending-style"));
    }
}
