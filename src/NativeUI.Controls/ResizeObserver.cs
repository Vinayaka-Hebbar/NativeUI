namespace NativeUI.Controls;

/// <summary>
/// Defines a lifecycle-aware observer that can start, pause, and stop observation.
/// </summary>
public interface IObserver : IDisposable
{
    /// <summary>
    /// Starts or resumes observation.
    /// </summary>
    void Observe();

    /// <summary>
    /// Pauses or unregisters observation.
    /// </summary>
    void Unobserve();
}

/// <summary>
/// Represents a platform-owned observer that monitors changes to the rendered dimensions of a <see cref="UIElement"/>.
/// </summary>
public abstract class ResizeObserver : IObserver
{
    /// <summary>
    /// Starts or resumes size observation.
    /// </summary>
    public abstract void Observe();

    /// <summary>
    /// Pauses or unregisters size observation.
    /// </summary>
    public abstract void Unobserve();

    /// <summary>
    /// Releases the observer, unregistering native observation.
    /// </summary>
    public virtual void Dispose()
    {
        Unobserve();
    }
}
