namespace NativeUI.Controls;

/// <summary>
/// Implemented by the <see cref="INativeHandle"/> of a <see cref="Portal"/> whose platform runs style transitions and
/// animations itself, such as a browser element.
/// </summary>
internal interface IPortalPeer
{
    /// <summary>
    /// Invokes <paramref name="callback"/> after the platform has drawn the portal in a frame.
    /// </summary>
    void RequestAnimationFrame(Action callback);

    /// <summary>
    /// Invokes <paramref name="callback"/> after the transitions and the animations with a finite number of iterations of
    /// the portal element finish, or at once when it has none.
    /// </summary>
    void WaitForAnimations(Action callback);
}
