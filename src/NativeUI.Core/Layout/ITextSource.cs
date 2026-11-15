namespace NativeUI.Layout;

/// <summary>
/// Supplies paragraph text and styled ranges to the text measurement and shaping pipeline.
/// </summary>
public interface ITextSource
{
    /// <summary>Gets the combined UTF-16 paragraph text, or null when child elements provide the content.</summary>
    string? Text { get; }

    /// <summary>Gets resolved formatted ranges, or an empty list for plain text.</summary>
    IReadOnlyList<TextRun> TextRuns => [];
}
