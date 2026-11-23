using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Mounts a <see cref="Dialog"/> as a native <c>&lt;dialog&gt;</c> element with accessible ARIA references.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomDialog : HtmlDomPortal
{
    private static int s_lastDialogId;
    private readonly Dialog _dialog;
    private readonly string _titleId;
    private readonly string _descriptionId;
    private JSObject? _events;

    internal HtmlDomDialog(Dialog dialog, JSObject? element, string tagName)
        : base(dialog, element, tagName)
    {
        _dialog = dialog;
        int id = ++s_lastDialogId;
        _titleId = $"nui-dialog-title-{id}";
        _descriptionId = $"nui-dialog-description-{id}";
    }

    /// <summary>Applies a native dialog close, such as Escape, to the shared dialog.</summary>
    internal void OnBrowserClosed()
    {
        if (!IsDisposed)
        {
            _dialog.Close();
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (Element is JSObject element)
        {
            _events = DomInterop.ObserveDialog(element, OnBrowserClosed);
        }

        UpdateAriaReferences();
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        using JSObject? events = _events;
        _events = null;
        if (events is not null)
        {
            DomInterop.UnobserveDialog(events);
        }

        base.OnDisposing();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Dialog.TitleProperty) ||
            ReferenceEquals(args.Property, Dialog.TitleContentProperty) ||
            ReferenceEquals(args.Property, Dialog.DescriptionProperty) ||
            ReferenceEquals(args.Property, Dialog.DescriptionContentProperty))
        {
            UpdateAriaReferences();
        }
    }

    private void UpdateAriaReferences()
    {
        if (_dialog.TitlePart.IsVisible)
        {
            _dialog.TitlePart.Id = _titleId;
            SetAttribute("aria-labelledby", _titleId);
        }
        else
        {
            SetAttribute("aria-labelledby", null);
        }

        if (_dialog.DescriptionPart.IsVisible)
        {
            _dialog.DescriptionPart.Id = _descriptionId;
            SetAttribute("aria-describedby", _descriptionId);
        }
        else
        {
            SetAttribute("aria-describedby", null);
        }
    }
}
