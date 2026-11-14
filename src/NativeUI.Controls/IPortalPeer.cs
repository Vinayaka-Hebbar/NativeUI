namespace NativeUI.Controls;

/// <summary>
/// Implemented by the <see cref="INativeHandle"/> of a <see cref="Portal"/> whose platform runs style transitions and
/// animations itself, such as a browser element.
/// </summary>
internal interface IPortalPeer
{
    /// <summary>
    /// Calls <paramref name="completed"/> after the platform has drawn the portal with <c>data-starting-style</c>.
    /// </summary>
    void WaitForStartingStyle(Action completed);

    /// <summary>
    /// Calls <paramref name="completed"/> after the transitions and the animations with a finite number of iterations of
    /// the portal element end, or at once when it has none.
    /// </summary>
    void WaitForEndingMotion(Action completed);
}
