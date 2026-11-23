using System.Reflection;
using System.Runtime.InteropServices;
using NativeUI.Graphics3D;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Holds the Metal objects that every 3D scene on a device draws with: the sky and mesh pipeline states, their depth
/// states, and the unit-box geometry. Created once per <see cref="MetalDevice"/> and kept for its lifetime.
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
        SkyPipelineState = CreatePipelineState(device, library, "skyVertex", "skyFragment", isPremultipliedBlendEnabled: false);
        MeshPipelineState = CreatePipelineState(device, library, "meshVertex", "meshFragment", isPremultipliedBlendEnabled: true);
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

    private static nint CreatePipelineState(
        nint device,
        nint library,
        string vertexName,
        string fragmentName,
        bool isPremultipliedBlendEnabled)
    {
        nint vertexFunction = MetalInterop.CreateFunction(library, vertexName);
        nint fragmentFunction = MetalInterop.CreateFunction(library, fragmentName);
        try
        {
            return MetalInterop.CreateRenderPipelineState(
                device,
                vertexFunction,
                fragmentFunction,
                MTLPixelFormat.BGRA8Unorm,
                SampleCount,
                isPremultipliedBlendEnabled);
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
