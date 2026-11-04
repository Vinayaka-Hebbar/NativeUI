using System.Text;
using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;
using Svg.Skia;

namespace NativeUI.Graphics.Skia;

/// <summary>Decodes bounded image content into owned desktop drawing resources.</summary>
public sealed class SkiaImageDecoder : IImageDecoder
{
    /// <summary>Gets the stateless shared decoder.</summary>
    public static SkiaImageDecoder Shared { get; } = new();

    /// <inheritdoc />
    public Task<IDecodedImage> DecodeAsync(byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return Task.Run<IDecodedImage>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (contentType == "image/svg+xml")
            {
                SvgNode definition = SvgParser.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                SKSvg svg = new();
                try
                {
                    using MemoryStream stream = new(bytes);
                    svg.Load(stream);
                    if (svg.Picture is null) throw new InvalidDataException("SVG could not be decoded.");
                    cancellationToken.ThrowIfCancellationRequested();
                    return new SkiaDecodedImage(svg, definition.GetIntrinsicSize());
                }
                catch { svg.Dispose(); throw; }
            }
            if (contentType is not ("image/png" or "image/jpeg"))
            {
                throw new NotSupportedException($"Image content type '{contentType}' is not supported.");
            }

            using SKData data = SKData.CreateCopy(bytes);
            using SKCodec codec = SKCodec.Create(data) ?? throw new InvalidDataException("Image decoding failed.");
            SKImageInfo info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 32_000_000) throw new InvalidDataException("Image exceeds the 32 million pixel limit.");
            using SKBitmap bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
            SKImage decoded = SKImage.FromBitmap(bitmap);
            if (cancellationToken.IsCancellationRequested)
            {
                decoded.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new SkiaDecodedImage(decoded);
        }, cancellationToken);
    }
}

internal sealed class SkiaDecodedImage : IDecodedImage
{
    private readonly SKImage? _image;
    private readonly SKSvg? _svg;
    private bool _disposed;

    public SkiaDecodedImage(SKImage image)
    {
        _image = image;
        Size = new Size(image.Width, image.Height);
    }
    public SkiaDecodedImage(SKSvg svg, Size size)
    {
        _svg = svg;
        Size = size;
    }
    public Size Size { get; }
    public void Draw(SKCanvas canvas, SKRect destination, double opacity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using SKPaint paint = new() { Color = SKColors.White.WithAlpha((byte)Math.Clamp(opacity * 255, 0, 255)), IsAntialias = true };
        if (_image is not null) canvas.DrawImage(_image, destination, new SKSamplingOptions(SKFilterMode.Linear), paint);
        else if (_svg?.Picture is SKPicture picture)
        {
            canvas.Save();
            try
            {
                canvas.Translate(destination.Left, destination.Top);
                SKRect bounds = picture.CullRect;
                canvas.Scale(destination.Width / bounds.Width, destination.Height / bounds.Height);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture, paint);
            }
            finally { canvas.Restore(); }
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _image?.Dispose();
        _svg?.Dispose();
    }
}
