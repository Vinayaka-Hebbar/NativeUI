using System.Collections.Immutable;
using System.Numerics;
using NativeUI.Graphics3D;
using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Builds the procedural shapes of <see cref="UnderwaterSceneView"/>. Every triangle lists its vertices counter-clockwise
/// as seen from its front, as <see cref="MeshGeometry3D"/> requires. Vertex colors darken crevices, undersides, and bases
/// and add streaks, and they multiply each instance's color.
/// </summary>
internal static class UnderwaterGeometry
{
    private const int BranchSides = 5;

    /// <summary>
    /// Returns the seabed height at a horizontal position: rolling dunes with smaller ripples, below the origin.
    /// </summary>
    public static float GetSeabedHeight(float x, float z)
    {
        return -2f
            + (2.2f * MathF.Sin(x * 0.035f) * MathF.Cos(z * 0.03f))
            + (0.9f * MathF.Sin((x * 0.11f) + (z * 0.07f)))
            + (0.25f * MathF.Cos((z * 0.43f) - (x * 0.19f)))
            + (0.08f * MathF.Sin((x * 1.3f) + (z * 0.9f)));
    }

    /// <summary>
    /// Returns a square heightfield of <paramref name="cells"/> by <paramref name="cells"/> cells covering
    /// <paramref name="size"/> units, centered on the origin and facing up.
    /// </summary>
    public static MeshGeometry3D CreateSeabed(float size, int cells)
    {
        int columns = cells + 1;
        ImmutableArray<Vector3>.Builder positions = ImmutableArray.CreateBuilder<Vector3>(columns * columns);
        for (int row = 0; row < columns; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                float x = ((column / (float)cells) - 0.5f) * size;
                float z = ((row / (float)cells) - 0.5f) * size;
                positions.Add(new Vector3(x, GetSeabedHeight(x, z), z));
            }
        }

        ImmutableArray<int>.Builder indices = ImmutableArray.CreateBuilder<int>(cells * cells * 6);
        for (int row = 0; row < cells; row++)
        {
            for (int column = 0; column < cells; column++)
            {
                int nearLeft = (row * columns) + column;
                int nearRight = nearLeft + 1;
                int farLeft = nearLeft + columns;
                int farRight = farLeft + 1;
                indices.AddRange(nearLeft, farRight, farLeft);
                indices.AddRange(nearLeft, nearRight, farRight);
            }
        }

        ImmutableArray<Color>.Builder colors = ImmutableArray.CreateBuilder<Color>(positions.Count);
        foreach (Vector3 position in positions)
        {
            float ripples = 0.5f + (0.5f * MathF.Sin((position.X * 0.9f) + (MathF.Sin(position.Z * 0.35f) * 2)));
            float patches = 0.5f + (0.5f * MathF.Sin((position.X * 0.13f) + 1.7f) * MathF.Cos(position.Z * 0.11f));
            float hollow = Math.Clamp((position.Y + 4.5f) / 4.5f, 0, 1);
            float shade = (0.62f + (0.18f * ripples)) * (0.55f + (0.45f * hollow));
            Vector3 tint = Vector3.Lerp(new Vector3(1, 0.97f, 0.9f), new Vector3(0.78f, 0.86f, 0.7f), patches * patches);
            colors.Add(ToColor(tint * shade));
        }

        return new MeshGeometry3D
        {
            Positions = positions.MoveToImmutable(),
            TriangleIndices = indices.MoveToImmutable(),
            Colors = colors.MoveToImmutable()
        };
    }

    /// <summary>
    /// Returns a weathered rock of radius about 1: an icosahedron subdivided twice, pushed in and out by smooth waves and
    /// a little random roughness.
    /// </summary>
    public static MeshGeometry3D CreateRock(int seed)
    {
        float golden = (1 + MathF.Sqrt(5)) / 2;
        List<Vector3> positions =
        [
            new(-1, golden, 0), new(1, golden, 0), new(-1, -golden, 0), new(1, -golden, 0),
            new(0, -1, golden), new(0, 1, golden), new(0, -1, -golden), new(0, 1, -golden),
            new(golden, 0, -1), new(golden, 0, 1), new(-golden, 0, -1), new(-golden, 0, 1)
        ];
        List<int> triangles =
        [
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        ];
        triangles = Subdivide(positions, triangles);
        triangles = Subdivide(positions, triangles);

        triangles = Subdivide(positions, triangles);

        Random random = new(seed);
        float phase = random.NextSingle() * MathF.Tau;
        Vector3[] shaped = new Vector3[positions.Count];
        Color[] colors = new Color[positions.Count];
        for (int i = 0; i < shaped.Length; i++)
        {
            Vector3 direction = Vector3.Normalize(positions[i]);
            float waves = (0.22f * MathF.Sin((direction.X * 3.1f) + phase) * MathF.Sin((direction.Z * 2.7f) - phase))
                + (0.12f * MathF.Sin((direction.Y * 5.3f) + (direction.X * 4.1f) + phase))
                + (0.05f * MathF.Sin((direction.X * 13f) + (direction.Y * 11f) + phase));
            float roughness = 0.04f * random.NextSingle();
            shaped[i] = direction * (0.9f + waves + roughness);

            float crevice = Math.Clamp(0.5f + (waves * 2.2f), 0, 1);
            float underside = Math.Clamp(0.55f + (direction.Y * 0.45f), 0, 1);
            float streaks = 0.85f + (0.15f * MathF.Sin((direction.Y * 22f) + (direction.X * 3f) + phase));
            float algae = Math.Clamp((direction.Y - 0.55f) * 2.5f, 0, 1) * (0.5f + (0.5f * MathF.Sin((direction.X * 7f) + phase)));
            float shade = (0.35f + (0.65f * crevice)) * underside * streaks;
            Vector3 tint = Vector3.Lerp(Vector3.One, new Vector3(0.72f, 0.95f, 0.68f), algae);
            colors[i] = ToColor(tint * shade);
        }

        return new MeshGeometry3D
        {
            Positions = [.. shaped],
            TriangleIndices = OrientAwayFrom(shaped, triangles, static _ => Vector3.Zero),
            Colors = [.. colors]
        };
    }

    /// <summary>
    /// Returns a kelp strand one unit tall: a thin stem standing on the origin with alternating leaves along its length.
    /// </summary>
    public static MeshGeometry3D CreateKelp(int seed)
    {
        Random random = new(seed);
        List<Vector3> positions = [];
        List<int> triangles = [];
        List<Vector3> axisPoints = [];

        AddBranch(positions, triangles, axisPoints, Vector3.Zero, Vector3.UnitY, length: 1, radius: 0.006f, tipRadius: 0.003f);

        const int LeafCount = 44;
        float goldenAngle = MathF.PI * (3 - MathF.Sqrt(5));
        for (int leaf = 0; leaf < LeafCount; leaf++)
        {
            float height = 0.06f + (0.92f * leaf / LeafCount);
            float angle = (leaf * goldenAngle) + (0.3f * random.NextSingle());
            float length = 0.03f + (0.025f * random.NextSingle());
            Vector3 outward = new(MathF.Cos(angle), 0, MathF.Sin(angle));
            Vector3 sideways = new(-outward.Z, 0, outward.X);
            Vector3 root = new(0, height, 0);
            Vector3 middle = root + (outward * length * 0.5f) + new Vector3(0, length * 0.35f, 0);
            Vector3 tip = root + (outward * length) + new Vector3(0, length * 0.5f, 0);
            float halfWidth = 0.006f + (0.004f * random.NextSingle());

            int first = positions.Count;
            positions.Add(root);
            positions.Add(middle + (sideways * halfWidth));
            positions.Add(middle - (sideways * halfWidth));
            positions.Add(tip);
            triangles.AddRange([first, first + 1, first + 3, first, first + 3, first + 2]);
            for (int i = 0; i < 6; i++)
            {
                axisPoints.Add(root - new Vector3(0, 1, 0));
            }
        }

        return new MeshGeometry3D
        {
            Positions = [.. positions],
            TriangleIndices = OrientAwayFrom(positions, triangles, triangle => axisPoints[triangle]),
            Colors = [.. positions.Select(static position => ToColor(new Vector3(0.4f + (0.6f * position.Y))))]
        };
    }

    /// <summary>
    /// Returns a branching coral about one unit tall standing on the origin: tapered limbs that fork three times.
    /// </summary>
    public static MeshGeometry3D CreateCoral(int seed)
    {
        Random random = new(seed);
        List<Vector3> positions = [];
        List<int> triangles = [];
        List<Vector3> axisPoints = [];
        AddCoralLimb(random, positions, triangles, axisPoints, Vector3.Zero, Vector3.UnitY, length: 0.35f, radius: 0.07f, depth: 3);
        float top = positions.Max(static position => position.Y);

        return new MeshGeometry3D
        {
            Positions = [.. positions],
            TriangleIndices = OrientAwayFrom(positions, triangles, triangle => axisPoints[triangle]),
            Colors = [.. positions.Select(position => ToColor(new Vector3(0.45f + (0.55f * position.Y / top))))]
        };
    }

    /// <summary>
    /// Returns a fish about one unit long pointing along +Z: a diamond body and a forked tail.
    /// </summary>
    public static MeshGeometry3D CreateFish()
    {
        Vector3[] positions =
        [
            new(0, 0, 0.55f),
            new(0, 0.2f, 0),
            new(0.08f, 0, 0),
            new(0, -0.16f, 0),
            new(-0.08f, 0, 0),
            new(0, 0, -0.3f),
            new(0, 0.18f, -0.5f),
            new(0, -0.18f, -0.5f)
        ];
        List<int> triangles =
        [
            0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
            5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4,
            5, 6, 7
        ];

        return new MeshGeometry3D
        {
            Positions = [.. positions],
            TriangleIndices = OrientAwayFrom(positions, triangles, static _ => Vector3.Zero)
        };
    }

    /// <summary>
    /// Returns a blade one unit tall that narrows to a point, curving slightly forward, standing on the origin.
    /// </summary>
    public static MeshGeometry3D CreateGrassBlade()
    {
        const int Segments = 5;
        const float BaseHalfWidth = 0.06f;
        ImmutableArray<Vector3>.Builder positions = ImmutableArray.CreateBuilder<Vector3>((Segments * 2) + 1);
        for (int segment = 0; segment < Segments; segment++)
        {
            float height = segment / (float)Segments;
            float halfWidth = BaseHalfWidth * (1 - height);
            float lean = 0.2f * height * height;
            positions.Add(new Vector3(-halfWidth, height, lean));
            positions.Add(new Vector3(halfWidth, height, lean));
        }

        positions.Add(new Vector3(0, 1, 0.2f));

        ImmutableArray<int>.Builder indices = ImmutableArray.CreateBuilder<int>();
        for (int segment = 0; segment < Segments - 1; segment++)
        {
            int left = segment * 2;
            indices.AddRange(left, left + 2, left + 3);
            indices.AddRange(left, left + 3, left + 1);
        }

        int lastLeft = (Segments - 1) * 2;
        indices.AddRange(lastLeft, Segments * 2, lastLeft + 1);

        return new MeshGeometry3D
        {
            Positions = positions.ToImmutable(),
            TriangleIndices = indices.ToImmutable(),
            Colors = [.. positions.Select(static position => ToColor(new Vector3(0.35f + (0.65f * position.Y))))]
        };
    }

    private static Color ToColor(Vector3 channels)
    {
        return Color.FromSrgb(
            Math.Clamp(channels.X, 0, 1),
            Math.Clamp(channels.Y, 0, 1),
            Math.Clamp(channels.Z, 0, 1));
    }

    private static void AddCoralLimb(
        Random random,
        List<Vector3> positions,
        List<int> triangles,
        List<Vector3> axisPoints,
        Vector3 start,
        Vector3 direction,
        float length,
        float radius,
        int depth)
    {
        Vector3 end = start + (direction * length);
        AddBranch(positions, triangles, axisPoints, start, direction, length, radius, radius * 0.7f);
        if (depth == 0)
        {
            return;
        }

        int forks = 2 + random.Next(2);
        for (int fork = 0; fork < forks; fork++)
        {
            float spread = 0.45f + (0.35f * random.NextSingle());
            float turn = (fork * MathF.Tau / forks) + (0.6f * random.NextSingle());
            Vector3 side = Vector3.Normalize(new Vector3(MathF.Cos(turn), 0, MathF.Sin(turn)));
            Vector3 childDirection = Vector3.Normalize(Vector3.Lerp(direction, side, spread) + new Vector3(0, 0.35f, 0));
            AddCoralLimb(random, positions, triangles, axisPoints, end, childDirection, length * 0.78f, radius * 0.68f, depth - 1);
        }
    }

    /// <summary>
    /// Adds a tapered prism along <paramref name="direction"/> with a pointed cap, recording the stem axis point beside
    /// each triangle so it can be oriented outward.
    /// </summary>
    private static void AddBranch(
        List<Vector3> positions,
        List<int> triangles,
        List<Vector3> axisPoints,
        Vector3 start,
        Vector3 direction,
        float length,
        float radius,
        float tipRadius)
    {
        Vector3 reference = MathF.Abs(direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 across = Vector3.Normalize(Vector3.Cross(direction, reference));
        Vector3 other = Vector3.Cross(direction, across);
        Vector3 end = start + (direction * length);
        Vector3 middle = (start + end) / 2;

        int baseRing = positions.Count;
        for (int side = 0; side < BranchSides; side++)
        {
            float angle = side * MathF.Tau / BranchSides;
            Vector3 offset = (across * MathF.Cos(angle)) + (other * MathF.Sin(angle));
            positions.Add(start + (offset * radius));
            positions.Add(end + (offset * tipRadius));
        }

        int cap = positions.Count;
        positions.Add(end + (direction * tipRadius * 2));

        for (int side = 0; side < BranchSides; side++)
        {
            int lower = baseRing + (side * 2);
            int upper = lower + 1;
            int nextLower = baseRing + (((side + 1) % BranchSides) * 2);
            int nextUpper = nextLower + 1;
            triangles.AddRange([lower, nextLower, nextUpper, lower, nextUpper, upper, upper, nextUpper, cap]);
            for (int i = 0; i < 9; i++)
            {
                axisPoints.Add(i < 6 ? middle : end);
            }
        }
    }

    private static List<int> Subdivide(List<Vector3> positions, List<int> triangles)
    {
        Dictionary<(int, int), int> midpoints = [];
        List<int> subdivided = [];
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int first = triangles[i];
            int second = triangles[i + 1];
            int third = triangles[i + 2];
            int firstSecond = GetMidpoint(positions, midpoints, first, second);
            int secondThird = GetMidpoint(positions, midpoints, second, third);
            int thirdFirst = GetMidpoint(positions, midpoints, third, first);
            subdivided.AddRange([first, firstSecond, thirdFirst]);
            subdivided.AddRange([second, secondThird, firstSecond]);
            subdivided.AddRange([third, thirdFirst, secondThird]);
            subdivided.AddRange([firstSecond, secondThird, thirdFirst]);
        }

        return subdivided;
    }

    private static int GetMidpoint(List<Vector3> positions, Dictionary<(int, int), int> midpoints, int first, int second)
    {
        (int, int) key = first < second ? (first, second) : (second, first);
        if (!midpoints.TryGetValue(key, out int index))
        {
            index = positions.Count;
            positions.Add(Vector3.Normalize(positions[first] + positions[second]) * positions[first].Length());
            midpoints[key] = index;
        }

        return index;
    }

    /// <summary>
    /// Returns the triangles reordered where needed so each one's front faces away from its interior point, which
    /// <paramref name="getInteriorPoint"/> returns for the triangle's first index position in <paramref name="triangles"/>.
    /// </summary>
    private static ImmutableArray<int> OrientAwayFrom(
        IReadOnlyList<Vector3> positions,
        List<int> triangles,
        Func<int, Vector3> getInteriorPoint)
    {
        ImmutableArray<int>.Builder oriented = ImmutableArray.CreateBuilder<int>(triangles.Count);
        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            int first = triangles[i];
            int second = triangles[i + 1];
            int third = triangles[i + 2];
            Vector3 center = (positions[first] + positions[second] + positions[third]) / 3;
            Vector3 front = Vector3.Cross(positions[third] - positions[first], positions[second] - positions[first]);
            if (Vector3.Dot(front, center - getInteriorPoint(i)) < 0)
            {
                (second, third) = (third, second);
            }

            oriented.AddRange(first, second, third);
        }

        return oriented.MoveToImmutable();
    }
}
