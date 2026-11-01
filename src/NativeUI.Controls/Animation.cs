namespace NativeUI.Controls;

/// <summary>
/// Represents a keyframe animation started by <see cref="UIElement.Animate"/>.
/// </summary>
public sealed class Animation
{
    private readonly TaskCompletionSource _finished = new();
    private Action? _cancel;

    internal Animation()
    {
    }

    /// <summary>
    /// Gets a task that completes when the animation ends or is cancelled. It completes immediately when the element has
    /// no platform to animate on, the keyframes do not exist, or the system asks to reduce motion.
    /// </summary>
    public Task Finished => _finished.Task;

    /// <summary>
    /// Stops the animation and returns the element to its style. Does nothing after the animation ends.
    /// </summary>
    public void Cancel()
    {
        if (_finished.Task.IsCompleted)
        {
            return;
        }

        _cancel?.Invoke();
        Complete();
    }

    /// <summary>
    /// Sets the platform operation that stops the animation.
    /// </summary>
    internal void SetCancelHandler(Action cancel)
    {
        _cancel = cancel;
    }

    /// <summary>
    /// Records that the animation ended.
    /// </summary>
    internal void Complete()
    {
        _finished.TrySetResult();
    }
}
