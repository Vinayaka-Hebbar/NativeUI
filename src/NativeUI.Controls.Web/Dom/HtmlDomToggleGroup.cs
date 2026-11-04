using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="ToggleGroup"/> with a custom element that has the ARIA <c>group</c> role. The base
/// stylesheet makes the element a row flex container, which applies the CSS <c>gap</c> property between its toggles.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomToggleGroup : HtmlDomControl
{
    internal HtmlDomToggleGroup(ToggleGroup toggleGroup, JSObject? element, string tagName)
        : base(toggleGroup, element, tagName)
    {
    }

    /// <inheritdoc />
    protected override string Role => "group";

    /// <inheritdoc />
    protected override bool IsFocusable => false;
}
