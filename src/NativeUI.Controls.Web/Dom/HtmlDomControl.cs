using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Control"/> that has no equivalent HTML element with an autonomous custom element. The
/// element exposes the control's ARIA role and disabled state. <see cref="HtmlDomElement"/> writes its <c>tabindex</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public abstract class HtmlDomControl : HtmlDomView
{
    internal HtmlDomControl(Control control, JSObject? element, string tagName)
        : base(control, element, tagName)
    {
    }

    /// <summary>
    /// Gets the ARIA role of the element.
    /// </summary>
    protected abstract string Role { get; }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("role", Role);
        UpdateEnabledState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, UIElement.IsEffectivelyEnabledProperty))
        {
            UpdateEnabledState();
        }
    }

    private void UpdateEnabledState()
    {
        SetAttribute("aria-disabled", Source.IsEffectivelyEnabled ? null : "true");
    }
}
