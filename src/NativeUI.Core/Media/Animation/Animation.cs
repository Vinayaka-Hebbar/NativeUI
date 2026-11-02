namespace NativeUI.Media.Animation;

/// <summary>
/// Represents a keyframe animation playback operation with cancellation and completion state.
/// </summary>
public sealed class Animation
{
    private readonly TaskCompletionSource _finished = new();
    private Action? _cancel;
    private PlaybackState _state = PlaybackState.Active;

    internal Animation()
    {
    }

    /// <summary>
    /// Gets a task that completes when the animation ends or is cancelled. It completes immediately when the element has
    /// no platform to animate on, the keyframes do not exist, or the system asks to reduce motion.
    /// </summary>
    public Task Finished => _finished.Task;

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    public PlaybackState State => _state;

    /// <summary>
    /// Stops the animation and returns the element to its style. Does nothing after the animation ends.
    /// </summary>
    public void Cancel()
    {
        if (_state != PlaybackState.Active)
        {
            return;
        }

        _state = PlaybackState.Cancelling;
        try
        {
            _cancel?.Invoke();
        }
        finally
        {
            Complete();
        }
    }

    /// <summary>
    /// Sets the platform operation that stops the animation.
    /// </summary>
    internal void SetCancelHandler(Action cancel)
    {
        if (_state == PlaybackState.Active)
        {
            _cancel = cancel;
        }
    }

    /// <summary>
    /// Records that the animation ended.
    /// </summary>
    internal void Complete()
    {
        _state = PlaybackState.Completed;
        _cancel = null;
        _finished.TrySetResult();
    }
}
