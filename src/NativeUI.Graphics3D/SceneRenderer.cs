using System.Numerics;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Draws a <see cref="Scene3D"/> as flat-shaded polygons, so every platform that can fill a polygon shows the same image.
/// Faces are sorted back to front with no depth buffer, so meshes that intersect or surround each other can overlap wrongly.
/// </summary>
internal static class SceneRenderer
{
    private const float NearPlane = 0.1f;
    private const float FarPlane = 1000f;

    /// <summary>
    /// The light every surface receives, which keeps a face turned away from all lights visible.
    /// </summary>
    internal const float AmbientLight = 0.25f;

    private const int SkyGradientStops = 48;

    /// <summary>
    /// The faces of a unit cube centered on the origin, each with its outward normal and its corners counter-clockwise as
    /// seen from outside.
    /// </summary>
    internal static readonly (Vector3 Normal, Vector3[] Corners)[] BoxFaces =
    [
        (-Vector3.UnitZ, [new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f)]),
        (Vector3.UnitZ, [new(0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f)]),
        (-Vector3.UnitX, [new(-0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f)]),
        (Vector3.UnitX, [new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f)]),
        (Vector3.UnitY, [new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]),
        (-Vector3.UnitY, [new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)])
    ];

    /// <summary>
    /// Draws <paramref name="scene"/> as <paramref name="camera"/> sees it, filling <paramref name="bounds"/>.
    /// </summary>
    public static void Render(DrawingContext context, Rect bounds, Scene3D scene, PerspectiveCamera camera)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Matrix4x4 viewProjection = GetViewProjection(camera, bounds.Width / bounds.Height);

        List<DirectionalLight> lights = [];
        SceneNode? fog = null;
        GradientSky? sceneSky = null;
        Caustics? caustics = null;
        Environment3D? environment = null;
        Vector3 hemisphereSky = Vector3.Zero;
        Vector3 hemisphereGround = Vector3.Zero;
        foreach (SceneNode item in scene.Items)
        {
            if (item is DirectionalLight light)
            {
                lights.Add(light);
            }
            else if (item is Fog or FogExp2)
            {
                fog = item;
            }
            else if (item is HemisphereLight hemisphere)
            {
                float intensity = (float)hemisphere.Intensity;
                hemisphereSky += ToVector3(SceneLighting.ToLinear(hemisphere.Color)) * intensity;
                hemisphereGround += ToVector3(SceneLighting.ToLinear(hemisphere.GroundColor)) * intensity;
            }
            else if (item is Environment3D sceneEnvironment)
            {
                environment = sceneEnvironment;
            }
            else if (item is Caustics sceneCaustics)
            {
                caustics = sceneCaustics;
            }
            else if (item is GradientSky sky)
            {
                sceneSky = sky;
                DrawSky(context, bounds, sky, camera);
            }
        }

        RenderState state = new(
            camera.Position,
            viewProjection,
            bounds,
            lights,
            hemisphereSky,
            hemisphereGround,
            fog,
            sceneSky,
            caustics,
            environment,
            []);
        foreach (SceneNode item in scene.Items)
        {
            if (item is InstancedMesh { Geometry: Geometry3D instancedGeometry } instancedMesh)
            {
                foreach (MeshInstance instance in instancedMesh.GetInstances())
                {
                    AddVisibleFaces(state, instancedGeometry, instance);
                }
            }
            else if (item is Mesh { Geometry: Geometry3D geometry } mesh)
            {
                Matrix4x4 world = GetGeometryTransform(geometry) * GetMeshTransform(mesh);
                AddVisibleFaces(state, geometry, SceneBuffers.CreateInstance(world, GetSurfaceColor(mesh), mesh.Material));
            }
        }

        state.Faces.Sort(static (first, second) => second.DistanceToCamera.CompareTo(first.DistanceToCamera));
        foreach (VisibleFace face in state.Faces)
        {
            context.FillPolygon(face.Points, new Paint(face.Color));
        }
    }

    /// <summary>
    /// Returns the matrix that maps world positions to clip space for <paramref name="camera"/>, with depth in [0, 1].
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="aspectRatio">The viewport width divided by its height.</param>
    internal static Matrix4x4 GetViewProjection(PerspectiveCamera camera, double aspectRatio)
    {
        Matrix4x4 view = Matrix4x4.CreateLookAtLeftHanded(camera.Position, camera.LookAt, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
            float.DegreesToRadians((float)camera.Fov),
            (float)aspectRatio,
            NearPlane,
            FarPlane);
        return view * projection;
    }

    /// <summary>
    /// Returns the rotation of <paramref name="mesh"/>, which also transforms its normals.
    /// </summary>
    internal static Matrix4x4 GetRotation(Mesh mesh)
    {
        return Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(mesh.Rotation.Y),
            float.DegreesToRadians(mesh.Rotation.X),
            float.DegreesToRadians(mesh.Rotation.Z));
    }

    /// <summary>
    /// Returns the transform from <paramref name="geometry"/>'s own coordinates to its mesh's coordinates: the box size
    /// for a <see cref="BoxGeometry"/>, whose vertices form a unit cube, and the identity otherwise.
    /// </summary>
    internal static Matrix4x4 GetGeometryTransform(Geometry3D? geometry)
    {
        return geometry is BoxGeometry box
            ? Matrix4x4.CreateScale((float)box.Width, (float)box.Height, (float)box.Depth)
            : Matrix4x4.Identity;
    }

    /// <summary>
    /// Returns the transform from <paramref name="mesh"/>'s coordinates to world coordinates: its rotation, then its position.
    /// </summary>
    internal static Matrix4x4 GetMeshTransform(Mesh mesh)
    {
        return GetRotation(mesh) * Matrix4x4.CreateTranslation(mesh.Position);
    }

    /// <summary>
    /// Returns the surface color of <paramref name="mesh"/>, which is white without a <see cref="StandardMaterial"/>.
    /// </summary>
    internal static Color GetSurfaceColor(Mesh mesh)
    {
        return mesh.Material is StandardMaterial material ? material.Color : Colors.White;
    }

    /// <summary>
    /// Fills <paramref name="bounds"/> with the sky seen down the center of the view. The camera never rolls, so a vertical
    /// gradient keeps the horizon level, but elevation away from the center column is approximate.
    /// </summary>
    private static void DrawSky(DrawingContext context, Rect bounds, GradientSky sky, PerspectiveCamera camera)
    {
        Vector3 forward = Vector3.Normalize(camera.LookAt - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        Vector3 up = Vector3.Cross(forward, right);
        float halfViewHeight = MathF.Tan(float.DegreesToRadians((float)camera.Fov) / 2);

        GradientStop[] stops = new GradientStop[SkyGradientStops];
        for (int i = 0; i < stops.Length; i++)
        {
            double offset = i / (double)(stops.Length - 1);
            Vector3 direction = Vector3.Normalize(forward + (up * (float)(1 - (2 * offset)) * halfViewHeight));
            stops[i] = new GradientStop(offset, sky.GetColor(direction));
        }

        context.FillLinearGradient(bounds, new Point(bounds.X, bounds.Y), new Point(bounds.X, bounds.Bottom), stops);
    }

    /// <summary>
    /// Adds the faces of <paramref name="geometry"/> placed and colored by <paramref name="instance"/> that face the camera.
    /// </summary>
    private static void AddVisibleFaces(RenderState state, Geometry3D geometry, MeshInstance instance)
    {
        Matrix4x4 world = instance.World;
        if (geometry is BoxGeometry)
        {
            foreach ((_, Vector3[] corners) in BoxFaces)
            {
                Vector3[] worldCorners = new Vector3[corners.Length];
                for (int i = 0; i < corners.Length; i++)
                {
                    worldCorners[i] = Vector3.Transform(corners[i], world);
                }

                AddFace(state, worldCorners, instance.Color, instance);
            }
        }
        else if (geometry is MeshGeometry3D mesh)
        {
            (SceneVertex[] vertices, uint[] indices) = mesh.GetTriangles();
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3[] worldCorners =
                [
                    Vector3.Transform(vertices[indices[i]].Position, world),
                    Vector3.Transform(vertices[indices[i + 1]].Position, world),
                    Vector3.Transform(vertices[indices[i + 2]].Position, world)
                ];
                Vector4 vertexColor = (vertices[indices[i]].Color + vertices[indices[i + 1]].Color + vertices[indices[i + 2]].Color) / 3;
                AddFace(state, worldCorners, instance.Color * vertexColor, instance);
            }
        }
    }

    /// <summary>
    /// Adds a flat polygon whose world corners are counter-clockwise as seen from its front, unless it faces away from
    /// the camera or crosses the near plane.
    /// </summary>
    private static void AddFace(RenderState state, Vector3[] worldCorners, Vector4 surface, MeshInstance instance)
    {
        Vector3 frontNormal = MeshGeometry3D.GetFrontNormal(worldCorners[0], worldCorners[1], worldCorners[2]);
        if (frontNormal == Vector3.Zero)
        {
            return;
        }

        Vector3 center = Vector3.Zero;
        foreach (Vector3 corner in worldCorners)
        {
            center += corner;
        }

        center /= worldCorners.Length;

        // A face pointing away from the camera is hidden behind the front of its closed surface.
        if (Vector3.Dot(frontNormal, state.CameraPosition - center) <= 0)
        {
            return;
        }

        if (TryProject(worldCorners, state.ViewProjection, state.Bounds, out Point[] points))
        {
            float distance = Vector3.Distance(state.CameraPosition, center);
            Vector4 material = instance.MaterialParameters;
            if (material.Y > 0)
            {
                float shade = SceneNoise.GetDetailShade(center / material.X, material.Y);
                surface = new Vector4(ToVector3(surface) * shade, surface.W);
            }

            Color color = ShadeFace(state, surface, ToVector3(instance.EmissiveColor), Vector3.Normalize(frontNormal), center);
            state.Faces.Add(new VisibleFace(points, color, distance));
        }
    }

    /// <summary>
    /// Maps world positions to logical coordinates, and returns false when any lies behind the camera, because the face
    /// would need clipping against the near plane to draw correctly.
    /// </summary>
    private static bool TryProject(Vector3[] worldCorners, Matrix4x4 viewProjection, Rect bounds, out Point[] points)
    {
        points = new Point[worldCorners.Length];
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector4 clip = Vector4.Transform(worldCorners[i], viewProjection);
            if (clip.W <= NearPlane)
            {
                return false;
            }

            double normalizedX = clip.X / clip.W;
            double normalizedY = clip.Y / clip.W;
            points[i] = new Point(
                bounds.X + ((normalizedX + 1) / 2 * bounds.Width),
                bounds.Y + ((1 - normalizedY) / 2 * bounds.Height));
        }

        return true;
    }

    /// <summary>
    /// Lights a face in linear light as the GPU scene shader does: the surface color times the ambient light, the
    /// hemisphere light, the caustics on a face turned up, and the light each directional source gives a face turned
    /// toward it, plus the emissive color, then blended toward the fog color by its distance.
    /// </summary>
    private static Color ShadeFace(RenderState state, Vector4 surface, Vector3 emissive, Vector3 normal, Vector3 position)
    {
        Vector3 received = new Vector3(AmbientLight)
            + SceneLighting.GetHemisphereLight(normal, state.HemisphereSkyColor, state.HemisphereGroundColor);
        foreach (DirectionalLight light in state.Lights)
        {
            float facing = Vector3.Dot(normal, Vector3.Normalize(light.Direction));
            if (facing > 0)
            {
                received += ToVector3(SceneLighting.ToLinear(light.Color)) * (float)light.Intensity * facing;
            }
        }

        if (state.Caustics is Caustics caustics && normal.Y > 0)
        {
            float scale = (float)Math.Max(caustics.Scale, 0.001);
            float pattern = Caustics.GetPattern(position.X / scale, position.Z / scale, (float)caustics.Time.TotalSeconds);
            received += ToVector3(SceneLighting.ToLinear(caustics.Color)) * (float)caustics.Intensity * pattern * normal.Y;
        }

        Vector3 color = (ToVector3(surface) * received) + emissive;
        if (state.Environment is null)
        {
            color = Vector3.Min(Vector3.One, color);
        }

        float distance = Vector3.Distance(state.CameraPosition, position);
        if (state.Fog is Fog fog)
        {
            float amount = Fog.GetAmount(distance, (float)fog.Near, (float)fog.Far);
            color = Vector3.Lerp(color, ToVector3(SceneLighting.ToLinear(fog.Color)), amount);
        }
        else if (state.Fog is FogExp2 exponentialFog)
        {
            Vector3 fogColor = ToVector3(SceneLighting.ToLinear(exponentialFog.Color));
            float aerialPerspective = Math.Clamp((float)exponentialFog.AerialPerspective, 0, 1);
            if (state.Sky is GradientSky sky && aerialPerspective > 0)
            {
                Vector3 skyColor = ToVector3(SceneLighting.ToLinear(sky.GetColor(Vector3.Normalize(position - state.CameraPosition))));
                fogColor = Vector3.Lerp(fogColor, skyColor, aerialPerspective);
            }

            color = Vector3.Lerp(color, fogColor, FogExp2.GetAmount(distance, (float)exponentialFog.Density));
        }

        return SceneLighting.Finish(color, surface.W, state.Environment);
    }

    private static Vector3 ToVector3(Vector4 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    /// <summary>
    /// Holds the camera, lights, and fog of one render, and collects the faces it draws.
    /// </summary>
    private readonly record struct RenderState(
        Vector3 CameraPosition,
        Matrix4x4 ViewProjection,
        Rect Bounds,
        List<DirectionalLight> Lights,
        Vector3 HemisphereSkyColor,
        Vector3 HemisphereGroundColor,
        SceneNode? Fog,
        GradientSky? Sky,
        Caustics? Caustics,
        Environment3D? Environment,
        List<VisibleFace> Faces);

    /// <summary>
    /// Holds one face ready to draw.
    /// </summary>
    private readonly record struct VisibleFace(Point[] Points, Color Color, float DistanceToCamera);
}
