using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>Describes a failure loading the current image generation.</summary>
public sealed class ImageFailedEventArgs : EventArgs
{
    /// <summary>Creates failure data for an image source.</summary>
    public ImageFailedEventArgs(ImageSource source, Exception exception)
    {
        Source = source;
        Exception = exception;
    }
    /// <summary>Gets the source that failed.</summary>
    public ImageSource Source { get; }
    /// <summary>Gets the failure details.</summary>
    public Exception Exception { get; }
}
