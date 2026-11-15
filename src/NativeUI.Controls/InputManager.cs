namespace NativeUI.Controls;

/// <summary>
/// Owns the primary keyboard device for the current UI thread.
/// </summary>
public sealed class InputManager
{
    [ThreadStatic]
    private static InputManager? _current;

    private InputManager()
    {
    }

    /// <summary>
    /// Gets the input manager for the calling UI thread.
    /// </summary>
    public static InputManager Current => _current ??= new InputManager();

    /// <summary>
    /// Gets the device that owns keyboard focus and dispatches keyboard input on this thread.
    /// </summary>
    public KeyboardDevice PrimaryKeyboardDevice { get; } = new();
}
