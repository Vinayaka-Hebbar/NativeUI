namespace NativeUI.Media.Animation;

/// <summary>
/// Describes the lifecycle of an animation playback operation.
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// The animation has not completed or begun cancellation.
    /// </summary>
    Active,

    /// <summary>
    /// The animation is executing its cancellation operation.
    /// </summary>
    Cancelling,

    /// <summary>
    /// The animation has ended or cancellation has finished.
    /// </summary>
    Completed
}
