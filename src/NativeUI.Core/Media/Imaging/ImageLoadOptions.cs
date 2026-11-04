namespace NativeUI.Media;

/// <summary>Controls encoded image loading. Decoders enforce their own decoded-pixel limits.</summary>
public sealed class ImageLoadOptions
{
    /// <summary>Gets the maximum encoded byte count per image, defaulting to 32 MiB.</summary>
    public long MaximumEncodedBytes { get; init; } = 32L * 1024 * 1024;
}
