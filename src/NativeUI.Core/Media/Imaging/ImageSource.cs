namespace NativeUI.Media;

/// <summary>Identifies encoded image content or a compiled SVG factory.</summary>
public abstract record ImageSource
{
    /// <summary>Converts a URI or application asset path to an image source.</summary>
    public static implicit operator ImageSource?(string? value) => string.IsNullOrWhiteSpace(value) ? null : new BitmapImage(value);
}
