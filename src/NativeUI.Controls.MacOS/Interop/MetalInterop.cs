using System.Runtime.InteropServices;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Provides Metal and Core Animation bindings for presenting Skia frames through a <c>CAMetalLayer</c>.
/// </summary>
internal static partial class MetalInterop
{
    /// <summary>
    /// The <c>MTLPixelFormatBGRA8Unorm</c> value of <c>MTLPixelFormat</c>.
    /// </summary>
    public const nuint PixelFormatBgra8Unorm = 80;

    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";
    private const string QuartzCoreFramework = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";

    [LibraryImport(MetalFramework, EntryPoint = "MTLCreateSystemDefaultDevice")]
    private static partial nint MTLCreateSystemDefaultDevice();

    [LibraryImport(ObjCLib, EntryPoint = "objc_autoreleasePoolPush")]
    public static partial nint AutoreleasePoolPush();

    [LibraryImport(ObjCLib, EntryPoint = "objc_autoreleasePoolPop")]
    public static partial void AutoreleasePoolPop(nint pool);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, nuint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, double arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, AppKitInterop.CGSize arg1);

    /// <summary>
    /// Returns the retained default Metal device, or <see cref="nint.Zero"/> when the system has none.
    /// </summary>
    public static nint CreateSystemDefaultDevice()
    {
        _ = NativeLibrary.Load(MetalFramework);
        return MTLCreateSystemDefaultDevice();
    }

    /// <summary>
    /// Returns a retained command queue of <paramref name="device"/>.
    /// </summary>
    public static nint CreateCommandQueue(nint device)
    {
        return AppKitInterop.SendMessage(device, AppKitInterop.GetSelector("newCommandQueue"));
    }

    /// <summary>
    /// Returns an autoreleased <c>CAMetalLayer</c> drawing with <paramref name="device"/> in BGRA8 pixels that Skia can
    /// sample, in the sRGB color space.
    /// </summary>
    public static nint CreateLayer(nint device, nint colorSpace)
    {
        _ = NativeLibrary.Load(QuartzCoreFramework);
        nint layer = AppKitInterop.SendMessage(AppKitInterop.GetClass("CAMetalLayer"), AppKitInterop.GetSelector("layer"));
        AppKitInterop.SendMessageVoid(layer, AppKitInterop.GetSelector("setDevice:"), device);
        SendMessageVoid(layer, AppKitInterop.GetSelector("setPixelFormat:"), PixelFormatBgra8Unorm);

        // Skia reads the render target for some blend modes and layer effects.
        AppKitInterop.SendMessageVoid(layer, AppKitInterop.GetSelector("setFramebufferOnly:"), false);
        AppKitInterop.SendMessageVoid(layer, AppKitInterop.GetSelector("setColorspace:"), colorSpace);
        return layer;
    }

    /// <summary>
    /// Sets the pixel size of the drawables that <paramref name="layer"/> returns.
    /// </summary>
    public static void SetDrawableSize(nint layer, int pixelWidth, int pixelHeight)
    {
        SendMessageVoid(layer, AppKitInterop.GetSelector("setDrawableSize:"), new AppKitInterop.CGSize(pixelWidth, pixelHeight));
    }

    /// <summary>
    /// Sets the ratio of drawable pixels to layer points.
    /// </summary>
    public static void SetContentsScale(nint layer, double scale)
    {
        SendMessageVoid(layer, AppKitInterop.GetSelector("setContentsScale:"), scale);
    }

    /// <summary>
    /// Returns the autoreleased next drawable of <paramref name="layer"/>, or <see cref="nint.Zero"/> when none becomes
    /// available in time.
    /// </summary>
    public static nint NextDrawable(nint layer)
    {
        return AppKitInterop.SendMessage(layer, AppKitInterop.GetSelector("nextDrawable"));
    }

    /// <summary>
    /// Returns the texture of <paramref name="drawable"/>, which the drawable owns.
    /// </summary>
    public static nint GetTexture(nint drawable)
    {
        return AppKitInterop.SendMessage(drawable, AppKitInterop.GetSelector("texture"));
    }

    /// <summary>
    /// Commits a command buffer on <paramref name="queue"/> that presents <paramref name="drawable"/> after the work already
    /// committed to the queue.
    /// </summary>
    public static void Present(nint queue, nint drawable)
    {
        nint commandBuffer = AppKitInterop.SendMessage(queue, AppKitInterop.GetSelector("commandBuffer"));
        AppKitInterop.SendMessageVoid(commandBuffer, AppKitInterop.GetSelector("presentDrawable:"), drawable);
        _ = AppKitInterop.SendMessage(commandBuffer, AppKitInterop.GetSelector("commit"));
    }
}
