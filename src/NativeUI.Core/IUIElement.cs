using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Core;

/// <summary>
/// Provides the element properties required to compute styles without depending on control implementations.
/// </summary>
public interface IUIElement
{
    /// <summary>
    /// Gets the canonical NativeUI element type name, such as <c>text</c> or <c>view</c>, available to style resolvers.
    /// </summary>
    string TagName { get; }

    /// <summary>
    /// Gets the style class names applied to the element.
    /// </summary>
    IReadOnlyList<string> Classes { get; }

    /// <summary>
    /// Gets the identifier of the enclosing view's stylesheet.
    /// </summary>
    string? StyleSheetId { get; }

    /// <summary>
    /// Gets the parent element supplying inherited style properties, or null for a root element.
    /// </summary>
    IUIElement? Parent { get; }

    /// <summary>
    /// Gets the specified inline style applied after matching CSS declarations.
    /// </summary>
    IStyle? Style { get; }

    /// <summary>
    /// Gets the element's width: its inline value, or the default for its control type. Stylesheets override a default.
    /// </summary>
    WidthValue? Width { get; }

    /// <summary>
    /// Gets the element's height: its inline value, or the default for its control type. Stylesheets override a default.
    /// </summary>
    double? Height { get; }

    /// <summary>
    /// Gets the element's horizontal overflow: its inline value, or the default for its control type. Stylesheets override a default.
    /// </summary>
    Overflow? OverflowX { get; }

    /// <summary>
    /// Gets the element's vertical overflow: its inline value, or the default for its control type. Stylesheets override a default.
    /// </summary>
    Overflow? OverflowY { get; }

    /// <summary>
    /// Gets the element's CSS positioning scheme: its inline value, or the default for its control type. Stylesheets override a default.
    /// </summary>
    Position? Position { get; }

    /// <summary>
    /// Gets the offset from the top edge of the containing block in logical pixels.
    /// </summary>
    double? Top { get; }

    /// <summary>
    /// Gets the offset from the right edge of the containing block in logical pixels.
    /// </summary>
    double? Right { get; }

    /// <summary>
    /// Gets the offset from the bottom edge of the containing block in logical pixels.
    /// </summary>
    double? Bottom { get; }

    /// <summary>
    /// Gets the offset from the left edge of the containing block in logical pixels.
    /// </summary>
    double? Left { get; }

    /// <summary>
    /// Gets the most recently computed style snapshot.
    /// </summary>
    ComputedStyle ComputedStyle { get; }

    /// <summary>
    /// Gets the most recently computed snapshot of the element's selected-text colors.
    /// </summary>
    SelectionStyle SelectionStyle { get; }

    /// <summary>
    /// Gets the element's current interaction states.
    /// </summary>
    ElementStates States { get; }

    /// <summary>
    /// Gets the specified font size in logical pixels from scoped stylesheets or inline styles, or null if unspecified.
    /// </summary>
    double? FontSize { get; }
}
