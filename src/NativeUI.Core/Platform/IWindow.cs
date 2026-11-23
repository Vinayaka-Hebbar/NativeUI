namespace NativeUI.Core;

/// <summary>
/// Represents a top-level window that the application opened with <see cref="Application.OpenWindow"/>.
/// </summary>
public interface IWindow
{
    /// <summary>
    /// Gets or sets the text shown in the window's title bar.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Gets the view the window shows, or <see langword="null"/> when it shows none.
    /// </summary>
    IView? Content { get; }

    /// <summary>
    /// Closes the window. Closing a closed window does nothing.
    /// </summary>
    void Close();

    /// <summary>
    /// Occurs once when the window closes, whether from <see cref="Close"/> or from the user.
    /// </summary>
    event EventHandler? Closed;
}
