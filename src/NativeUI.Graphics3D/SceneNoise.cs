using System.Numerics;

namespace NativeUI.Graphics3D;

/// <summary>
/// Computes the smooth 3D value noise behind <see cref="StandardMaterial.DetailAmount"/>, matching the GPU scene shader.
/// </summary>
internal static class SceneNoise
{
    private const int Octaves = 3;

    /// <summary>
    /// Returns the sum of three octaves of value noise at <paramref name="position"/>, each half as strong and twice as
    /// fine as the last, normalized to [0, 1].
    /// </summary>
    public static float GetFractal(Vector3 position)
    {
        float sum = 0;
        float weight = 0.5f;
        float total = 0;
        for (int octave = 0; octave < Octaves; octave++)
        {
            sum += GetValue(position) * weight;
            total += weight;
            position = (position * 2.03f) + new Vector3(17.1f, 9.2f, 3.7f);
            weight *= 0.5f;
        }

        return sum / total;
    }

    /// <summary>
    /// Returns the color factor of surface detail: 1 when <paramref name="amount"/> is 0, varying by up to
    /// 0.6 × <paramref name="amount"/> either side of 1 with the noise.
    /// </summary>
    public static float GetDetailShade(Vector3 position, float amount)
    {
        return 1 + (amount * (GetFractal(position) - 0.5f) * 1.2f);
    }

    private static float GetValue(Vector3 position)
    {
        Vector3 cell = new(MathF.Floor(position.X), MathF.Floor(position.Y), MathF.Floor(position.Z));
        Vector3 local = position - cell;
        Vector3 blend = local * local * (new Vector3(3) - (2 * local));

        float x00 = float.Lerp(Hash(cell), Hash(cell + new Vector3(1, 0, 0)), blend.X);
        float x10 = float.Lerp(Hash(cell + new Vector3(0, 1, 0)), Hash(cell + new Vector3(1, 1, 0)), blend.X);
        float x01 = float.Lerp(Hash(cell + new Vector3(0, 0, 1)), Hash(cell + new Vector3(1, 0, 1)), blend.X);
        float x11 = float.Lerp(Hash(cell + new Vector3(0, 1, 1)), Hash(cell + new Vector3(1, 1, 1)), blend.X);
        return float.Lerp(float.Lerp(x00, x10, blend.Y), float.Lerp(x01, x11, blend.Y), blend.Z);
    }

    /// <summary>
    /// Returns a pseudo-random value in [0, 1) for an integer lattice point, from an integer hash that gives the same
    /// result in C# and in the shader.
    /// </summary>
    private static float Hash(Vector3 cell)
    {
        uint hash = (uint)(int)cell.X * 73856093u ^ (uint)(int)cell.Y * 19349663u ^ (uint)(int)cell.Z * 83492791u;
        hash ^= hash >> 13;
        hash *= 0x5bd1e995u;
        hash ^= hash >> 15;
        return (hash & 0xFFFFFFu) / 16777216f;
    }
}
