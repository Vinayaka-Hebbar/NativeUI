using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ProgressBar"/> with an element exposing the ARIA <c>progressbar</c> role. The element is a
/// <c>div</c> and not <c>&lt;progress&gt;</c>, so the same stylesheet rules style its fill on every platform.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomProgressBar : HtmlDomView
{
    private readonly ProgressBar _progressBar;

    internal HtmlDomProgressBar(ProgressBar progressBar, JSObject? element)
        : base(progressBar, element)
    {
        _progressBar = progressBar;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute(StyleSheetCss.ProgressBarAttribute, string.Empty);
        SetAttribute("role", "progressbar");
        SetAttribute("aria-valuemin", "0");
        UpdateAriaValues();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, ProgressBar.ValueProperty) ||
            ReferenceEquals(args.Property, ProgressBar.MaximumProperty) ||
            ReferenceEquals(args.Property, ProgressBar.IsIndeterminateProperty))
        {
            UpdateAriaValues();
        }
    }

    private void UpdateAriaValues()
    {
        SetAttribute("aria-valuemax", _progressBar.Maximum.ToString("R", CultureInfo.InvariantCulture));

        // ARIA defines a missing current value as indeterminate.
        SetAttribute(
            "aria-valuenow",
            _progressBar.IsIndeterminate ? null : _progressBar.Value.ToString("R", CultureInfo.InvariantCulture));
    }
}
