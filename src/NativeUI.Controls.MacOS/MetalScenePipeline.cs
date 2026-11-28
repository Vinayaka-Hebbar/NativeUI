using System.Reflection;
using System.Runtime.InteropServices;
using NativeUI.Graphics3D;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Holds the Metal objects that every 3D scene on a device draws with: the sky and mesh pipeline states in standard and
/// HDR variants, the post-processing pipeline states, the depth states, and the unit-box geometry. Created once per
/// <see cref="MetalDevice"/> and kept for its lifetime.
/// </summary>
internal sealed class MetalScenePipeline
{
    /// <summary>
    /// The number of samples per pixel of scene render targets.
    /// </summary>
    public const int SampleCount = 4;

    private const string ShaderResourceName = "NativeUI.Controls.MacOS.Shaders.Scene.metal";

    /// <summary>
    /// Compiles the scene shaders and uploads the unit-box geometry to <paramref name="device"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The shaders do not compile or a pipeline is invalid for the device.</exception>
    public MetalScenePipeline(nint device)
    {
        nint library = MetalInterop.CreateLibrary(device, ReadShaderSource());
        SkyPipelineState = CreateScenePipelineState(device, library, "skyVertex", "skyFragment", MTLPixelFormat.BGRA8Unorm, PipelineBlending.Replace);
        MeshPipelineState = CreateScenePipelineState(device, library, "meshVertex", "meshFragment", MTLPixelFormat.BGRA8Unorm, PipelineBlending.PremultipliedOver);
        HdrSkyPipelineState = CreateScenePipelineState(device, library, "skyVertex", "skyFragmentHdr", MTLPixelFormat.RGBA16Float, PipelineBlending.Replace);
        HdrMeshPipelineState = CreateScenePipelineState(device, library, "meshVertex", "meshFragmentHdr", MTLPixelFormat.RGBA16Float, PipelineBlending.PremultipliedOver);
        GlowThresholdPipelineState = CreatePostPipelineState(device, library, "glowThreshold", MTLPixelFormat.RGBA16Float, PipelineBlending.Replace);
        GlowDownsamplePipelineState = CreatePostPipelineState(device, library, "glowDownsample", MTLPixelFormat.RGBA16Float, PipelineBlending.Replace);
        GlowUpsamplePipelineState = CreatePostPipelineState(device, library, "glowUpsample", MTLPixelFormat.RGBA16Float, PipelineBlending.Additive);
        LightShaftsPipelineState = CreatePostPipelineState(device, library, "lightShafts", MTLPixelFormat.RGBA16Float, PipelineBlending.Replace);
        CompositePipelineState = CreatePostPipelineState(device, library, "composite", MTLPixelFormat.BGRA8Unorm, PipelineBlending.Replace);
        MetalInterop.Release(library);

        SkyDepthState = MetalInterop.CreateDepthStencilState(device, MTLCompareFunction.Always, isDepthWriteEnabled: false);
        MeshDepthState = MetalInterop.CreateDepthStencilState(device, MTLCompareFunction.Less, isDepthWriteEnabled: true);
        BoxVertexBuffer = MetalInterop.CreateBuffer(device, MemoryMarshal.AsBytes(SceneBuffers.BoxVertices));
        BoxIndexBuffer = MetalInterop.CreateBuffer(device, MemoryMarshal.AsBytes(SceneBuffers.BoxIndices));
    }

    /// <summary>
    /// Gets the pipeline state that fills the render target with the sky.
    /// </summary>
    public nint SkyPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that draws mesh instances.
    /// </summary>
    public nint MeshPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that fills an HDR render target with the sky in linear light.
    /// </summary>
    public nint HdrSkyPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that draws mesh instances into an HDR render target in unclamped linear light.
    /// </summary>
    public nint HdrMeshPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that keeps the light above the glow threshold at half size.
    /// </summary>
    public nint GlowThresholdPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that halves a glow level.
    /// </summary>
    public nint GlowDownsamplePipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that adds a smaller glow level onto the next larger one.
    /// </summary>
    public nint GlowUpsamplePipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that streams bright light away from the light shaft source.
    /// </summary>
    public nint LightShaftsPipelineState { get; }

    /// <summary>
    /// Gets the pipeline state that combines the HDR image, glow, and light shafts, and tone maps them to sRGB.
    /// </summary>
    public nint CompositePipelineState { get; }

    /// <summary>
    /// Gets the depth state of the sky, which neither tests nor writes depth.
    /// </summary>
    public nint SkyDepthState { get; }

    /// <summary>
    /// Gets the depth state of meshes, which keeps the nearest surface.
    /// </summary>
    public nint MeshDepthState { get; }

    /// <summary>
    /// Gets the buffer of <see cref="SceneBuffers.BoxVertices"/>.
    /// </summary>
    public nint BoxVertexBuffer { get; }

    /// <summary>
    /// Gets the buffer of <see cref="SceneBuffers.BoxIndices"/>.
    /// </summary>
    public nint BoxIndexBuffer { get; }

    private static nint CreateScenePipelineState(
        nint device,
        nint library,
        string vertexName,
        string fragmentName,
        MTLPixelFormat colorFormat,
        PipelineBlending blending)
    {
        return CreatePipelineState(device, library, vertexName, fragmentName, colorFormat, MTLPixelFormat.Depth32Float, SampleCount, blending);
    }

    private static nint CreatePostPipelineState(
        nint device,
        nint library,
        string fragmentName,
        MTLPixelFormat colorFormat,
        PipelineBlending blending)
    {
        return CreatePipelineState(device, library, "postVertex", fragmentName, colorFormat, MTLPixelFormat.Invalid, 1, blending);
    }

    private static nint CreatePipelineState(
        nint device,
        nint library,
        string vertexName,
        string fragmentName,
        MTLPixelFormat colorFormat,
        MTLPixelFormat depthFormat,
        int sampleCount,
        PipelineBlending blending)
    {
        nint vertexFunction = MetalInterop.CreateFunction(library, vertexName);
        nint fragmentFunction = MetalInterop.CreateFunction(library, fragmentName);
        try
        {
            return MetalInterop.CreateRenderPipelineState(
                device,
                vertexFunction,
                fragmentFunction,
                colorFormat,
                depthFormat,
                sampleCount,
                blending);
        }
        finally
        {
            MetalInterop.Release(vertexFunction);
            MetalInterop.Release(fragmentFunction);
        }
    }

    private static string ReadShaderSource()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ShaderResourceName)
            ?? throw new InvalidOperationException($"The shader resource '{ShaderResourceName}' is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
