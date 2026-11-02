using NativeUI.Media.Animation;

namespace NativeUI.Controls;

/// <summary>
/// Implemented by a <see cref="INativeHandle"/> whose platform runs animations itself, such as a browser element.
/// </summary>
internal interface IAnimationPeer
{
    /// <summary>
    /// Plays the keyframes that <paramref name="spec"/> names and completes <paramref name="animation"/> when they end.
    /// </summary>
    void Animate(AnimationSpec spec, Animation animation);
}
