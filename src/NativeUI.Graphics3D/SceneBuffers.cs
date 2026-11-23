using System.Numerics;
using System.Runtime.InteropServices;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Holds the per-frame values that a GPU scene shader reads as its <c>Frame</c> struct.
/// </summary>
/// <remarks>
/// Matrices are stored as M11 through M44, row by row. A shader reads that memory as the column-major transpose, which
/// applies the matrix to column vectors exactly as System.Numerics applies it to row vectors. Colors are sRGB channels in
/// [0, 1].
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct SceneFrame
{
    /// <summary>
    /// The transform from world positions to clip space.
    /// </summary>
    public Matrix4x4 ViewProjection;

    /// <summary>
    /// The inverse of <see cref="ViewProjection"/>, which maps clip-space points back to world positions.
    /// </summary>
    public Matrix4x4 InverseViewProjection;

    /// <summary>
    /// The sky color directly overhead.
    /// </summary>
    public Vector4 SkyTopColor;

    /// <summary>
    /// The sky color at the horizon.
    /// </summary>
    public Vector4 SkyHorizonColor;

    /// <summary>
    /// The ground color at the horizon.
    /// </summary>
    public Vector4 GroundHorizonColor;

    /// <summary>
    /// The ground color directly below.
    /// </summary>
    public Vector4 GroundColor;

    /// <summary>
    /// The sky curve, the ground curve, the energy multiplier, and 1 when the scene has a sky or 0 when it has none.
    /// </summary>
    public Vector4 SkyParameters;

    /// <summary>
    /// The light every surface receives.
    /// </summary>
    public float AmbientLight;

    /// <summary>
    /// The number of lights in the light buffer.
    /// </summary>
    public uint LightCount;

    /// <summary>
    /// Pads the struct to the 16-byte alignment of the shader struct.
    /// </summary>
    public Vector2 Reserved;
}

/// <summary>
/// Holds one directional light that a GPU scene shader reads as its <c>Light</c> struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SceneLight
{
    /// <summary>
    /// The unit direction from the scene toward the light, with W unused.
    /// </summary>
    public Vector4 Direction;

    /// <summary>
    /// The light's sRGB color.
    /// </summary>
    public Vector4 Color;
}

/// <summary>
/// Holds one mesh instance that a GPU scene shader reads as its <c>Instance</c> struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MeshInstance
{
    /// <summary>
    /// The transform from unit-geometry coordinates to world coordinates.
    /// </summary>
    public Matrix4x4 World;

    /// <summary>
    /// The rotation part of <see cref="World"/>, which transforms normals.
    /// </summary>
    public Matrix4x4 Rotation;

    /// <summary>
    /// The surface's sRGB color with alpha.
    /// </summary>
    public Vector4 Color;
}

/// <summary>
/// Holds one geometry vertex that a GPU scene shader reads as its <c>Vertex</c> struct, with tightly packed vectors.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SceneVertex
{
    /// <summary>
    /// The position in unit-geometry coordinates.
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// The outward unit normal of the vertex's face.
    /// </summary>
    public Vector3 Normal;
}

/// <summary>
/// Packs a <see cref="Scene3D"/> into the buffers a GPU scene shader reads: the frame uniform, the lights, and one instance
/// per box mesh, all drawn with the shared unit-box geometry.
/// </summary>
internal sealed class SceneBuffers
{
    private static readonly SceneVertex[] s_boxVertices = CreateBoxVertices();
    private static readonly ushort[] s_boxIndices = CreateBoxIndices();

    private readonly List<SceneLight> _lights = [];
    private readonly List<MeshInstance> _instances = [];
    private SceneFrame _frame;

    /// <summary>
    /// Gets the unit-box vertices, four per face.
    /// </summary>
    public static ReadOnlySpan<SceneVertex> BoxVertices => s_boxVertices;

    /// <summary>
    /// Gets the unit-box triangle indices, counter-clockwise as seen from outside.
    /// </summary>
    public static ReadOnlySpan<ushort> BoxIndices => s_boxIndices;

    /// <summary>
    /// Gets the frame uniform packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ref readonly SceneFrame Frame => ref _frame;

    /// <summary>
    /// Gets the lights packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ReadOnlySpan<SceneLight> Lights => CollectionsMarshal.AsSpan(_lights);

    /// <summary>
    /// Gets the mesh instances packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ReadOnlySpan<MeshInstance> Instances => CollectionsMarshal.AsSpan(_instances);

    /// <summary>
    /// Packs <paramref name="scene"/> as <paramref name="camera"/> sees it through a viewport of the given aspect ratio.
    /// A later sky replaces an earlier one, as it would paint over it.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="aspectRatio">The viewport width divided by its height.</param>
    public void Update(Scene3D scene, PerspectiveCamera camera, double aspectRatio)
    {
        _lights.Clear();
        _instances.Clear();

        Matrix4x4 viewProjection = SceneRenderer.GetViewProjection(camera, aspectRatio);
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection);
        _frame = new SceneFrame
        {
            ViewProjection = viewProjection,
            InverseViewProjection = inverseViewProjection,
            AmbientLight = SceneRenderer.AmbientLight
        };

        foreach (SceneNode item in scene.Items)
        {
            if (item is GradientSky sky)
            {
                _frame.SkyTopColor = ToVector(sky.TopColor);
                _frame.SkyHorizonColor = ToVector(sky.HorizonColor);
                _frame.GroundHorizonColor = ToVector(sky.GroundHorizonColor);
                _frame.GroundColor = ToVector(sky.GroundColor);
                _frame.SkyParameters = new Vector4((float)sky.SkyCurve, (float)sky.GroundCurve, (float)sky.EnergyMultiplier, 1);
            }
            else if (item is DirectionalLight light)
            {
                _lights.Add(new SceneLight
                {
                    Direction = new Vector4(Vector3.Normalize(light.Direction), 0),
                    Color = ToVector(light.Color)
                });
            }
            else if (item is Mesh { Geometry: BoxGeometry box } mesh)
            {
                Matrix4x4 rotation = SceneRenderer.GetRotation(mesh);
                _instances.Add(new MeshInstance
                {
                    World = SceneRenderer.GetBoxWorld(mesh, box, rotation),
                    Rotation = rotation,
                    Color = ToVector(SceneRenderer.GetSurfaceColor(mesh))
                });
            }
        }

        _frame.LightCount = (uint)_lights.Count;
    }

    private static Vector4 ToVector(Color color)
    {
        (byte red, byte green, byte blue, byte alpha) = color.ToSrgbBytes();
        return new Vector4(red, green, blue, alpha) / 255f;
    }

    private static SceneVertex[] CreateBoxVertices()
    {
        List<SceneVertex> vertices = [];
        foreach ((Vector3 normal, Vector3[] corners) in SceneRenderer.BoxFaces)
        {
            foreach (Vector3 corner in corners)
            {
                vertices.Add(new SceneVertex { Position = corner, Normal = normal });
            }
        }

        return [.. vertices];
    }

    private static ushort[] CreateBoxIndices()
    {
        ushort[] indices = new ushort[SceneRenderer.BoxFaces.Length * 6];
        for (int face = 0; face < SceneRenderer.BoxFaces.Length; face++)
        {
            ushort first = (ushort)(face * 4);
            int offset = face * 6;
            indices[offset] = first;
            indices[offset + 1] = (ushort)(first + 1);
            indices[offset + 2] = (ushort)(first + 2);
            indices[offset + 3] = first;
            indices[offset + 4] = (ushort)(first + 2);
            indices[offset + 5] = (ushort)(first + 3);
        }

        return indices;
    }
}
