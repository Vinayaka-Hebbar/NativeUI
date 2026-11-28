using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NativeUI.Graphics3D;
using NativeUI.Layout;
using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Renders the 3D scenes of one window with Metal into pooled textures, and draws each texture into the window's Skia frame.
/// Keeps the GPU buffers of each <see cref="MeshGeometry3D"/> and <see cref="InstancedMesh"/> until the node changes.
/// A frame starts with <see cref="BeginFrame"/> and ends with <see cref="EndFrame"/>, which releases the render targets and
/// buffers the frame did not use.
/// </summary>
internal sealed class MetalSceneRenderer : IDisposable
{
    private const int GlowLevelCount = 6;

    private static readonly MTLClearColor s_transparent = new(0, 0, 0, 0);

    private readonly MetalDevice _device;
    private readonly SceneBuffers _buffers = new();
    private readonly List<RenderTarget> _targets = [];
    private readonly Dictionary<MeshGeometry3D, GeometryBuffers> _geometries = [];
    private readonly Dictionary<InstancedMesh, VersionedBuffer> _instances = [];

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

        foreach (GeometryBuffers geometry in _geometries.Values)
        {
            geometry.IsUsed = false;
        }

        foreach (VersionedBuffer instances in _instances.Values)
        {
            instances.IsUsed = false;
        }
    }

    /// <summary>
    /// Releases the render targets and buffers the frame did not use.
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

        ReleaseUnused(_geometries);
        ReleaseUnused(_instances);
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
        PostProcessFrame? post = _buffers.Environment is Environment3D environment
            ? _buffers.CreatePostProcessFrame(environment, camera.Position, bounds.Width / bounds.Height)
            : null;
        Encode(target, post);

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

        foreach (GeometryBuffers geometry in _geometries.Values)
        {
            geometry.Dispose();
        }

        foreach (VersionedBuffer instances in _instances.Values)
        {
            instances.Dispose();
        }

        _targets.Clear();
        _geometries.Clear();
        _instances.Clear();
    }

    private static void ReleaseUnused<TKey, TValue>(Dictionary<TKey, TValue> buffers)
        where TKey : notnull
        where TValue : IDisposable, IFrameResource
    {
        List<TKey> unused = [];
        foreach (KeyValuePair<TKey, TValue> entry in buffers)
        {
            if (!entry.Value.IsUsed)
            {
                unused.Add(entry.Key);
            }
        }

        foreach (TKey key in unused)
        {
            buffers[key].Dispose();
            buffers.Remove(key);
        }
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
    /// Commits one command buffer that draws the sky and then every batch into <paramref name="target"/>. The command
    /// buffer is committed before Skia flushes the frame on the same queue, which completes the texture before Skia
    /// samples it.
    /// </summary>
    private void Encode(RenderTarget target, PostProcessFrame? post)
    {
        MetalScenePipeline pipeline = _device.ScenePipeline;
        ReadOnlySpan<byte> frame = MemoryMarshal.AsBytes(new ReadOnlySpan<SceneFrame>(in _buffers.Frame));
        ReadOnlySpan<SceneBatch> batches = _buffers.Batches;
        bool isHdr = post is not null;
        if (isHdr)
        {
            target.EnsureHdrTextures(_device.Device);
        }

        // The command buffer, render passes, and encoders are autoreleased.
        nint pool = MetalInterop.AutoreleasePoolPush();
        try
        {
            nint commandBuffer = MetalInterop.CreateCommandBuffer(_device.CommandQueue);
            nint encoder = MetalInterop.BeginRenderPass(
                commandBuffer,
                isHdr ? target.HdrColorTexture : target.ColorTexture,
                isHdr ? target.HdrResolveTexture : target.ResolveTexture,
                target.DepthTexture,
                s_transparent);

            MetalInterop.SetStates(encoder, isHdr ? pipeline.HdrSkyPipelineState : pipeline.SkyPipelineState, pipeline.SkyDepthState);
            MetalInterop.SetFragmentBytes(encoder, frame, 0);
            MetalInterop.DrawTriangles(encoder, 3);

            nint frameInstanceBuffer = nint.Zero;
            nint lightBuffer = nint.Zero;
            if (!batches.IsEmpty)
            {
                // A light buffer must not be empty. The shader reads only the first LightCount lights.
                ReadOnlySpan<SceneLight> lights = _buffers.Lights.IsEmpty ? [default] : _buffers.Lights;
                lightBuffer = MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(lights));
                if (!_buffers.Instances.IsEmpty)
                {
                    frameInstanceBuffer = MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(_buffers.Instances));
                }

                MetalInterop.SetStates(encoder, isHdr ? pipeline.HdrMeshPipelineState : pipeline.MeshPipelineState, pipeline.MeshDepthState);
                MetalInterop.SetVertexBytes(encoder, frame, 2);
                MetalInterop.SetFragmentBytes(encoder, frame, 0);
                MetalInterop.SetFragmentBuffer(encoder, lightBuffer, 1);
                foreach (SceneBatch batch in batches)
                {
                    DrawBatch(encoder, pipeline, batch, frameInstanceBuffer);
                }
            }

            MetalInterop.EndEncoding(encoder);
            if (post is PostProcessFrame postFrame)
            {
                EncodePostProcessing(commandBuffer, pipeline, target, postFrame);
            }

            MetalInterop.Commit(commandBuffer);

            // The committed command buffer retains the buffers it reads.
            MetalInterop.Release(frameInstanceBuffer);
            MetalInterop.Release(lightBuffer);
        }
        finally
        {
            MetalInterop.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>
    /// Encodes the glow chain, the light shafts, and the composite pass, which writes the finished sRGB image into the
    /// target's resolve texture. Disabled effects are skipped and their inputs are bound to the scene with zero weight.
    /// </summary>
    private static void EncodePostProcessing(nint commandBuffer, MetalScenePipeline pipeline, RenderTarget target, PostProcessFrame post)
    {
        ReadOnlySpan<byte> postBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<PostProcessFrame>(in post));
        nint glow = target.HdrResolveTexture;
        if (post.Adjustments.W > 0)
        {
            IReadOnlyList<GlowLevel> levels = target.GlowLevels;
            DrawPostPass(commandBuffer, pipeline.GlowThresholdPipelineState, levels[0].Texture, MTLLoadAction.DontCare, postBytes, target.HdrResolveTexture, GetTexel(target.PixelWidth, target.PixelHeight));
            for (int i = 1; i < levels.Count; i++)
            {
                DrawPostPass(commandBuffer, pipeline.GlowDownsamplePipelineState, levels[i].Texture, MTLLoadAction.DontCare, postBytes, levels[i - 1].Texture, levels[i - 1].Texel);
            }

            for (int i = levels.Count - 1; i > 0; i--)
            {
                DrawPostPass(commandBuffer, pipeline.GlowUpsamplePipelineState, levels[i - 1].Texture, MTLLoadAction.Load, postBytes, levels[i].Texture, levels[i].Texel);
            }

            glow = levels[0].Texture;
        }

        nint shafts = target.HdrResolveTexture;
        if (post.LightShaftColor.W > 0 && post.LightShaftColor.X + post.LightShaftColor.Y + post.LightShaftColor.Z > 0)
        {
            DrawPostPass(commandBuffer, pipeline.LightShaftsPipelineState, target.ShaftTexture, MTLLoadAction.DontCare, postBytes, target.HdrResolveTexture, GetTexel(target.PixelWidth, target.PixelHeight));
            shafts = target.ShaftTexture;
        }

        nint encoder = MetalInterop.BeginPostPass(commandBuffer, target.ResolveTexture, MTLLoadAction.DontCare);
        MetalInterop.SetPipelineState(encoder, pipeline.CompositePipelineState);
        MetalInterop.SetFragmentTexture(encoder, target.HdrResolveTexture, 0);
        MetalInterop.SetFragmentTexture(encoder, glow, 1);
        MetalInterop.SetFragmentTexture(encoder, shafts, 2);
        MetalInterop.SetFragmentBytes(encoder, postBytes, 0);
        MetalInterop.DrawTriangles(encoder, 3);
        MetalInterop.EndEncoding(encoder);
    }

    private static void DrawPostPass(
        nint commandBuffer,
        nint pipelineState,
        nint destination,
        MTLLoadAction loadAction,
        ReadOnlySpan<byte> postBytes,
        nint source,
        Vector2 sourceTexel)
    {
        nint encoder = MetalInterop.BeginPostPass(commandBuffer, destination, loadAction);
        MetalInterop.SetPipelineState(encoder, pipelineState);
        MetalInterop.SetFragmentTexture(encoder, source, 0);
        MetalInterop.SetFragmentBytes(encoder, postBytes, 0);
        MetalInterop.SetFragmentBytes(encoder, MemoryMarshal.AsBytes(new ReadOnlySpan<Vector2>(in sourceTexel)), 1);
        MetalInterop.DrawTriangles(encoder, 3);
        MetalInterop.EndEncoding(encoder);
    }

    private static Vector2 GetTexel(int pixelWidth, int pixelHeight)
    {
        return new Vector2(1f / pixelWidth, 1f / pixelHeight);
    }

    private void DrawBatch(nint encoder, MetalScenePipeline pipeline, SceneBatch batch, nint frameInstanceBuffer)
    {
        nint vertexBuffer = pipeline.BoxVertexBuffer;
        nint indexBuffer = pipeline.BoxIndexBuffer;
        int indexCount = SceneBuffers.BoxIndices.Length;
        if (batch.Geometry is MeshGeometry3D geometry)
        {
            GeometryBuffers buffers = AcquireGeometry(geometry);
            vertexBuffer = buffers.VertexBuffer;
            indexBuffer = buffers.IndexBuffer;
            indexCount = buffers.IndexCount;
        }

        nint instanceBuffer = frameInstanceBuffer;
        int instanceOffset = batch.FirstInstance * Unsafe.SizeOf<MeshInstance>();
        if (batch.Source is InstancedMesh source)
        {
            instanceBuffer = AcquireInstances(source);
            instanceOffset = 0;
        }

        MetalInterop.SetVertexBuffer(encoder, vertexBuffer, 0, 0);
        MetalInterop.SetVertexBuffer(encoder, instanceBuffer, instanceOffset, 1);
        MetalInterop.DrawIndexedTriangles(encoder, indexCount, indexBuffer, batch.InstanceCount);
    }

    /// <summary>
    /// Returns the buffers of <paramref name="geometry"/>, uploading them when it has none or changed since the upload.
    /// </summary>
    private GeometryBuffers AcquireGeometry(MeshGeometry3D geometry)
    {
        if (_geometries.TryGetValue(geometry, out GeometryBuffers? buffers) && buffers.Version != geometry.Version)
        {
            buffers.Dispose();
            buffers = null;
        }

        if (buffers is null)
        {
            (SceneVertex[] vertices, uint[] indices) = geometry.GetTriangles();
            buffers = new GeometryBuffers(
                MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(vertices.AsSpan())),
                MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(indices.AsSpan())),
                indices.Length,
                geometry.Version);
            _geometries[geometry] = buffers;
        }

        buffers.IsUsed = true;
        return buffers;
    }

    /// <summary>
    /// Returns the instance buffer of <paramref name="mesh"/>, uploading it when it has none or changed since the upload.
    /// </summary>
    private nint AcquireInstances(InstancedMesh mesh)
    {
        if (_instances.TryGetValue(mesh, out VersionedBuffer? buffer) && buffer.Version != mesh.Version)
        {
            buffer.Dispose();
            buffer = null;
        }

        if (buffer is null)
        {
            buffer = new VersionedBuffer(
                MetalInterop.CreateBuffer(_device.Device, MemoryMarshal.AsBytes(mesh.GetInstances())),
                mesh.Version);
            _instances[mesh] = buffer;
        }

        buffer.IsUsed = true;
        return buffer.Buffer;
    }

    /// <summary>
    /// Marks a GPU resource that is released at the end of a frame that did not use it.
    /// </summary>
    private interface IFrameResource
    {
        bool IsUsed { get; }
    }

    /// <summary>
    /// Holds the vertex and index buffers uploaded from one version of a <see cref="MeshGeometry3D"/>.
    /// </summary>
    private sealed class GeometryBuffers(nint vertexBuffer, nint indexBuffer, int indexCount, int version) : IDisposable, IFrameResource
    {
        public nint VertexBuffer { get; } = vertexBuffer;

        public nint IndexBuffer { get; } = indexBuffer;

        public int IndexCount { get; } = indexCount;

        public int Version { get; } = version;

        public bool IsUsed { get; set; }

        public void Dispose()
        {
            MetalInterop.Release(VertexBuffer);
            MetalInterop.Release(IndexBuffer);
        }
    }

    /// <summary>
    /// Holds one buffer uploaded from one version of a scene node.
    /// </summary>
    private sealed class VersionedBuffer(nint buffer, int version) : IDisposable, IFrameResource
    {
        public nint Buffer { get; } = buffer;

        public int Version { get; } = version;

        public bool IsUsed { get; set; }

        public void Dispose()
        {
            MetalInterop.Release(Buffer);
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

        /// <summary>
        /// Gets the multisampled HDR color texture, which exists after <see cref="EnsureHdrTextures"/>.
        /// </summary>
        public nint HdrColorTexture { get; private set; }

        /// <summary>
        /// Gets the resolved HDR scene, which exists after <see cref="EnsureHdrTextures"/>.
        /// </summary>
        public nint HdrResolveTexture { get; private set; }

        /// <summary>
        /// Gets the half-size light shaft texture, which exists after <see cref="EnsureHdrTextures"/>.
        /// </summary>
        public nint ShaftTexture { get; private set; }

        /// <summary>
        /// Gets the glow chain from half size down, which exists after <see cref="EnsureHdrTextures"/>.
        /// </summary>
        public IReadOnlyList<GlowLevel> GlowLevels { get; private set; } = [];

        /// <summary>
        /// Creates the HDR scene, light shaft, and glow textures the first time a scene with an environment draws here.
        /// </summary>
        public void EnsureHdrTextures(nint device)
        {
            if (HdrColorTexture != nint.Zero)
            {
                return;
            }

            HdrColorTexture = MetalInterop.CreateTexture(
                device,
                MTLPixelFormat.RGBA16Float,
                PixelWidth,
                PixelHeight,
                MetalScenePipeline.SampleCount,
                MTLTextureUsage.RenderTarget);
            HdrResolveTexture = CreatePostTexture(device, PixelWidth, PixelHeight);
            ShaftTexture = CreatePostTexture(device, Math.Max(1, PixelWidth / 2), Math.Max(1, PixelHeight / 2));

            List<GlowLevel> levels = [];
            int width = PixelWidth;
            int height = PixelHeight;
            while (levels.Count < GlowLevelCount && (width > 1 || height > 1))
            {
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
                levels.Add(new GlowLevel(CreatePostTexture(device, width, height), GetTexel(width, height)));
            }

            GlowLevels = levels;
        }

        public void Dispose()
        {
            MetalInterop.Release(ColorTexture);
            MetalInterop.Release(DepthTexture);
            MetalInterop.Release(ResolveTexture);
            MetalInterop.Release(HdrColorTexture);
            MetalInterop.Release(HdrResolveTexture);
            MetalInterop.Release(ShaftTexture);
            foreach (GlowLevel level in GlowLevels)
            {
                MetalInterop.Release(level.Texture);
            }
        }

        private static nint CreatePostTexture(nint device, int width, int height)
        {
            return MetalInterop.CreateTexture(
                device,
                MTLPixelFormat.RGBA16Float,
                width,
                height,
                1,
                MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead);
        }
    }

    /// <summary>
    /// Holds one level of the glow chain and the size of one of its pixels in texture coordinates.
    /// </summary>
    private readonly record struct GlowLevel(nint Texture, Vector2 Texel);
}
