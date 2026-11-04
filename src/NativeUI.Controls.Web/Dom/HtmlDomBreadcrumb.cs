using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Breadcrumb"/> with a custom element that has the ARIA <c>navigation</c> role and the
/// accessible name <c>breadcrumb</c>. The base stylesheet makes the element a row flex container.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomBreadcrumb : HtmlDomControl
{
    internal HtmlDomBreadcrumb(Breadcrumb breadcrumb, JSObject? element, string tagName)
        : base(breadcrumb, element, tagName)
    {
    }

    /// <inheritdoc />
    protected override string Role => "navigation";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("aria-label", "breadcrumb");
    }
}
