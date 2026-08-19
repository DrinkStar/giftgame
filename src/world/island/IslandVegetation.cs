// Original — ridge-aligned vegetation sampling
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   One heightmap cell with slope and ridge-orientation derived from the
///   same grid <see cref="IslandMeshBuilder"/> uses. Placement scores prefer
///   mountain crests (positive Laplacian) and yaw trees/grass along the
///   contour (perpendicular to the height gradient) instead of scattering
///   on a flat disc.
/// </summary>
public readonly struct VegetationSample
{
    public VegetationSample(
        Vector2I grid,
        Vector3 local,
        float height01,
        float radialMeters,
        float slope,
        Vector2 ridgeDir,
        float ridgeScore,
        float yaw)
    {
        Grid = grid;
        Local = local;
        Height01 = height01;
        RadialMeters = radialMeters;
        Slope = slope;
        RidgeDir = ridgeDir;
        RidgeScore = ridgeScore;
        Yaw = yaw;
    }

    public Vector2I Grid { get; }
    public Vector3 Local { get; }
    public float Height01 { get; }
    public float RadialMeters { get; }
    /// <summary>World-space |∇Y| (rise/run). 1 ≈ 45°.</summary>
    public float Slope { get; }
    /// <summary>Unit XZ along the contour / ridge (perpendicular to slope).</summary>
    public Vector2 RidgeDir { get; }
    public float RidgeScore { get; }
    /// <summary>Y-axis yaw that aligns a prop with <see cref="RidgeDir"/>.</summary>
    public float Yaw { get; }
}

/// <summary>
///   Deterministic vegetation queries over an island heightmap. Same spec +
///   heightmap always yields the same sample list and the same greedy picks.
/// </summary>
public static class IslandVegetation
{
    /// <summary>
    ///   Walks interior heightmap cells and records slope / ridge data.
    ///   Sea cells (<c>h01 &lt; 0.46</c>) are skipped.
    /// </summary>
    public static VegetationSample[] Analyze(IslandSpec spec, float[] heightmap, int resolution)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(heightmap);
        if (resolution < 3)
            throw new ArgumentOutOfRangeException(nameof(resolution), "resolution must be >= 3.");
        if (heightmap.Length != resolution * resolution)
            throw new ArgumentException("heightmap length must equal resolution².", nameof(heightmap));

        float cell = spec.Radius * 2f / (resolution - 1);
        float half = (resolution - 1) * 0.5f;
        var list = new List<VegetationSample>(resolution * resolution / 3);

        for (int row = 1; row < resolution - 1; row++)
        {
            for (int col = 1; col < resolution - 1; col++)
            {
                float h01 = heightmap[row * resolution + col];
                if (h01 < 0.46f)
                    continue;

                float lx = (col - half) * cell;
                float lz = (row - half) * cell;
                float yC = WorldY(heightmap, row, col, resolution, spec);
                float yL = WorldY(heightmap, row, col - 1, resolution, spec);
                float yR = WorldY(heightmap, row, col + 1, resolution, spec);
                float yD = WorldY(heightmap, row - 1, col, resolution, spec);
                float yU = WorldY(heightmap, row + 1, col, resolution, spec);

                float gx = (yR - yL) / (2f * cell);
                float gz = (yU - yD) / (2f * cell);
                float slope = Mathf.Sqrt(gx * gx + gz * gz);
                var ridgeDir = gx * gx + gz * gz < 1e-8f
                    ? Vector2.Right
                    : new Vector2(-gz, gx).Normalized();
                // Positive Laplacian in world Y = local crest (ridge / knoll).
                float lap = 4f * yC - (yL + yR + yU + yD);
                float ridgeScore = Mathf.Max(0f, lap)
                    + h01 * 0.35f
                    + Mathf.Max(0f, 0.7f - slope) * 0.2f;
                float yaw = Mathf.Atan2(ridgeDir.X, ridgeDir.Y);

                list.Add(new VegetationSample(
                    new Vector2I(col, row),
                    new Vector3(lx, yC, lz),
                    h01,
                    new Vector2(lx, lz).Length(),
                    slope,
                    ridgeDir,
                    ridgeScore,
                    yaw));
            }
        }

        return list.ToArray();
    }

    /// <summary>
    ///   Greedy top-score pick with minimum spacing. Sort is
    ///   (score desc, row, col) so the same inputs always yield the same
    ///   list — no RNG required. Returns fewer than <paramref name="count"/>
    ///   when the band is sparse.
    /// </summary>
    public static List<VegetationSample> Pick(
        VegetationSample[] samples,
        Func<VegetationSample, bool> predicate,
        Func<VegetationSample, float> score,
        int count,
        float minSpacing)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(score);
        if (count <= 0)
            return new List<VegetationSample>();

        var ranked = new List<(VegetationSample Sample, float Score)>(samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];
            if (!predicate(sample))
                continue;
            ranked.Add((sample, score(sample)));
        }

        ranked.Sort(CompareRanked);

        var picked = new List<VegetationSample>(count);
        float minSq = minSpacing * minSpacing;
        for (int i = 0; i < ranked.Count && picked.Count < count; i++)
        {
            var sample = ranked[i].Sample;
            if (TooClose(picked, sample, minSq))
                continue;
            picked.Add(sample);
        }

        return picked;
    }

    private static int CompareRanked(
        (VegetationSample Sample, float Score) a,
        (VegetationSample Sample, float Score) b)
    {
        int byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0)
            return byScore;
        int byRow = a.Sample.Grid.Y.CompareTo(b.Sample.Grid.Y);
        if (byRow != 0)
            return byRow;
        return a.Sample.Grid.X.CompareTo(b.Sample.Grid.X);
    }

    private static bool TooClose(
        List<VegetationSample> picked, VegetationSample sample, float minSq)
    {
        for (int i = 0; i < picked.Count; i++)
        {
            float dx = sample.Local.X - picked[i].Local.X;
            float dz = sample.Local.Z - picked[i].Local.Z;
            if (dx * dx + dz * dz < minSq)
                return true;
        }

        return false;
    }

    private static float WorldY(
        float[] heightmap, int row, int col, int resolution, IslandSpec spec) =>
        (heightmap[row * resolution + col] - 0.5f) * spec.HeightScale;
}
