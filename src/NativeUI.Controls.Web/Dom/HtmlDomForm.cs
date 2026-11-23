using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Maps a shared form to a native HTML form with C#-coordinated validation and submission.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomForm : HtmlDomView
{
    private readonly Form _form;
    private JSObject? _events;
    internal HtmlDomForm(Form form, JSObject? element) : base(form, element, "form") => _form = form;

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        if (Element is not null)
        {
            _events = DomInterop.ObserveForm(Element, OnFormEvent);
        }
        SetAttribute("novalidate", string.Empty);
        UpdateBusy();
    }

    /// <summary>Routes a native form action through the shared validation and submission pipeline.</summary>
    internal void OnBrowserAction(int kind, Button? submitter)
    {
        if (IsDisposed || (submitter is not null && !ReferenceEquals(Form.GetForm(submitter), _form)))
        {
            return;
        }

        if (kind == 1)
        {
            _form.ResetForm();
        }
        else if (kind == 2 && submitter is not null)
        {
            submitter.PerformClick();
        }
        else if (kind == 0)
        {
            _form.RequestSubmit(submitter);
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        using JSObject? events = _events;
        _events = null;
        if (events is not null)
        {
            DomInterop.UnobserveForm(events);
        }

        base.OnDisposing();
    }

    private void OnFormEvent(int kind, int submitterId)
    {
        if (IsDisposed)
        {
            return;
        }

        Button? submitter = FindByNodeId(submitterId)?.Source as Button;
        if (submitterId != 0 && submitter is null)
        {
            return;
        }

        OnBrowserAction(kind, submitter);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Form.IsSubmittingProperty))
        {
            UpdateBusy();
        }
    }
    private void UpdateBusy() => SetAttribute("aria-busy", _form.IsSubmitting ? "true" : null);
}
