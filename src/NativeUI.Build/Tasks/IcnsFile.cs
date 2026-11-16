using System.Buffers.Binary;
using System.Text;

namespace NativeUI.Build;

/// <summary>
/// Creates Apple icon image files (<c>.icns</c>) from PNG images.
/// </summary>
public static class IcnsFile
{
    private const int HeaderLength = 8;
    private static readonly byte[] s_pngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Returns an <c>.icns</c> file that holds <paramref name="png"/> as its only image. The PNG must be square and 128, 256,
    /// 512, or 1024 pixels wide, because the file format names each image size.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="png"/> is not a PNG image of a supported size.</exception>
    public static byte[] FromPng(ReadOnlySpan<byte> png)
    {
        const int dimensionsOffset = 16;
        if (png.Length < dimensionsOffset + 8 || !png[..s_pngSignature.Length].SequenceEqual(s_pngSignature))
        {
            throw new ArgumentException("The image is not a PNG.", nameof(png));
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(png[dimensionsOffset..]);
        int height = BinaryPrimitives.ReadInt32BigEndian(png[(dimensionsOffset + 4)..]);
        string? type = width == height ? GetImageType(width) : null;
        if (type is null)
        {
            throw new ArgumentException($"The PNG is {width} by {height} pixels. It must be square and 128, 256, 512, or 1024 pixels wide.", nameof(png));
        }

        int imageLength = HeaderLength + png.Length;
        byte[] file = new byte[HeaderLength + imageLength];
        Encoding.ASCII.GetBytes("icns", file);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(4), file.Length);
        Encoding.ASCII.GetBytes(type, file.AsSpan(HeaderLength));
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(HeaderLength + 4), imageLength);
        png.CopyTo(file.AsSpan(HeaderLength * 2));
        return file;
    }

    private static string? GetImageType(int size)
    {
        return size switch
        {
            128 => "ic07",
            256 => "ic08",
            512 => "ic09",
            1024 => "ic10",
            _ => null
        };
    }
}
