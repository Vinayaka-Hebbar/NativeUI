using System.Runtime.InteropServices;
using NativeUI.Graphics3D;
using NativeUI.Layout;
using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Renders the 3D scenes of one window with Metal into pooled textures, and draws each texture into the window's Skia frame.
/// A frame starts with <see cref="BeginFrame"/> and ends with <see cref="EndFrame"/>, which releases the render targets the
/// frame did not use.
/// </summary>
internal sealed class MetalSceneRenderer : IDisposable
{
    private static readonly MTLClearColor s_transparent = new(0, 0, 0, 0);

    private readonly MetalDevice _device;
    private readonly SceneBuffers _buffers = new();
    private readonly List<RenderTarget> _targets = [];

    /// <summary>
    /// Initializes a renderer drawing with <paramref name="device"/>.
    /// </summary>
    public MetalSceneRenderer(MetalDevice device)
    {
        _device = device;
    }

    /// <summary>
    /// Marks every render target as unused by the frame that starts.
    /// </summary>
    public void BeginFrame()
    {
        foreach (RenderTarget target in _targets)
        {
            target.IsUsed = false;
        }
    }

    /// <summary>
    /// Releases the render targets the frame did not use.
    /// </summary>
    public void EndFrame()
    {
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            if (!_targets[i].IsUsed)
            {
                _targets[i].Dispose();
                _targets.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Renders <paramref name="scene"/> into a render target at the canvas's pixel density and draws it at
    /// <paramref name="bounds"/>.
    /// </summary>
    /// <param name="canvas">The window's frame canvas.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="bounds">The area to fill, in the canvas's current coordinates.</param>
    public void Render(SKCanvas canvas, Scene3D scene, PerspectiveCamera camera, Rect bounds)
    {
        SKMatrix matrix = canvas.TotalMatrix;
        double scale = Math.Sqrt((matrix.ScaleX * matrix.ScaleX) + (matrix.SkewY * matrix.SkewY));
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        RenderTarget target = AcquireTarget(pixelWidth, pixelHeight);

        _buffers.Update(scene, camera, bounds.Width / bounds.Height);
        Encode(target);

        GRMtlTextureInfo textureInfo = new(target.ResolveTexture);
        using GRBackendTexture texture = new(pixelWidth, pixelHeight, false, textureInfo);
        using SKImage? image = SKImage.FromTexture(
            _device.Context,
            texture,
            GRSurfaceOrigin.TopLeft,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        if (image is not null)
        {
            SKRect destination = new((float)bounds.X, (float)bounds.Y, (float)bounds.Right, (float)bounds.Bottom);
            canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear), null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (RenderTarget target in _targets)
        {
            target.Dispose();
        }

        _targets.Clear();
    }

    private RenderTarget AcquireTarget(int pixelWidth, int pixelHeight)
    {
        foreach (RenderTarget target in _targets)
        {
            if (!target.IsUsed && target.PixelWidth == pixelWidth && target.PixelHeight == pixelHeight)
            {
                target.IsUsed = true;
                return target;
            }
        }

        RenderTarget created = new(_device.Device, pixelWidth, pixelHeight) { IsUsed = true };
        _targets.Add(created);
        return created;
    }

    /// <summary>
    /// Commits one command buffer that draws the sky and then every mesh instance into <paramref name="target"/>. The
    /// command buffer is committed before Skia flushes the frame on the same queue, which completes the texture before
    /// Skia samples it.
    /// </summary>
    private void Encode(RenderTarget target)
    {
        MetalScenePipeline pipeline = _device.ScenePipeline;
        ReadOnlySpan<byte> frame = MemoryMarshal.AsBytes(new ReadOnlySpan<SceneFrame>(in _buffers.Frame));
        ReadOnlySpan<MeshInstance> instances = _buffers.Instances;

        // The command buffer, render pass, and encoder are autoreleased.
        nint pool = MetalInterop.AutoreleasePoolPush();
        try
        {
            nint commandBuffer = MetalInterop.CreateCommandBuffer(_device.CommandQueue);
            nint encoder = MetalInterop.BeginRenderPass(
                commandBuffer,
                target.ColorTexture,
                target.ResolveTexture,
                target.DepthTexture,
                s_transparent);

            MetalInterop.SetStates(encoder, pipeline.SkyPipelineState, pipeline.SkyDepthState);
            MetalInterop.SetFragmentBytes(encoder, frame, 0);
            MetalInterop.DrawTriangles(encoder, 3);

            nint instanceBuffer = nint.Zero;
            nint lightBuffer = nint.Zero;
            if (!instances.IsEmpty)
            {
                // A light buffer must not be empty. The shader reads only the first LightCount lights.
                ReadOnlySpan<SceneLight> lights = _buffers.Lights.IsEmpty ? [default] : _buffers.Lights;
                instanceBuffer = MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(instances));
                lightBuffer = MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(lights));

                MetalInterop.SetStates(encoder, pipeline.MeshPipelineState, pipeline.MeshDepthState);
                MetalInterop.SetVertexBuffer(encoder, pipeline.BoxVertexBuffer, 0);
                MetalInterop.SetVertexBuffer(encoder, instanceBuffer, 1);
                MetalInterop.SetVertexBytes(encoder, frame, 2);
                MetalInterop.SetFragmentBytes(encoder, frame, 0);
                MetalInterop.SetFragmentBuffer(encoder, lightBuffer, 1);
                MetalInterop.DrawIndexedTriangles(encoder, SceneBuffers.BoxIndices.Length, pipeline.BoxIndexBuffer, instances.Length);
            }

            MetalInterop.EndEncoding(encoder);
            MetalInterop.Commit(commandBuffer);

            // The committed command buffer retains the buffers it reads.
            MetalInterop.Release(instanceBuffer);
            MetalInterop.Release(lightBuffer);
        }
        finally
        {
            MetalInterop.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>
    /// Holds the multisampled color, depth, and resolved textures of one scene drawn in a frame.
    /// </summary>
    private sealed class RenderTarget : IDisposable
    {
        public RenderTarget(nint device, int pixelWidth, int pixelHeight)
        {
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            ColorTexture = MetalInterop.CreateTexture(
                device,
                MTLPixelFormat.BGRA8Unorm,
                pixelWidth,
                pixelHeight,
                MetalScenePipeline.SampleCount,
                MTLTextureUsage.RenderTarget);
            DepthTexture = MetalInterop.CreateTexture(
                device,
                MTLPixelFormat.Depth32Float,
                pixelWidth,
                pixelHeight,
                MetalScenePipeline.SampleCount,
                MTLTextureUsage.RenderTarget);
            ResolveTexture = MetalInterop.CreateTexture(
                device,
                MTLPixelFormat.BGRA8Unorm,
                pixelWidth,
                pixelHeight,
                1,
                MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead);
        }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public nint ColorTexture { get; }

        public nint DepthTexture { get; }

        public nint ResolveTexture { get; }

        public bool IsUsed { get; set; }

        public void Dispose()
        {
            MetalInterop.Release(ColorTexture);
            MetalInterop.Release(DepthTexture);
            MetalInterop.Release(ResolveTexture);
        }
    }
}
