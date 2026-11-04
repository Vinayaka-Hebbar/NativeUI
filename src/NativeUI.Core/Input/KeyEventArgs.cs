namespace NativeUI.Core;

/// <summary>
/// Describes a platform-independent key press or release.
/// </summary>
/// <param name="key">The logical key.</param>
/// <param name="text">The text the key produces, or <see langword="null"/> when it produces none.</param>
/// <param name="modifiers">The modifier keys held during the event.</param>
/// <param name="isRepeat">Whether the key press repeats because the key is held down.</param>
public sealed class KeyEventArgs(
    Key key,
    string? text = null,
    InputModifiers modifiers = InputModifiers.None,
    bool isRepeat = false) : RoutedEventArgs
{
    /// <summary>
    /// Gets the logical key.
    /// </summary>
    public Key Key { get; } = key;

    /// <summary>
    /// Gets the text the key produces, or <see langword="null"/> when it produces none. <see cref="Key.Character"/>
    /// always has text.
    /// </summary>
    public string? Text { get; } = text;

    /// <summary>
    /// Gets the modifier keys held during the event.
    /// </summary>
    public InputModifiers Modifiers { get; } = modifiers;

    /// <summary>
    /// Gets a value indicating whether the key press repeats because the key is held down.
    /// </summary>
    public bool IsRepeat { get; } = isRepeat;
}
