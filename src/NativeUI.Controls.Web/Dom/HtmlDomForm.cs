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
    internal HtmlDomForm(Form form, JSObject? element) : base(form, element, "form") => _form = form;

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("novalidate", string.Empty);
        UpdateBusy();
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
