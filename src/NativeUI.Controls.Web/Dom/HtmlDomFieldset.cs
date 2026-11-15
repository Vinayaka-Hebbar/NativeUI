using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Maps a semantic grouping to a native fieldset, including the first-legend exception.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomFieldset : HtmlDomView
{
    private readonly Fieldset _fieldset;
    internal HtmlDomFieldset(Fieldset fieldset, JSObject? element) : base(fieldset, element, "fieldset") => _fieldset = fieldset;

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
        if (ReferenceEquals(args.Property, UIElement.IsEnabledProperty) || ReferenceEquals(args.Property, Fieldset.NameProperty))
        {
            UpdateAttributes();
        }
    }
    private void UpdateAttributes()
    {
        SetAttribute("disabled", !_fieldset.IsEnabled ? string.Empty : null);
        SetAttribute("name", _fieldset.Name);
    }
}
