namespace NativeUI.Controls;

/// <summary>
/// Implemented by objects that <see cref="AnimationClock"/> advances on each screen refresh.
/// </summary>
internal interface IFrameTarget
{
    /// <summary>
    /// Advances to <paramref name="now"/>, in milliseconds on the clock's timeline.
    /// </summary>
    void Tick(double now);
}
