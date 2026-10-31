using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Measures the logical pixel bounds of a text run for desktop layout calculation.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>
    /// Measures the width and height of <paramref name="text"/> using the given font metrics and maximum available width.
    /// </summary>
    Size MeasureText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string? fontFamily,
        double maxWidth);
}
