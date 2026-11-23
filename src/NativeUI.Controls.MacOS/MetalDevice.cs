using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Holds the Metal device, its command queue, and the Skia <see cref="GRContext"/> that every window draws with.
/// Created on the main thread on first use and kept for the lifetime of the process.
/// </summary>
internal sealed class MetalDevice
{
    private static MetalDevice? s_shared;

    private MetalDevice(nint device, nint commandQueue, GRContext context)
    {
        Device = device;
        CommandQueue = commandQueue;
        Context = context;
    }

    /// <summary>
    /// Gets the device shared by every window, creating it on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">The system has no Metal device.</exception>
    public static MetalDevice Shared => s_shared ??= Create();

    /// <summary>
    /// Gets the retained <c>MTLDevice</c>.
    /// </summary>
    public nint Device { get; }

    /// <summary>
    /// Gets the retained <c>MTLCommandQueue</c> that Skia submits to and that presents drawables.
    /// </summary>
    public nint CommandQueue { get; }

    /// <summary>
    /// Gets the Skia context drawing on <see cref="Device"/>.
    /// </summary>
    public GRContext Context { get; }

    private static MetalDevice Create()
    {
        nint device = MetalInterop.CreateSystemDefaultDevice();
        if (device == nint.Zero)
        {
            throw new InvalidOperationException("The system has no Metal device.");
        }

        nint commandQueue = MetalInterop.CreateCommandQueue(device);
        GRMtlBackendContext backend = new()
        {
            DeviceHandle = device,
            QueueHandle = commandQueue
        };
        GRContext context = GRContext.CreateMetal(backend)
            ?? throw new InvalidOperationException("Failed to create the Skia Metal context.");
        return new MetalDevice(device, commandQueue, context);
    }
}
