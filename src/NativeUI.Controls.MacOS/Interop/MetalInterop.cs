using System.Runtime.InteropServices;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Identifies the pixel formats NativeUI uses. Values are those of Metal's <c>MTLPixelFormat</c>.
/// </summary>
internal enum MTLPixelFormat : ulong
{
    BGRA8Unorm = 80,
    Depth32Float = 252
}

/// <summary>
/// Identifies texture dimensions and sampling. Values are those of Metal's <c>MTLTextureType</c>.
/// </summary>
internal enum MTLTextureType : ulong
{
    Type2D = 2,
    Type2DMultisample = 4
}

/// <summary>
/// Identifies how a texture is used. Values are those of Metal's <c>MTLTextureUsage</c>.
/// </summary>
[Flags]
internal enum MTLTextureUsage : ulong
{
    ShaderRead = 1,
    RenderTarget = 4
}

/// <summary>
/// Identifies where a resource's memory lives. Values are those of Metal's <c>MTLStorageMode</c>.
/// </summary>
internal enum MTLStorageMode : ulong
{
    Shared = 0,
    Private = 2
}

/// <summary>
/// Identifies a depth comparison. Values are those of Metal's <c>MTLCompareFunction</c>.
/// </summary>
internal enum MTLCompareFunction : ulong
{
    Less = 1,
    Always = 7
}

/// <summary>
/// Identifies a blend factor. Values are those of Metal's <c>MTLBlendFactor</c>.
/// </summary>
internal enum MTLBlendFactor : ulong
{
    One = 1,
    OneMinusSourceAlpha = 5
}

/// <summary>
/// Identifies how an attachment starts a render pass. Values are those of Metal's <c>MTLLoadAction</c>.
/// </summary>
internal enum MTLLoadAction : ulong
{
    Clear = 2
}

/// <summary>
/// Identifies how an attachment ends a render pass. Values are those of Metal's <c>MTLStoreAction</c>.
/// </summary>
internal enum MTLStoreAction : ulong
{
    DontCare = 0,
    MultisampleResolve = 2
}

/// <summary>
/// Identifies how vertices form primitives. Values are those of Metal's <c>MTLPrimitiveType</c>.
/// </summary>
internal enum MTLPrimitiveType : ulong
{
    Triangle = 3
}

/// <summary>
/// Identifies the size of index buffer entries. Values are those of Metal's <c>MTLIndexType</c>.
/// </summary>
internal enum MTLIndexType : ulong
{
    UInt16 = 0
}

/// <summary>
/// Holds the color an attachment is cleared to, as Metal's <c>MTLClearColor</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MTLClearColor(double Red, double Green, double Blue, double Alpha);

/// <summary>
/// Provides Metal and Core Animation bindings for presenting Skia frames through a <c>CAMetalLayer</c> and for encoding
/// render passes.
/// </summary>
internal static partial class MetalInterop
{
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
    private static partial void SendMessageVoid(nint receiver, nint selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial nint SendMessage(nint receiver, nint selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, MTLClearColor arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, nint arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(nint receiver, nint selector, ulong arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void SendMessageVoid(
        nint receiver,
        nint selector,
        ulong arg1,
        ulong arg2,
        ulong arg3,
        nint arg4,
        ulong arg5,
        ulong arg6);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial nint SendMessage(nint receiver, nint selector, nint arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial nint SendMessage(
        nint receiver,
        nint selector,
        ulong arg1,
        ulong arg2,
        ulong arg3,
        [MarshalAs(UnmanagedType.I1)] bool arg4);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static unsafe partial nint SendMessage(nint receiver, nint selector, nint arg1, nint* arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static unsafe partial nint SendMessage(nint receiver, nint selector, nint arg1, nint arg2, nint* arg3);

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
        SendMessageVoid(layer, AppKitInterop.GetSelector("setPixelFormat:"), (ulong)MTLPixelFormat.BGRA8Unorm);

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

    /// <summary>
    /// Compiles Metal Shading Language source into a retained <c>MTLLibrary</c> of <paramref name="device"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source does not compile.</exception>
    public static unsafe nint CreateLibrary(nint device, string source)
    {
        nint error = nint.Zero;
        nint nsSource = AppKitInterop.CreateNSString(source);
        nint library = SendMessage(device, AppKitInterop.GetSelector("newLibraryWithSource:options:error:"), nsSource, nint.Zero, &error);
        Release(nsSource);
        ThrowIfFailed(library, error, "Failed to compile the Metal shader library.");
        return library;
    }

    /// <summary>
    /// Returns the retained function named <paramref name="name"/> in <paramref name="library"/>.
    /// </summary>
    public static nint CreateFunction(nint library, string name)
    {
        nint nsName = AppKitInterop.CreateNSString(name);
        nint function = AppKitInterop.SendMessage(library, AppKitInterop.GetSelector("newFunctionWithName:"), nsName);
        Release(nsName);
        return function;
    }

    /// <summary>
    /// Returns a retained render pipeline state drawing with the given functions into a multisampled color attachment and
    /// a depth attachment.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <param name="vertexFunction">The vertex function.</param>
    /// <param name="fragmentFunction">The fragment function.</param>
    /// <param name="colorFormat">The color attachment's pixel format.</param>
    /// <param name="sampleCount">The number of samples per pixel.</param>
    /// <param name="isPremultipliedBlendEnabled">Whether fragments blend over the attachment as premultiplied colors.</param>
    /// <exception cref="InvalidOperationException">The pipeline is invalid for the device.</exception>
    public static unsafe nint CreateRenderPipelineState(
        nint device,
        nint vertexFunction,
        nint fragmentFunction,
        MTLPixelFormat colorFormat,
        int sampleCount,
        bool isPremultipliedBlendEnabled)
    {
        nint descriptor = CreateInstance("MTLRenderPipelineDescriptor");
        AppKitInterop.SendMessageVoid(descriptor, AppKitInterop.GetSelector("setVertexFunction:"), vertexFunction);
        AppKitInterop.SendMessageVoid(descriptor, AppKitInterop.GetSelector("setFragmentFunction:"), fragmentFunction);
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setRasterSampleCount:"), (ulong)sampleCount);
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setDepthAttachmentPixelFormat:"), (ulong)MTLPixelFormat.Depth32Float);

        nint color = GetFirstColorAttachment(descriptor);
        SendMessageVoid(color, AppKitInterop.GetSelector("setPixelFormat:"), (ulong)colorFormat);
        if (isPremultipliedBlendEnabled)
        {
            AppKitInterop.SendMessageVoid(color, AppKitInterop.GetSelector("setBlendingEnabled:"), true);
            SendMessageVoid(color, AppKitInterop.GetSelector("setSourceRGBBlendFactor:"), (ulong)MTLBlendFactor.One);
            SendMessageVoid(color, AppKitInterop.GetSelector("setSourceAlphaBlendFactor:"), (ulong)MTLBlendFactor.One);
            SendMessageVoid(color, AppKitInterop.GetSelector("setDestinationRGBBlendFactor:"), (ulong)MTLBlendFactor.OneMinusSourceAlpha);
            SendMessageVoid(color, AppKitInterop.GetSelector("setDestinationAlphaBlendFactor:"), (ulong)MTLBlendFactor.OneMinusSourceAlpha);
        }

        nint error = nint.Zero;
        nint state = SendMessage(device, AppKitInterop.GetSelector("newRenderPipelineStateWithDescriptor:error:"), descriptor, &error);
        Release(descriptor);
        ThrowIfFailed(state, error, "Failed to create the Metal render pipeline.");
        return state;
    }

    /// <summary>
    /// Returns a retained depth-stencil state that compares depth with <paramref name="compare"/>.
    /// </summary>
    public static nint CreateDepthStencilState(nint device, MTLCompareFunction compare, bool isDepthWriteEnabled)
    {
        nint descriptor = CreateInstance("MTLDepthStencilDescriptor");
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setDepthCompareFunction:"), (ulong)compare);
        AppKitInterop.SendMessageVoid(descriptor, AppKitInterop.GetSelector("setDepthWriteEnabled:"), isDepthWriteEnabled);
        nint state = AppKitInterop.SendMessage(device, AppKitInterop.GetSelector("newDepthStencilStateWithDescriptor:"), descriptor);
        Release(descriptor);
        return state;
    }

    /// <summary>
    /// Returns a retained buffer in shared memory holding a copy of <paramref name="bytes"/>.
    /// </summary>
    public static unsafe nint CreateBuffer(nint device, ReadOnlySpan<byte> bytes)
    {
        fixed (byte* pointer = bytes)
        {
            return SendMessage(
                device,
                AppKitInterop.GetSelector("newBufferWithBytes:length:options:"),
                (nint)pointer,
                (ulong)bytes.Length,
                (ulong)MTLStorageMode.Shared);
        }
    }

    /// <summary>
    /// Returns a retained two-dimensional texture in GPU-private memory.
    /// </summary>
    public static nint CreateTexture(
        nint device,
        MTLPixelFormat format,
        int width,
        int height,
        int sampleCount,
        MTLTextureUsage usage)
    {
        nint descriptor = SendMessage(
            AppKitInterop.GetClass("MTLTextureDescriptor"),
            AppKitInterop.GetSelector("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
            (ulong)format,
            (ulong)width,
            (ulong)height,
            false);
        MTLTextureType type = sampleCount > 1 ? MTLTextureType.Type2DMultisample : MTLTextureType.Type2D;
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setTextureType:"), (ulong)type);
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setSampleCount:"), (ulong)sampleCount);
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setUsage:"), (ulong)usage);
        SendMessageVoid(descriptor, AppKitInterop.GetSelector("setStorageMode:"), (ulong)MTLStorageMode.Private);
        return AppKitInterop.SendMessage(device, AppKitInterop.GetSelector("newTextureWithDescriptor:"), descriptor);
    }

    /// <summary>
    /// Returns an autoreleased command buffer of <paramref name="queue"/>.
    /// </summary>
    public static nint CreateCommandBuffer(nint queue)
    {
        return AppKitInterop.SendMessage(queue, AppKitInterop.GetSelector("commandBuffer"));
    }

    /// <summary>
    /// Commits <paramref name="commandBuffer"/> for execution after the work already committed to its queue.
    /// </summary>
    public static void Commit(nint commandBuffer)
    {
        _ = AppKitInterop.SendMessage(commandBuffer, AppKitInterop.GetSelector("commit"));
    }

    /// <summary>
    /// Begins a render pass that clears a multisampled color attachment to <paramref name="clearColor"/> and resolves it
    /// into <paramref name="resolveTexture"/>, with a cleared depth attachment that is discarded afterward.
    /// </summary>
    /// <returns>The autoreleased render command encoder.</returns>
    public static nint BeginRenderPass(
        nint commandBuffer,
        nint colorTexture,
        nint resolveTexture,
        nint depthTexture,
        MTLClearColor clearColor)
    {
        nint pass = AppKitInterop.SendMessage(
            AppKitInterop.GetClass("MTLRenderPassDescriptor"),
            AppKitInterop.GetSelector("renderPassDescriptor"));
        nint color = GetFirstColorAttachment(pass);
        AppKitInterop.SendMessageVoid(color, AppKitInterop.GetSelector("setTexture:"), colorTexture);
        AppKitInterop.SendMessageVoid(color, AppKitInterop.GetSelector("setResolveTexture:"), resolveTexture);
        SendMessageVoid(color, AppKitInterop.GetSelector("setLoadAction:"), (ulong)MTLLoadAction.Clear);
        SendMessageVoid(color, AppKitInterop.GetSelector("setStoreAction:"), (ulong)MTLStoreAction.MultisampleResolve);
        SendMessageVoid(color, AppKitInterop.GetSelector("setClearColor:"), clearColor);

        nint depth = AppKitInterop.SendMessage(pass, AppKitInterop.GetSelector("depthAttachment"));
        AppKitInterop.SendMessageVoid(depth, AppKitInterop.GetSelector("setTexture:"), depthTexture);
        SendMessageVoid(depth, AppKitInterop.GetSelector("setLoadAction:"), (ulong)MTLLoadAction.Clear);
        SendMessageVoid(depth, AppKitInterop.GetSelector("setStoreAction:"), (ulong)MTLStoreAction.DontCare);
        SendMessageVoid(depth, AppKitInterop.GetSelector("setClearDepth:"), 1.0);

        return AppKitInterop.SendMessage(commandBuffer, AppKitInterop.GetSelector("renderCommandEncoderWithDescriptor:"), pass);
    }

    /// <summary>
    /// Sets the pipeline and depth-stencil state of subsequent draws.
    /// </summary>
    public static void SetStates(nint encoder, nint pipelineState, nint depthStencilState)
    {
        AppKitInterop.SendMessageVoid(encoder, AppKitInterop.GetSelector("setRenderPipelineState:"), pipelineState);
        AppKitInterop.SendMessageVoid(encoder, AppKitInterop.GetSelector("setDepthStencilState:"), depthStencilState);
    }

    /// <summary>
    /// Binds a copy of <paramref name="bytes"/> to the vertex stage at <paramref name="index"/>. Metal limits the copy to 4 KB.
    /// </summary>
    public static unsafe void SetVertexBytes(nint encoder, ReadOnlySpan<byte> bytes, int index)
    {
        fixed (byte* pointer = bytes)
        {
            SendMessageVoid(encoder, AppKitInterop.GetSelector("setVertexBytes:length:atIndex:"), (nint)pointer, (ulong)bytes.Length, (ulong)index);
        }
    }

    /// <summary>
    /// Binds a copy of <paramref name="bytes"/> to the fragment stage at <paramref name="index"/>. Metal limits the copy to 4 KB.
    /// </summary>
    public static unsafe void SetFragmentBytes(nint encoder, ReadOnlySpan<byte> bytes, int index)
    {
        fixed (byte* pointer = bytes)
        {
            SendMessageVoid(encoder, AppKitInterop.GetSelector("setFragmentBytes:length:atIndex:"), (nint)pointer, (ulong)bytes.Length, (ulong)index);
        }
    }

    /// <summary>
    /// Binds <paramref name="buffer"/> to the vertex stage at <paramref name="index"/>.
    /// </summary>
    public static void SetVertexBuffer(nint encoder, nint buffer, int index)
    {
        SendMessageVoid(encoder, AppKitInterop.GetSelector("setVertexBuffer:offset:atIndex:"), buffer, 0, (ulong)index);
    }

    /// <summary>
    /// Binds <paramref name="buffer"/> to the fragment stage at <paramref name="index"/>.
    /// </summary>
    public static void SetFragmentBuffer(nint encoder, nint buffer, int index)
    {
        SendMessageVoid(encoder, AppKitInterop.GetSelector("setFragmentBuffer:offset:atIndex:"), buffer, 0, (ulong)index);
    }

    /// <summary>
    /// Draws <paramref name="vertexCount"/> vertices as triangles.
    /// </summary>
    public static void DrawTriangles(nint encoder, int vertexCount)
    {
        SendMessageVoid(
            encoder,
            AppKitInterop.GetSelector("drawPrimitives:vertexStart:vertexCount:"),
            (ulong)MTLPrimitiveType.Triangle,
            0,
            (ulong)vertexCount);
    }

    /// <summary>
    /// Draws <paramref name="instanceCount"/> instances of the triangles listed by 16-bit indices in <paramref name="indexBuffer"/>.
    /// </summary>
    public static void DrawIndexedTriangles(nint encoder, int indexCount, nint indexBuffer, int instanceCount)
    {
        SendMessageVoid(
            encoder,
            AppKitInterop.GetSelector("drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:instanceCount:"),
            (ulong)MTLPrimitiveType.Triangle,
            (ulong)indexCount,
            (ulong)MTLIndexType.UInt16,
            indexBuffer,
            0,
            (ulong)instanceCount);
    }

    /// <summary>
    /// Ends encoding of a render pass.
    /// </summary>
    public static void EndEncoding(nint encoder)
    {
        _ = AppKitInterop.SendMessage(encoder, AppKitInterop.GetSelector("endEncoding"));
    }

    /// <summary>
    /// Releases a retained Objective-C object.
    /// </summary>
    public static void Release(nint instance)
    {
        if (instance != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(instance, AppKitInterop.GetSelector("release"));
        }
    }

    private static nint CreateInstance(string className)
    {
        nint instance = AppKitInterop.SendMessage(AppKitInterop.GetClass(className), AppKitInterop.GetSelector("alloc"));
        return AppKitInterop.SendMessage(instance, AppKitInterop.GetSelector("init"));
    }

    private static nint GetFirstColorAttachment(nint descriptor)
    {
        nint attachments = AppKitInterop.SendMessage(descriptor, AppKitInterop.GetSelector("colorAttachments"));
        return SendMessage(attachments, AppKitInterop.GetSelector("objectAtIndexedSubscript:"), 0UL);
    }

    private static void ThrowIfFailed(nint result, nint error, string message)
    {
        if (result != nint.Zero)
        {
            return;
        }

        string? description = error == nint.Zero
            ? null
            : Marshal.PtrToStringUTF8(AppKitInterop.SendMessage(
                AppKitInterop.SendMessage(error, AppKitInterop.GetSelector("localizedDescription")),
                AppKitInterop.GetSelector("UTF8String")));
        throw new InvalidOperationException(description is null ? message : $"{message} {description}");
    }
}
