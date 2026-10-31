namespace NativeUI.Core;

/// <summary>
/// A synchronous <see cref="IDispatcher"/> implementation that executes actions immediately on the calling thread.
/// </summary>
public sealed class ImmediateDispatcher : IDispatcher
{
    /// <summary>
    /// Gets the shared singleton instance of <see cref="ImmediateDispatcher"/>.
    /// </summary>
    public static ImmediateDispatcher Instance { get; } = new();

    /// <inheritdoc />
    public bool CheckAccess()
    {
        return true;
    }

    /// <inheritdoc />
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
