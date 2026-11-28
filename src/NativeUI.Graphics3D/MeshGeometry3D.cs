using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Graphics3D;

/// <summary>
/// Describes a surface of triangles. <see cref="TriangleIndices"/> lists three indices into <see cref="Positions"/> per
/// triangle, counter-clockwise as seen from the triangle's front.
/// </summary>
public class MeshGeometry3D : Geometry3D
{
    /// <summary>
    /// Identifies the <see cref="Positions"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<Vector3>> PositionsProperty =
        BindableProperty.Create<MeshGeometry3D, ImmutableArray<Vector3>>(nameof(Positions), defaultValue: []);

    /// <summary>
    /// Identifies the <see cref="Normals"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<Vector3>> NormalsProperty =
        BindableProperty.Create<MeshGeometry3D, ImmutableArray<Vector3>>(nameof(Normals), defaultValue: []);

    /// <summary>
    /// Identifies the <see cref="TriangleIndices"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<int>> TriangleIndicesProperty =
        BindableProperty.Create<MeshGeometry3D, ImmutableArray<int>>(nameof(TriangleIndices), defaultValue: []);

    /// <summary>
    /// Identifies the <see cref="Colors"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<Color>> ColorsProperty =
        BindableProperty.Create<MeshGeometry3D, ImmutableArray<Color>>(nameof(Colors), defaultValue: []);

    private (SceneVertex[] Vertices, uint[] Indices)? _triangles;
    private int _trianglesVersion;

    /// <summary>
    /// Gets or sets the vertex positions.
    /// </summary>
    public ImmutableArray<Vector3> Positions
    {
        get => GetValue(PositionsProperty);
        set => SetValue(PositionsProperty, value);
    }

    /// <summary>
    /// Gets or sets one normal per position. When the count differs from <see cref="Positions"/>, each vertex uses the
    /// average normal of the triangles that share it.
    /// </summary>
    public ImmutableArray<Vector3> Normals
    {
        get => GetValue(NormalsProperty);
        set => SetValue(NormalsProperty, value);
    }

    /// <summary>
    /// Gets or sets one color per position, which multiplies the material or instance color across each triangle. When the
    /// count differs from <see cref="Positions"/>, every vertex is white and the surface keeps its material color.
    /// </summary>
    public ImmutableArray<Color> Colors
    {
        get => GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    /// <summary>
    /// Gets or sets three indices into <see cref="Positions"/> per triangle. A triangle with an index outside
    /// <see cref="Positions"/> is not drawn, and indices after the last complete triangle are ignored.
    /// </summary>
    public ImmutableArray<int> TriangleIndices
    {
        get => GetValue(TriangleIndicesProperty);
        set => SetValue(TriangleIndicesProperty, value);
    }

    /// <summary>
    /// Returns the vertices with their normals and the indices of the triangles to draw, rebuilt only after a change.
    /// </summary>
    internal (SceneVertex[] Vertices, uint[] Indices) GetTriangles()
    {
        if (_triangles is not { } triangles || _trianglesVersion != Version)
        {
            triangles = BuildTriangles();
            _triangles = triangles;
            _trianglesVersion = Version;
        }

        return triangles;
    }

    private (SceneVertex[] Vertices, uint[] Indices) BuildTriangles()
    {
        ImmutableArray<Vector3> positions = Positions.IsDefault ? [] : Positions;
        ImmutableArray<Vector3> normals = Normals.IsDefault ? [] : Normals;
        ImmutableArray<int> triangleIndices = TriangleIndices.IsDefault ? [] : TriangleIndices;
        ImmutableArray<Color> colors = Colors.IsDefault || Colors.Length != positions.Length ? [] : Colors;

        List<uint> indices = [];
        for (int i = 0; i + 2 < triangleIndices.Length; i += 3)
        {
            int first = triangleIndices[i];
            int second = triangleIndices[i + 1];
            int third = triangleIndices[i + 2];
            if (IsIndex(first, positions.Length) && IsIndex(second, positions.Length) && IsIndex(third, positions.Length))
            {
                indices.Add((uint)first);
                indices.Add((uint)second);
                indices.Add((uint)third);
            }
        }

        Vector3[] vertexNormals = normals.Length == positions.Length
            ? [.. normals]
            : ComputeVertexNormals(positions, indices);

        SceneVertex[] vertices = new SceneVertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new SceneVertex
            {
                Position = positions[i],
                Normal = vertexNormals[i],
                Color = colors.IsEmpty ? Vector4.One : SceneLighting.ToLinear(colors[i])
            };
        }

        return (vertices, [.. indices]);
    }

    /// <summary>
    /// Sums the area-weighted front normal of every triangle into its vertices and normalizes the sums. A vertex used by
    /// no triangle points up.
    /// </summary>
    private static Vector3[] ComputeVertexNormals(ImmutableArray<Vector3> positions, List<uint> indices)
    {
        Vector3[] normals = new Vector3[positions.Length];
        for (int i = 0; i < indices.Count; i += 3)
        {
            uint first = indices[i];
            uint second = indices[i + 1];
            uint third = indices[i + 2];
            Vector3 normal = GetFrontNormal(positions[(int)first], positions[(int)second], positions[(int)third]);
            normals[first] += normal;
            normals[second] += normal;
            normals[third] += normal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i] == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(normals[i]);
        }

        return normals;
    }

    /// <summary>
    /// Returns the unnormalized normal on the front of a triangle whose vertices are counter-clockwise as seen from the
    /// front in left-handed coordinates. Its length is twice the triangle's area.
    /// </summary>
    internal static Vector3 GetFrontNormal(Vector3 first, Vector3 second, Vector3 third)
    {
        return Vector3.Cross(third - first, second - first);
    }

    private static bool IsIndex(int index, int count)
    {
        return index >= 0 && index < count;
    }
}
