namespace NativeUI.Core;

/// <summary>
/// Provides cross-platform asynchronous access to the system clipboard for copying and reading plain text.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Copies text to the system clipboard.
    /// </summary>
    ValueTask SetTextAsync(string text);

    /// <summary>
    /// Gets plain text from the system clipboard, or <see langword="null"/> if unavailable.
    /// </summary>
    ValueTask<string?> GetTextAsync();
}
