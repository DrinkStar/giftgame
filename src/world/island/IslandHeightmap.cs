// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   Pure height-field generation for one island (T8.5.1). Produces a
///   deterministic 0..1 normalized heightmap, row-major with
///   <c>index = row * resolution + col</c> (col → +X, row → +Z — the same
///   order <see cref="Godot.HeightMapShape3D.MapData"/> expects), consumed
///   by both <see cref="IslandMeshBuilder"/> (mesh Y) and the
///   HeightMapShape3D collision, so the visual and physics surfaces always
///   agree.
///
///   Noise: <see cref="Godot.FastNoiseLite"/> (Simplex + FBM fractal) seeded
///   with the spec's fixed <c>Seed</c> — the same spec always yields the
///   same array. The grid always spans exactly ±<c>spec.Radius</c> meters
///   around the grid center (cell size <c>Radius·2 / (resolution - 1)</c>);
///   the caller (IslandBuilder) is responsible for centering the island body
///   at <c>spec.Center</c>.
///
///   Mask: radial smoothstep falloff from 0.75·Radius to Radius
///   (<see cref="MaskFalloffStart"/>); outside the radius the height is
///   exactly 0. Height formula:
///   <c>h01 = mask · (0.5 + 0.5 · n01)</c> with <c>n01 = (noise + 1) / 2</c>.
///   The <c>0.5</c> baseline keeps the island interior at or above sea level
///   (h01 ≥ 0.5 ⇔ mesh Y ≥ 0) while the falloff zone crosses h01 = 0.5
///   (Y = 0) to form beaches, and outside the mask the terrain drops to the
///   deep basin (h01 = 0 ⇔ Y = -HeightScale / 2).
/// </summary>
public static class IslandHeightmap
{
    /// <summary>Default grid resolution (vertices per axis): 129² total.</summary>
    public const int DefaultResolution = 129;

    /// <summary>
    ///   Radius fraction at which the radial falloff starts. Terrain is at
    ///   full height inside this and smoothsteps to zero between here and
    ///   <see cref="IslandSpec.Radius"/>.
    /// </summary>
    public const float MaskFalloffStart = 0.75f;

    /// <summary>Octave count of the FBM fractal.</summary>
    public const int FractalOctaves = 4;

    /// <summary>
    ///   Generates the normalized heightmap for <paramref name="spec"/>.
    /// </summary>
    /// <param name="spec">Island description; must not be null.</param>
    /// <param name="resolution">
    ///   Grid size per axis (default 129). The world cell size is derived as
    ///   <c>spec.Radius * 2 / (resolution - 1)</c>.
    /// </param>
    /// <returns>
    ///   Row-major <c>float[resolution * resolution]</c> clamped to [0, 1];
    ///   exactly 0 outside the radial mask (see class docs).
    /// </returns>
    public static float[] Generate(IslandSpec spec, int resolution = DefaultResolution)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (resolution < 2)
            throw new ArgumentOutOfRangeException(nameof(resolution), "resolution must be >= 2.");

        float cell = spec.Radius * 2f / (resolution - 1);
        float half = (resolution - 1) * 0.5f;
        float innerRadius = spec.Radius * MaskFalloffStart;

        var noise = new FastNoiseLite
        {
            Seed = (int)spec.Seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = FractalOctaves,
            Frequency = spec.NoiseScale
        };

        var result = new float[resolution * resolution];
        for (int row = 0; row < resolution; row++)
        {
            float localZ = (row - half) * cell;
            for (int col = 0; col < resolution; col++)
            {
                float localX = (col - half) * cell;
                float dist = new Vector2(localX, localZ).Length();

                // 1 outside the falloff (dist <= inner), 0 beyond radius.
                float mask = 1f - Mathf.SmoothStep(innerRadius, spec.Radius, dist);
                float n01 = (noise.GetNoise2D(localX, localZ) + 1f) * 0.5f;
                float h01 = mask * (0.5f + 0.5f * n01);

                result[row * resolution + col] = Mathf.Clamp(h01, 0f, 1f);
            }
        }

        return result;
    }
}
