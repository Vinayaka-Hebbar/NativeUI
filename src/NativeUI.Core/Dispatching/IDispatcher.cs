namespace NativeUI.Core;

/// <summary>
/// Provides UI-thread marshaling for observable property updates and UI mutations.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the caller is currently executing on the UI thread.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Executes the specified <paramref name="action"/> on the UI thread.
    /// </summary>
    /// <param name="action">The work item to execute.</param>
    void Invoke(Action action);
}
