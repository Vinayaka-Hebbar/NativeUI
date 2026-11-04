namespace NativeUI.Graphics;

/// <summary>Decodes bounded encoded image content without depending on a control.</summary>
public interface IImageDecoder
{
    /// <summary>Decodes encoded image bytes using their resolved content type.</summary>
    Task<IDecodedImage> DecodeAsync(byte[] bytes, string contentType, CancellationToken cancellationToken);
}
