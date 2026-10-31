namespace NativeUI.Layout;

/// <summary>
/// Describes the result of hit-testing a 2D point against a shaped <see cref="ITextLayout"/>.
/// </summary>
/// <param name="TextPosition">The resolved caret UTF-16 character index (leading or trailing boundary of <paramref name="Range"/>).</param>
/// <param name="Range">The character range of the hit text cluster.</param>
/// <param name="IsInside">Whether the tested point was inside the visual bounds of the text lines.</param>
/// <param name="IsTrailingEdge">Whether the point resolved to the logical trailing edge of <paramref name="Range"/>.</param>
public readonly record struct TextHitTestResult(
    int TextPosition,
    TextRange Range,
    bool IsInside,
    bool IsTrailingEdge);
