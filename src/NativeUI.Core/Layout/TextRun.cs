using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Describes a UTF-16 range sharing one resolved text style within a paragraph.
/// </summary>
/// <param name="Range">The range in the combined paragraph text.</param>
/// <param name="Style">The resolved typography and paint of the range.</param>
public readonly record struct TextRun(TextRange Range, ComputedStyle Style);
