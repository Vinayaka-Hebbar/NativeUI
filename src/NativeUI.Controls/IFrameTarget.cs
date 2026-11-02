namespace NativeUI.Controls;

/// <summary>
/// Implemented by objects that <see cref="AnimationClock"/> advances on each screen refresh.
/// </summary>
internal interface IFrameTarget
{
    /// <summary>
    /// Gets the control whose tree membership and visibility determine whether this target receives frames.
    /// </summary>
    UIElement Target { get; }

    /// <summary>
    /// Advances to <paramref name="now"/>, in milliseconds on the clock's timeline.
    /// </summary>
    void Tick(double now);
}
