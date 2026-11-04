using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Control"/> that has no equivalent HTML element with an autonomous custom element. The
/// element exposes the control's ARIA role and disabled state, and a focusable control is in the tab order while it is enabled.
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

    /// <summary>
    /// Gets a value indicating whether the element receives keyboard focus while the control is enabled.
    /// </summary>
    protected virtual bool IsFocusable => true;

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
        if (ReferenceEquals(args.Property, UIElement.IsEnabledProperty))
        {
            UpdateEnabledState();
        }
    }

    private void UpdateEnabledState()
    {
        bool isEnabled = Source.IsEnabled;
        SetAttribute("aria-disabled", isEnabled ? null : "true");
        if (IsFocusable)
        {
            SetAttribute("tabindex", isEnabled ? "0" : null);
        }
    }
}
