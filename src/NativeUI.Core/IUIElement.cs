using NativeUI.Media;

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
    /// Gets the effective theme inherited from the parent or host unless explicitly set.
    /// </summary>
    Theme ActualTheme { get; }

    /// <summary>
    /// Gets the parent element supplying inherited style properties, or null for a root element.
    /// </summary>
    IUIElement? Parent { get; }

    /// <summary>
    /// Gets the specified inline style applied after matching CSS declarations.
    /// </summary>
    IStyle? Style { get; }

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
}
