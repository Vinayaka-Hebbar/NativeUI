namespace NativeUI.Controls;

/// <summary>
/// Specifies the phase of the open and close transitions of a <see cref="Portal"/>.
/// </summary>
internal enum TransitionStatus
{
    /// <summary>
    /// No transition is pending.
    /// </summary>
    Idle,

    /// <summary>
    /// The portal shows its first frame after opening, with <c>data-starting-style</c>.
    /// </summary>
    Starting,

    /// <summary>
    /// The portal is closed and stays drawn, with <c>data-ending-style</c>, until its animations finish.
    /// </summary>
    Ending
}
