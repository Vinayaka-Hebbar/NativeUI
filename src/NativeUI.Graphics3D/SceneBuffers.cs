using System.Numerics;
using System.Runtime.InteropServices;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Holds the per-frame values that a GPU scene shader reads as its <c>Frame</c> struct.
/// </summary>
/// <remarks>
/// Matrices are stored as M11 through M44, row by row. A shader reads that memory as the column-major transpose, which
/// applies the matrix to column vectors exactly as System.Numerics applies it to row vectors. Sky colors are sRGB
/// channels in [0, 1], and every other color is in linear light.
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
    /// The camera position, with W unused.
    /// </summary>
    public Vector4 CameraPosition;

    /// <summary>
    /// The color that fogged surfaces fade to.
    /// </summary>
    public Vector4 FogColor;

    /// <summary>
    /// The linear fog's near and far distances, the exponential fog's density, and the <see cref="SceneFogMode"/>.
    /// </summary>
    public Vector4 FogParameters;

    /// <summary>
    /// The summed sky colors of the hemisphere lights, scaled by their intensities.
    /// </summary>
    public Vector4 HemisphereSkyColor;

    /// <summary>
    /// The summed ground colors of the hemisphere lights, scaled by their intensities.
    /// </summary>
    public Vector4 HemisphereGroundColor;

    /// <summary>
    /// The caustics color scaled by its intensity, with W set to 1 when the scene has caustics or 0 when it has none.
    /// </summary>
    public Vector4 CausticsColor;

    /// <summary>
    /// The caustics cell size in world units, the caustics time in seconds, and two unused values.
    /// </summary>
    public Vector4 CausticsParameters;

    /// <summary>
    /// The unit horizontal wind direction, with the wind strength in W.
    /// </summary>
    public Vector4 WindDirection;

    /// <summary>
    /// The wind turbulence, frequency, time in seconds, and 1 when the scene has wind or 0 when it has none.
    /// </summary>
    public Vector4 WindParameters;

    /// <summary>
    /// The light every surface receives.
    /// </summary>
    public float AmbientLight;

    /// <summary>
    /// The number of lights in the light buffer.
    /// </summary>
    public uint LightCount;

    /// <summary>
    /// How much the fog takes the sky color along each view direction, from 0 to 1.
    /// </summary>
    public float FogAerialPerspective;

    /// <summary>
    /// Pads the struct to the 16-byte alignment of the shader struct.
    /// </summary>
    public float Reserved;
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
    /// The light's color in linear light.
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
    /// The inverse transpose of <see cref="World"/>, which keeps transformed normals perpendicular to their surface under
    /// non-uniform scale.
    /// </summary>
    public Matrix4x4 NormalTransform;

    /// <summary>
    /// The surface's color in linear light, with alpha.
    /// </summary>
    public Vector4 Color;

    /// <summary>
    /// The light the surface gives off, in linear light scaled by its intensity, with W unused.
    /// </summary>
    public Vector4 EmissiveColor;

    /// <summary>
    /// The material's detail scale, detail amount, wind response, and an unused value.
    /// </summary>
    public Vector4 MaterialParameters;
}

/// <summary>
/// Holds the values that GPU post-processing passes read as their <c>PostFrame</c> struct, built from an
/// <see cref="Environment3D"/>. Colors are in linear light.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PostProcessFrame
{
    /// <summary>
    /// The exposure, the <see cref="ToneMapper"/>, the brightness, and the contrast.
    /// </summary>
    public Vector4 Tonemap;

    /// <summary>
    /// The saturation, the glow intensity, the glow threshold, and the glow strength, which is 0 when glow is disabled.
    /// </summary>
    public Vector4 Adjustments;

    /// <summary>
    /// The light shaft color scaled by its intensity, with the shafts' visibility from 0 to 1 in W.
    /// </summary>
    public Vector4 LightShaftColor;

    /// <summary>
    /// The light's position in texture coordinates, with the top left at (0, 0), the decay, and an unused value.
    /// </summary>
    public Vector4 LightShaftParameters;

    /// <summary>
    /// The vignette color, with its intensity in W.
    /// </summary>
    public Vector4 VignetteColor;

    /// <summary>
    /// The vignette smoothness, the image width divided by its height, and two unused values.
    /// </summary>
    public Vector4 VignetteParameters;
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

    /// <summary>
    /// The vertex color in linear light, with alpha, which multiplies the surface color.
    /// </summary>
    public Vector4 Color;
}

/// <summary>
/// Identifies one draw call: a geometry and a range of instances.
/// </summary>
/// <param name="Geometry">The triangles to draw, or null for the unit box of <see cref="SceneBuffers.BoxVertices"/>.</param>
/// <param name="Source">
/// The instanced mesh whose <see cref="InstancedMesh.GetInstances"/> holds the instances, or null when they are in
/// <see cref="SceneBuffers.Instances"/>.
/// </param>
/// <param name="FirstInstance">The index of the first instance in <see cref="SceneBuffers.Instances"/>, when it holds them.</param>
/// <param name="InstanceCount">The number of instances.</param>
internal readonly record struct SceneBatch(MeshGeometry3D? Geometry, InstancedMesh? Source, int FirstInstance, int InstanceCount);

/// <summary>
/// Packs a <see cref="Scene3D"/> into the buffers a GPU scene shader reads: the frame uniform, the lights, and draw batches.
/// Box meshes share one batch of the unit box. Each mesh with a <see cref="MeshGeometry3D"/> is a batch of one instance,
/// and each <see cref="InstancedMesh"/> is a batch of its own cached instances.
/// </summary>
internal sealed class SceneBuffers
{
    private static readonly SceneVertex[] s_boxVertices = CreateBoxVertices();
    private static readonly uint[] s_boxIndices = CreateBoxIndices();

    private readonly List<SceneLight> _lights = [];
    private readonly List<MeshInstance> _instances = [];
    private readonly List<SceneBatch> _batches = [];
    private SceneFrame _frame;

    /// <summary>
    /// Gets the unit-box vertices, four per face.
    /// </summary>
    public static ReadOnlySpan<SceneVertex> BoxVertices => s_boxVertices;

    /// <summary>
    /// Gets the unit-box triangle indices, counter-clockwise as seen from outside.
    /// </summary>
    public static ReadOnlySpan<uint> BoxIndices => s_boxIndices;

    /// <summary>
    /// Gets the frame uniform packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ref readonly SceneFrame Frame => ref _frame;

    /// <summary>
    /// Gets the lights packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ReadOnlySpan<SceneLight> Lights => CollectionsMarshal.AsSpan(_lights);

    /// <summary>
    /// Gets the instances of batches without a <see cref="SceneBatch.Source"/>, packed by the most recent <see cref="Update"/>.
    /// </summary>
    public ReadOnlySpan<MeshInstance> Instances => CollectionsMarshal.AsSpan(_instances);

    /// <summary>
    /// Gets the draw batches packed by the most recent <see cref="Update"/>, in scene order with box meshes first.
    /// </summary>
    public ReadOnlySpan<SceneBatch> Batches => CollectionsMarshal.AsSpan(_batches);

    /// <summary>
    /// Gets the environment found by the most recent <see cref="Update"/>, or null when the scene has none. A GPU renderer
    /// draws in high dynamic range and post-processes the image only when the scene has one.
    /// </summary>
    public Environment3D? Environment { get; private set; }

    /// <summary>
    /// Returns the post-processing values of <paramref name="environment"/> for a camera at
    /// <paramref name="cameraPosition"/> and the view-projection of the most recent <see cref="Update"/>.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <param name="cameraPosition">The camera position.</param>
    /// <param name="aspectRatio">The image width divided by its height.</param>
    public PostProcessFrame CreatePostProcessFrame(Environment3D environment, Vector3 cameraPosition, double aspectRatio)
    {
        Vector3 direction = environment.LightShaftDirection == Vector3.Zero
            ? Vector3.UnitY
            : Vector3.Normalize(environment.LightShaftDirection);
        Vector4 clip = Vector4.Transform(new Vector4(cameraPosition + (direction * 500), 1), _frame.ViewProjection);
        float visibility = 0;
        Vector2 lightPosition = new(0.5f, 0.5f);
        if (clip.W > 0)
        {
            Vector2 normalized = new(clip.X / clip.W, clip.Y / clip.W);
            lightPosition = new Vector2((normalized.X * 0.5f) + 0.5f, 0.5f - (normalized.Y * 0.5f));
            float outside = Math.Max(MathF.Abs(normalized.X), MathF.Abs(normalized.Y)) - 1;
            visibility = Math.Clamp(1 - outside, 0, 1);
        }

        Vector4 shaftColor = SceneLighting.ToLinear(environment.LightShaftColor) * (float)environment.LightShaftIntensity;
        shaftColor.W = visibility;
        Vector4 vignetteColor = SceneLighting.ToLinear(environment.VignetteColor);
        vignetteColor.W = Math.Clamp((float)environment.VignetteIntensity, 0, 1);
        return new PostProcessFrame
        {
            Tonemap = new Vector4(
                (float)environment.TonemapExposure,
                (float)environment.TonemapMode,
                (float)environment.AdjustmentBrightness,
                (float)environment.AdjustmentContrast),
            Adjustments = new Vector4(
                (float)environment.AdjustmentSaturation,
                (float)environment.GlowIntensity,
                (float)environment.GlowThreshold,
                environment.IsGlowEnabled ? Math.Clamp((float)environment.GlowStrength, 0, 1) : 0),
            LightShaftColor = shaftColor,
            LightShaftParameters = new Vector4(lightPosition, Math.Clamp((float)environment.LightShaftDecay, 0, 1), 0),
            VignetteColor = vignetteColor,
            VignetteParameters = new Vector4(Math.Clamp((float)environment.VignetteSmoothness, 0.01f, 1), (float)aspectRatio, 0, 0)
        };
    }

    /// <summary>
    /// Returns the packed instance of a surface placed by <paramref name="world"/> with <paramref name="color"/> and the
    /// emission, detail, and wind response of <paramref name="material"/>.
    /// </summary>
    public static MeshInstance CreateInstance(Matrix4x4 world, Color color, Material3D? material)
    {
        Matrix4x4 normalTransform = Matrix4x4.Invert(world, out Matrix4x4 inverse)
            ? Matrix4x4.Transpose(inverse)
            : Matrix4x4.Identity;
        return new MeshInstance
        {
            World = world,
            NormalTransform = normalTransform,
            Color = SceneLighting.ToLinear(color),
            EmissiveColor = material is StandardMaterial standard
                ? SceneLighting.ToLinear(standard.EmissiveColor) * (float)standard.EmissiveIntensity
                : Vector4.Zero,
            MaterialParameters = material is StandardMaterial detailed
                ? new Vector4(
                    (float)Math.Max(detailed.DetailScale, 0.001),
                    Math.Clamp((float)detailed.DetailAmount, 0, 1),
                    (float)detailed.WindResponse,
                    0)
                : new Vector4(1, 0, 0, 0)
        };
    }

    /// <summary>
    /// Packs <paramref name="scene"/> as <paramref name="camera"/> sees it through a viewport of the given aspect ratio.
    /// A later sky, fog, or caustics replaces an earlier one, and hemisphere lights add up.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="aspectRatio">The viewport width divided by its height.</param>
    public void Update(Scene3D scene, PerspectiveCamera camera, double aspectRatio)
    {
        _lights.Clear();
        _instances.Clear();
        _batches.Clear();
        Environment = null;

        Matrix4x4 viewProjection = SceneRenderer.GetViewProjection(camera, aspectRatio);
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection);
        _frame = new SceneFrame
        {
            ViewProjection = viewProjection,
            InverseViewProjection = inverseViewProjection,
            CameraPosition = new Vector4(camera.Position, 1),
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
            else if (item is Fog fog)
            {
                _frame.FogColor = SceneLighting.ToLinear(fog.Color);
                _frame.FogParameters = new Vector4((float)fog.Near, (float)fog.Far, 0, (float)SceneFogMode.Linear);
                _frame.FogAerialPerspective = 0;
            }
            else if (item is FogExp2 exponentialFog)
            {
                _frame.FogColor = SceneLighting.ToLinear(exponentialFog.Color);
                _frame.FogParameters = new Vector4(0, 0, (float)exponentialFog.Density, (float)SceneFogMode.Exponential);
                _frame.FogAerialPerspective = Math.Clamp((float)exponentialFog.AerialPerspective, 0, 1);
            }
            else if (item is WindZone wind)
            {
                Vector3 horizontal = new(wind.Direction.X, 0, wind.Direction.Z);
                horizontal = horizontal == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(horizontal);
                _frame.WindDirection = new Vector4(horizontal, (float)wind.Strength);
                _frame.WindParameters = new Vector4(
                    (float)wind.Turbulence,
                    (float)wind.Frequency,
                    (float)wind.Time.TotalSeconds,
                    1);
            }
            else if (item is Environment3D environment)
            {
                Environment = environment;
            }
            else if (item is Caustics caustics)
            {
                _frame.CausticsColor = SceneLighting.ToLinear(caustics.Color) * (float)caustics.Intensity;
                _frame.CausticsColor.W = 1;
                _frame.CausticsParameters = new Vector4(
                    (float)Math.Max(caustics.Scale, 0.001),
                    (float)caustics.Time.TotalSeconds,
                    0,
                    0);
            }
            else if (item is HemisphereLight hemisphere)
            {
                float intensity = (float)hemisphere.Intensity;
                _frame.HemisphereSkyColor += SceneLighting.ToLinear(hemisphere.Color) * intensity;
                _frame.HemisphereGroundColor += SceneLighting.ToLinear(hemisphere.GroundColor) * intensity;
            }
            else if (item is DirectionalLight light)
            {
                _lights.Add(new SceneLight
                {
                    Direction = new Vector4(Vector3.Normalize(light.Direction), 0),
                    Color = SceneLighting.ToLinear(light.Color) * (float)light.Intensity
                });
            }
            else if (item is Mesh { Geometry: BoxGeometry box } mesh and not InstancedMesh)
            {
                Matrix4x4 world = SceneRenderer.GetGeometryTransform(box) * SceneRenderer.GetMeshTransform(mesh);
                _instances.Add(CreateInstance(world, SceneRenderer.GetSurfaceColor(mesh), mesh.Material));
            }
        }

        if (_instances.Count > 0)
        {
            _batches.Add(new SceneBatch(null, null, 0, _instances.Count));
        }

        foreach (SceneNode item in scene.Items)
        {
            if (item is InstancedMesh instancedMesh)
            {
                AddInstancedBatch(instancedMesh);
            }
            else if (item is Mesh { Geometry: MeshGeometry3D geometry } mesh && geometry.GetTriangles().Indices.Length > 0)
            {
                Matrix4x4 world = SceneRenderer.GetMeshTransform(mesh);
                _batches.Add(new SceneBatch(geometry, null, _instances.Count, 1));
                _instances.Add(CreateInstance(world, SceneRenderer.GetSurfaceColor(mesh), mesh.Material));
            }
        }

        _frame.LightCount = (uint)_lights.Count;
    }

    private void AddInstancedBatch(InstancedMesh mesh)
    {
        int count = mesh.GetInstances().Length;
        if (count == 0)
        {
            return;
        }

        if (mesh.Geometry is BoxGeometry)
        {
            _batches.Add(new SceneBatch(null, mesh, 0, count));
        }
        else if (mesh.Geometry is MeshGeometry3D geometry && geometry.GetTriangles().Indices.Length > 0)
        {
            _batches.Add(new SceneBatch(geometry, mesh, 0, count));
        }
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
                vertices.Add(new SceneVertex { Position = corner, Normal = normal, Color = Vector4.One });
            }
        }

        return [.. vertices];
    }

    private static uint[] CreateBoxIndices()
    {
        uint[] indices = new uint[SceneRenderer.BoxFaces.Length * 6];
        for (int face = 0; face < SceneRenderer.BoxFaces.Length; face++)
        {
            uint first = (uint)(face * 4);
            int offset = face * 6;
            indices[offset] = first;
            indices[offset + 1] = first + 1;
            indices[offset + 2] = first + 2;
            indices[offset + 3] = first;
            indices[offset + 4] = first + 2;
            indices[offset + 5] = first + 3;
        }

        return indices;
    }
}
