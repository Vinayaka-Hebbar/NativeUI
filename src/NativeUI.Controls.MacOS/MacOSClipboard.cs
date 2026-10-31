using NativeUI.Core;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Implements <see cref="IClipboard"/> for macOS using AppKit's <c>NSPasteboard.generalPasteboard</c>,
/// with an in-memory fallback when running in headless test environments.
/// </summary>
public sealed class MacOSClipboard : IClipboard
{
    private string? _fallbackText;

    /// <summary>
    /// Gets the default singleton instance of <see cref="MacOSClipboard"/>.
    /// </summary>
    public static MacOSClipboard Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _fallbackText = text;

        if (OperatingSystem.IsMacOS())
        {
            AppKitInterop.SetPasteboardString(text);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<string?> GetTextAsync()
    {
        if (OperatingSystem.IsMacOS())
        {
            string? nativeText = AppKitInterop.GetPasteboardString();
            if (nativeText is not null)
            {
                return ValueTask.FromResult<string?>(nativeText);
            }
        }

        return ValueTask.FromResult(_fallbackText);
    }
}
