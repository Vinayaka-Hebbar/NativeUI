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

    // The light every surface receives, so a face turned away from all lights stays visible.
    private const float AmbientLight = 0.25f;

    private const int SkyGradientStops = 48;

    // Each face lists its corners of a unit cube, with its outward normal.
    private static readonly (Vector3 Normal, Vector3[] Corners)[] s_boxFaces =
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

        Matrix4x4 view = Matrix4x4.CreateLookAtLeftHanded(camera.Position, camera.LookAt, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
            float.DegreesToRadians((float)camera.Fov),
            (float)(bounds.Width / bounds.Height),
            NearPlane,
            FarPlane);
        Matrix4x4 viewProjection = view * projection;

        List<DirectionalLight> lights = [];
        List<VisibleFace> faces = [];
        foreach (SceneNode item in scene.Items)
        {
            if (item is DirectionalLight light)
            {
                lights.Add(light);
            }
            else if (item is GradientSky sky)
            {
                DrawSky(context, bounds, sky, camera);
            }
        }

        foreach (SceneNode item in scene.Items)
        {
            if (item is Mesh { Geometry: BoxGeometry box } mesh)
            {
                AddVisibleFaces(mesh, box, camera.Position, viewProjection, bounds, lights, faces);
            }
        }

        faces.Sort(static (first, second) => second.DistanceToCamera.CompareTo(first.DistanceToCamera));
        foreach (VisibleFace face in faces)
        {
            context.FillPolygon(face.Points, new Paint(face.Color));
        }
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

    private static void AddVisibleFaces(
        Mesh mesh,
        BoxGeometry box,
        Vector3 cameraPosition,
        Matrix4x4 viewProjection,
        Rect bounds,
        List<DirectionalLight> lights,
        List<VisibleFace> faces)
    {
        Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(mesh.Rotation.Y),
            float.DegreesToRadians(mesh.Rotation.X),
            float.DegreesToRadians(mesh.Rotation.Z));
        Matrix4x4 world =
            Matrix4x4.CreateScale((float)box.Width, (float)box.Height, (float)box.Depth) *
            rotation *
            Matrix4x4.CreateTranslation(mesh.Position);
        Color surface = mesh.Material is StandardMaterial material ? material.Color : Colors.White;

        foreach ((Vector3 localNormal, Vector3[] corners) in s_boxFaces)
        {
            Vector3 normal = Vector3.TransformNormal(localNormal, rotation);
            Vector3 center = Vector3.Zero;
            Vector3[] worldCorners = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                worldCorners[i] = Vector3.Transform(corners[i], world);
                center += worldCorners[i];
            }

            center /= corners.Length;

            // A face pointing away from the camera is hidden behind the rest of the box.
            if (Vector3.Dot(normal, cameraPosition - center) <= 0)
            {
                continue;
            }

            if (TryProject(worldCorners, viewProjection, bounds, out Point[] points))
            {
                faces.Add(new VisibleFace(points, ShadeFace(surface, normal, lights), Vector3.Distance(cameraPosition, center)));
            }
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
    /// Multiplies the surface color by the ambient light plus the light each source gives a face turned toward it.
    /// </summary>
    private static Color ShadeFace(Color surface, Vector3 normal, List<DirectionalLight> lights)
    {
        Vector3 received = new(AmbientLight);
        foreach (DirectionalLight light in lights)
        {
            float facing = Vector3.Dot(normal, Vector3.Normalize(light.Direction));
            if (facing > 0)
            {
                (byte red, byte green, byte blue, _) = light.Color.ToSrgbBytes();
                received += new Vector3(red, green, blue) / 255f * facing;
            }
        }

        (byte surfaceRed, byte surfaceGreen, byte surfaceBlue, byte surfaceAlpha) = surface.ToSrgbBytes();
        return Color.FromSrgb(
            Math.Min(1, surfaceRed / 255.0 * received.X),
            Math.Min(1, surfaceGreen / 255.0 * received.Y),
            Math.Min(1, surfaceBlue / 255.0 * received.Z),
            surfaceAlpha / 255.0);
    }

    /// <summary>
    /// Holds one face ready to draw.
    /// </summary>
    private readonly record struct VisibleFace(Point[] Points, Color Color, float DistanceToCamera);
}
