using System.Diagnostics;

namespace NativeUI.Controls;

/// <summary>
/// Advances frame targets on each screen refresh, when a host calls <see cref="Tick"/>. Style animations also need a host
/// that attaches as a driver because it draws its own frames. Without a driver styles change immediately: a browser runs
/// its own CSS animations, and an off-screen window has no frames to show. Operations must run on the UI thread.
/// </summary>
internal static class AnimationClock
{
    private static readonly List<IFrameTarget> s_active = [];
    private static int s_driverCount;
    private static bool s_isMotionReduced;

    /// <summary>
    /// Occurs when a target starts while none were running, so a paused driver must resume its ticks.
    /// </summary>
    public static event Action? AnimationStarted;

    /// <summary>
    /// Gets a value indicating whether styles animate instead of changing immediately.
    /// </summary>
    public static bool IsEnabled => s_driverCount > 0 && !s_isMotionReduced;

    /// <summary>
    /// Gets a value indicating whether any target needs further ticks.
    /// </summary>
    public static bool HasActiveAnimations => s_active.Count > 0;

    /// <summary>
    /// Gets the current time in milliseconds on the clock's monotonic timeline.
    /// </summary>
    public static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Registers a host that ticks this clock.
    /// </summary>
    /// <param name="isMotionReduced">Whether the system asks applications to avoid animation.</param>
    public static void AttachDriver(bool isMotionReduced)
    {
        s_driverCount++;
        s_isMotionReduced = isMotionReduced;
    }

    /// <summary>
    /// Removes a host registered by <see cref="AttachDriver"/>.
    /// </summary>
    public static void DetachDriver()
    {
        s_driverCount--;
    }

    /// <summary>
    /// Advances every active target to the current time.
    /// </summary>
    public static void Tick()
    {
        double now = Now;
        foreach (IFrameTarget target in s_active.ToArray())
        {
            target.Tick(now);
        }
    }

    /// <summary>
    /// Adds <paramref name="target"/> to the objects receiving ticks.
    /// </summary>
    public static void Add(IFrameTarget target)
    {
        if (s_active.Contains(target))
        {
            return;
        }

        s_active.Add(target);
        if (s_active.Count == 1)
        {
            AnimationStarted?.Invoke();
        }
    }

    /// <summary>
    /// Removes <paramref name="target"/> from the objects receiving ticks.
    /// </summary>
    public static void Remove(IFrameTarget target)
    {
        s_active.Remove(target);
    }
}
