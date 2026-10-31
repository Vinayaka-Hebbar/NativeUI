using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Measures and shapes text runs into <see cref="ITextLayout"/> instances for desktop layout and hit-testing.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>
    /// Measures the width and height of <paramref name="text"/> within <paramref name="availableSize"/> using <paramref name="style"/>.
    /// </summary>
    Size MeasureText(
        string text,
        Size availableSize,
        ComputedStyle style);

    /// <summary>
    /// Creates a shaped, wrapped <see cref="ITextLayout"/> for <paramref name="text"/> within <paramref name="availableSize"/>
    /// using <paramref name="style"/>.
    /// </summary>
    ITextLayout CreateTextLayout(
        string text,
        Size availableSize,
        ComputedStyle style);
}
