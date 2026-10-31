using System.Collections.Immutable;

namespace NativeUI.Layout;

/// <summary>
/// Represents a shaped, wrapped, and bidirectional-aware text layout supporting character/caret hit-testing,
/// word boundary selection, and multi-line selection highlight geometry.
/// </summary>
public interface ITextLayout
{
    /// <summary>
    /// Gets the source UTF-16 text string represented by this layout.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Gets the overall measured bounding size of all lines in logical pixels.
    /// </summary>
    Size Size { get; }

    /// <summary>
    /// Gets the immutable wrapped lines of shaped text.
    /// </summary>
    ImmutableArray<TextLine> Lines { get; }

    /// <summary>
    /// Hit-tests a point (relative to the layout's top-left origin) to find the nearest text cluster
    /// and its visual-direction-aware leading or trailing caret position.
    /// </summary>
    TextHitTestResult HitTestPoint(Point localPoint);

    /// <summary>
    /// Computes the immutable visual selection highlight rectangles (relative to the layout's top-left origin)
    /// for the UTF-16 character range <c>[startIndex, startIndex + length)</c> across wrapped lines and BiDi runs.
    /// </summary>
    ImmutableArray<Rect> HitTestTextRange(int startIndex, int length);

    /// <summary>
    /// Returns the word range surrounding <paramref name="textIndex"/> for double-click word selection.
    /// </summary>
    TextRange GetWordRangeAt(int textIndex);
}
