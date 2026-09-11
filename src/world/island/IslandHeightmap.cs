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
///   at <c>spec.Center</c>. Land never exists outside Radius: the collision
///   HeightMapShape3D stays Radius-based.
///
///   Mask: seed-deterministic shoreline warp. Angular radius
///   <c>R(θ) = Radius · shape(θ)</c> with <c>shape ∈ [ShapeMin, ShapeMax]</c>
///   from a low-frequency lobe sinusoid plus FastNoiseLite, then a very
///   low-frequency domain warp of (localX, localZ) for bays / headlands.
///   Falloff is <c>1 − SmoothStep(0.75·R(θ), R(θ), dist')</c>. Habitat math
///   still uses the bounding Radius; cells that warp into the sea are
///   skipped by height. Interior is grassland over the 0.5 sea-level
///   baseline, with inland mountain ridges and a seed-derived meandering
///   river that mouths at the warped shoreline. On Main / Tutorial a
///   building plateau is blended to the Game.tscn Ground box top (Y = 0.5).
///   Height formula (then clamp):
///   <c>h01 = mask · (0.5 + 0.5 · n01) + mountain − river</c>, plateau blend
///   last. Beaches form where the falloff crosses h01 = 0.5 (Y = 0).
/// </summary>
public static class IslandHeightmap
{
    /// <summary>Default grid resolution (vertices per axis): 193² total.</summary>
    public const int DefaultResolution = 193;

    /// <summary>
    ///   Radius fraction at which the radial falloff starts, measured against
    ///   the warped shoreline <c>R(θ)</c> rather than the bounding circle.
    /// </summary>
    public const float MaskFalloffStart = 0.75f;

    /// <summary>
    ///   Mix constant so shoreline warp is independent of height FBM,
    ///   mountain ridges, and the river RNG stream.
    /// </summary>
    public const int ShorelineSeedMix = unchecked((int)0x5A0A1E10);

    /// <summary>
    ///   Hard floor of <c>shape(θ)</c>. Islands stay chunky (no needle
    ///   peninsulas); land is still clipped to <see cref="IslandSpec.Radius"/>.
    /// </summary>
    public const float ShapeMin = 0.72f;

    /// <summary>Hard ceiling of <c>shape(θ)</c>: land never exceeds Radius.</summary>
    public const float ShapeMax = 1f;

    /// <summary>Octave count of the FBM fractal.</summary>
    public const int FractalOctaves = 4;

    /// <summary>
    ///   Main-island plateau radius in meters. Matches the 40×40 Ground /
    ///   BuildingSystem grid (corner ≈ 28 m) so spawn and building stay at
    ///   the Ground box top.
    /// </summary>
    public const float BuildingPlateauRadius = 24f;

    /// <summary>
    ///   Main and tutorial islands blend a flat building plateau so campfire
    ///   / bed placement sits on the Ground box top.
    /// </summary>
    public static bool HasBuildingPlateau(IslandTier tier) =>
        tier is IslandTier.Main or IslandTier.Tutorial;

    /// <summary>Harvest island flattens a central field for the farm look.</summary>
    public static bool HasFarmClearing(IslandTier tier) => tier == IslandTier.Harvest;

    /// <summary>
    ///   Per-tier mountain ridge strength. Tutorial stays at 0.42 so the
    ///   dedicated island silhouette is unchanged.
    /// </summary>
    public static float MountainBoost(IslandTier tier) => tier switch
    {
        IslandTier.Harvest => 0.10f,
        IslandTier.Main => 0.34f,
        IslandTier.Ruin => 0.58f,
        IslandTier.Mutant => 0.36f,
        IslandTier.Storm => 0.50f,
        IslandTier.Wild => 0.40f,
        IslandTier.Atoll => 0.08f,
        IslandTier.Wreck => 0.32f,
        IslandTier.Volcano => 0.72f,
        IslandTier.Polar => 0.70f,
        _ => 0.42f
    };

    /// <summary>
    ///   Per-tier floor of <c>shape(θ)</c>. Tutorial stays high so the 24 m
    ///   plateau is fully inland; Harvest is oval; Ruin / Storm cut deeper.
    /// </summary>
    public static float ShapeAmplitudeMin(IslandTier tier) => tier switch
    {
        IslandTier.Tutorial => 0.90f,
        IslandTier.Main => 0.80f,
        IslandTier.Harvest => 0.84f,
        IslandTier.Ruin => 0.72f,
        IslandTier.Mutant => 0.74f,
        IslandTier.Storm => 0.74f,
        IslandTier.Wild => 0.76f,
        IslandTier.Atoll => 0.82f,
        IslandTier.Wreck => 0.74f,
        IslandTier.Volcano => 0.72f,
        IslandTier.Polar => 0.80f,
        _ => 0.80f
    };

    /// <summary>Radial fraction where mountain boost begins (outside the plateau).</summary>
    public const float MountainRadialStart = 0.28f;

    /// <summary>
    ///   Angular shoreline radius in meters: <c>Radius · shape(θ)</c>.
    ///   Domain-warp bays are extra variation on top of this; the heightmap
    ///   still never places land outside <see cref="IslandSpec.Radius"/>.
    /// </summary>
    public static float ShorelineRadius(IslandSpec spec, float theta)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return ShorelineField.FromSpec(spec).RadiusAt(theta);
    }

    /// <summary>
    ///   Generates the normalized heightmap for <paramref name="spec"/>.
    /// </summary>
    /// <param name="spec">Island description; must not be null.</param>
    /// <param name="resolution">
    ///   Grid size per axis (default 193). The world cell size is derived as
    ///   <c>spec.Radius * 2 / (resolution - 1)</c>.
    /// </param>
    /// <returns>
    ///   Row-major <c>float[resolution * resolution]</c> clamped to [0, 1];
    ///   exactly 0 outside the bounding radius (see class docs).
    /// </returns>
    public static float[] Generate(IslandSpec spec, int resolution = DefaultResolution)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (resolution < 2)
            throw new ArgumentOutOfRangeException(nameof(resolution), "resolution must be >= 2.");

        float cell = spec.Radius * 2f / (resolution - 1);
        float half = (resolution - 1) * 0.5f;
        float mountainStart = spec.Radius * MountainRadialStart;
        var shore = ShorelineField.FromSpec(spec);
        var river = RiverPath.FromSpec(spec);
        bool plateau = HasBuildingPlateau(spec.Tier);

        var noise = new FastNoiseLite
        {
            Seed = (int)spec.Seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = FractalOctaves,
            Frequency = spec.NoiseScale
        };

        var mountainNoise = new FastNoiseLite
        {
            Seed = unchecked((int)spec.Seed ^ (int)0xA24BAED5),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 3,
            Frequency = spec.NoiseScale * 0.7f
        };

        // Ground box top is Y = 0.5; Y = (h01 − 0.5) · HeightScale.
        float plateauH01 = spec.HeightScale > 0f
            ? 0.5f + 0.5f / spec.HeightScale
            : 0.5f;

        var result = new float[resolution * resolution];
        for (int row = 0; row < resolution; row++)
        {
            float localZ = (row - half) * cell;
            for (int col = 0; col < resolution; col++)
            {
                float localX = (col - half) * cell;
                float dist = new Vector2(localX, localZ).Length();

                float mask = shore.EvaluateMask(localX, localZ, dist);
                if (plateau && dist < BuildingPlateauRadius)
                    mask = 1f;

                float n01 = (noise.GetNoise2D(localX, localZ) + 1f) * 0.5f;
                float h01 = mask * (0.5f + 0.5f * n01);

                float ridge = (mountainNoise.GetNoise2D(localX, localZ) + 1f) * 0.5f;
                float mountainMask = Mathf.SmoothStep(mountainStart, spec.Radius * 0.55f, dist);
                h01 += mask * ridge * ridge * mountainMask * MountainBoost(spec.Tier) * (1f - h01);

                h01 -= river.Carve(localX, localZ, dist, mask);

                if (plateau && dist < BuildingPlateauRadius)
                {
                    float w = 1f - Mathf.SmoothStep(0f, BuildingPlateauRadius, dist);
                    h01 = Mathf.Lerp(h01, plateauH01, w);
                }

                if (HasFarmClearing(spec.Tier))
                {
                    float fieldInner = spec.Radius * 0.18f;
                    float fieldOuter = spec.Radius * 0.42f;
                    if (dist < fieldOuter)
                    {
                        float w = 1f - Mathf.SmoothStep(fieldInner, fieldOuter, dist);
                        h01 = Mathf.Lerp(h01, 0.58f, w * 0.85f);
                    }
                }

                result[row * resolution + col] = Mathf.Clamp(h01, 0f, 1f);
            }
        }

        return result;
    }

    /// <summary>
    ///   Local XZ of the river polyline at <paramref name="along01"/>
    ///   (0 = inland start, 1 = beach mouth). Same seed as the carve.
    /// </summary>
    public static Vector2 SampleRiverLocal(IslandSpec spec, float along01)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return RiverPath.FromSpec(spec).Sample(Mathf.Clamp(along01, 0f, 1f));
    }

    /// <summary>
    ///   Local XZ on the river bank (half-width + 2.5 m), for drink points.
    ///   <paramref name="side"/> sign chooses left vs right bank.
    /// </summary>
    public static Vector2 SampleRiverBankLocal(IslandSpec spec, float along01, float side)
    {
        ArgumentNullException.ThrowIfNull(spec);
        float sign = side >= 0f ? 1f : -1f;
        return RiverPath.FromSpec(spec).SampleBank(Mathf.Clamp(along01, 0f, 1f), sign);
    }

    /// <summary>
    ///   True when <paramref name="worldXz"/> sits in this island's river
    ///   channel or on the bank (<paramref name="extraReach"/> matches
    ///   <see cref="SampleRiverBankLocal"/>'s +2.5 m). Plateau interiors,
    ///   mask≈0 sea, and points off the polyline are false. Does not place
    ///   land — the caller still requires the player to be inside
    ///   <see cref="IslandSpec.Radius"/>.
    /// </summary>
    public static bool IsNearRiverChannel(
        IslandSpec spec, Vector2 worldXz, float extraReach = 2.5f)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var local = worldXz - spec.Center;
        float dist = local.Length();
        float mask = ShorelineField.FromSpec(spec).EvaluateMask(local.X, local.Y, dist);
        if (mask <= 0.01f)
            return false;

        return RiverPath.FromSpec(spec).Contains(local.X, local.Y, extraReach);
    }

    /// <summary>
    ///   Seed-deterministic angular radius plus low-frequency domain warp.
    ///   <c>shape(θ) = lerp(min, 1, 0.5 + 0.5 · clamp(lobe + noise + jagged))</c>.
    /// </summary>
    private sealed class ShorelineField
    {
        private readonly FastNoiseLite _shapeNoise;
        private readonly FastNoiseLite _domainNoise;
        private readonly float _radius;
        private readonly float _minShape;
        private readonly float _phase;
        private readonly int _lobes;
        private readonly float _lobeWeight;
        private readonly float _noiseWeight;
        private readonly float _jaggedWeight;
        private readonly float _jaggedFreq;
        private readonly float _domainAmp;

        private ShorelineField(
            FastNoiseLite shapeNoise,
            FastNoiseLite domainNoise,
            float radius,
            float minShape,
            float phase,
            int lobes,
            float lobeWeight,
            float noiseWeight,
            float jaggedWeight,
            float jaggedFreq,
            float domainAmp)
        {
            _shapeNoise = shapeNoise;
            _domainNoise = domainNoise;
            _radius = radius;
            _minShape = minShape;
            _phase = phase;
            _lobes = lobes;
            _lobeWeight = lobeWeight;
            _noiseWeight = noiseWeight;
            _jaggedWeight = jaggedWeight;
            _jaggedFreq = jaggedFreq;
            _domainAmp = domainAmp;
        }

        public static ShorelineField FromSpec(IslandSpec spec)
        {
            int seed = unchecked((int)spec.Seed ^ ShorelineSeedMix);
            float minShape = Mathf.Max(ShapeMin, ShapeAmplitudeMin(spec.Tier));
            int lobes;
            float lobeWeight;
            float noiseWeight;
            float jaggedWeight;
            float jaggedFreq;
            float domainFrac;
            switch (spec.Tier)
            {
                case IslandTier.Tutorial:
                    lobes = 3;
                    lobeWeight = 0.45f;
                    noiseWeight = 0.55f;
                    jaggedWeight = 0f;
                    jaggedFreq = 5f;
                    domainFrac = 0.030f;
                    break;
                case IslandTier.Main:
                    lobes = 3;
                    lobeWeight = 0.40f;
                    noiseWeight = 0.60f;
                    jaggedWeight = 0f;
                    jaggedFreq = 5f;
                    domainFrac = 0.045f;
                    break;
                case IslandTier.Harvest:
                    lobes = 2;
                    lobeWeight = 0.70f;
                    noiseWeight = 0.30f;
                    jaggedWeight = 0f;
                    jaggedFreq = 4f;
                    domainFrac = 0.025f;
                    break;
                case IslandTier.Ruin:
                    lobes = 4;
                    lobeWeight = 0.25f;
                    noiseWeight = 0.53f;
                    jaggedWeight = 0.22f;
                    jaggedFreq = 5.5f;
                    domainFrac = 0.055f;
                    break;
                case IslandTier.Mutant:
                    lobes = 4;
                    lobeWeight = 0.55f;
                    noiseWeight = 0.45f;
                    jaggedWeight = 0f;
                    jaggedFreq = 4.5f;
                    domainFrac = 0.050f;
                    break;
                case IslandTier.Storm:
                    lobes = 5;
                    lobeWeight = 0.20f;
                    noiseWeight = 0.45f;
                    jaggedWeight = 0.35f;
                    jaggedFreq = 7f;
                    domainFrac = 0.050f;
                    break;
                case IslandTier.Wild:
                    lobes = 4;
                    lobeWeight = 0.50f;
                    noiseWeight = 0.50f;
                    jaggedWeight = 0.08f;
                    jaggedFreq = 4.5f;
                    domainFrac = 0.048f;
                    break;
                case IslandTier.Atoll:
                    lobes = 2;
                    lobeWeight = 0.62f;
                    noiseWeight = 0.38f;
                    jaggedWeight = 0f;
                    jaggedFreq = 4f;
                    domainFrac = 0.028f;
                    break;
                case IslandTier.Wreck:
                    lobes = 5;
                    lobeWeight = 0.28f;
                    noiseWeight = 0.48f;
                    jaggedWeight = 0.24f;
                    jaggedFreq = 6f;
                    domainFrac = 0.048f;
                    break;
                case IslandTier.Volcano:
                    lobes = 5;
                    lobeWeight = 0.18f;
                    noiseWeight = 0.42f;
                    jaggedWeight = 0.40f;
                    jaggedFreq = 7.5f;
                    domainFrac = 0.055f;
                    break;
                case IslandTier.Polar:
                    lobes = 3;
                    lobeWeight = 0.48f;
                    noiseWeight = 0.40f;
                    jaggedWeight = 0.12f;
                    jaggedFreq = 5f;
                    domainFrac = 0.042f;
                    break;
                default:
                    lobes = 3;
                    lobeWeight = 0.40f;
                    noiseWeight = 0.60f;
                    jaggedWeight = 0f;
                    jaggedFreq = 5f;
                    domainFrac = 0.040f;
                    break;
            }

            var shapeNoise = new FastNoiseLite
            {
                Seed = seed,
                NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = 2,
                Frequency = 1f
            };
            var domainNoise = new FastNoiseLite
            {
                Seed = unchecked(seed ^ 0x11F0C0DE),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = 2,
                Frequency = 1.15f / Mathf.Max(spec.Radius, 1f)
            };
            float phase = (seed & 1023) / 1023f * Mathf.Tau;

            return new ShorelineField(
                shapeNoise,
                domainNoise,
                spec.Radius,
                minShape,
                phase,
                lobes,
                lobeWeight,
                noiseWeight,
                jaggedWeight,
                jaggedFreq,
                spec.Radius * domainFrac);
        }

        public float RadiusAt(float theta) => _radius * Multiplier(theta);

        public float EvaluateMask(float localX, float localZ, float dist)
        {
            if (dist >= _radius)
                return 0f;

            float wx = localX + _domainNoise.GetNoise2D(localX, localZ) * _domainAmp;
            float wz = localZ
                + _domainNoise.GetNoise2D(localX + 37.1f, localZ - 19.7f) * _domainAmp;
            float warpedDist = new Vector2(wx, wz).Length();
            float theta = Mathf.Atan2(wz, wx);
            float outer = RadiusAt(theta);
            float inner = outer * MaskFalloffStart;
            float mask = 1f - Mathf.SmoothStep(inner, outer, warpedDist);
            mask *= 1f - Mathf.SmoothStep(_radius * 0.992f, _radius, dist);
            return mask;
        }

        private float Multiplier(float theta)
        {
            float lobe = Mathf.Sin(_lobes * theta + _phase);
            float c = Mathf.Cos(theta);
            float s = Mathf.Sin(theta);
            float n = _shapeNoise.GetNoise2D(c * 2.2f, s * 2.2f);
            float jagged = 0f;
            if (_jaggedWeight > 0f)
                jagged = _shapeNoise.GetNoise2D(c * _jaggedFreq, s * _jaggedFreq);

            float mix = _lobeWeight * lobe + _noiseWeight * n + _jaggedWeight * jagged;
            float t = 0.5f + 0.5f * Mathf.Clamp(mix, -1f, 1f);
            return Mathf.Lerp(_minShape, ShapeMax, t);
        }
    }

    /// <summary>
    ///   Seed-derived meandering stream from inland toward the beach.
    ///   Built once per Generate so the per-cell loop stays cheap.
    /// </summary>
    private readonly struct RiverPath
    {
        private readonly float _cos;
        private readonly float _sin;
        private readonly float _phase;
        private readonly float _meander;
        private readonly float _halfWidth;
        private readonly float _start;
        private readonly float _end;
        private readonly float _plateau;

        private RiverPath(
            float cos, float sin, float phase, float meander,
            float halfWidth, float start, float end, float plateau)
        {
            _cos = cos;
            _sin = sin;
            _phase = phase;
            _meander = meander;
            _halfWidth = halfWidth;
            _start = start;
            _end = end;
            _plateau = plateau;
        }

        public static RiverPath FromSpec(IslandSpec spec)
        {
            var rng = new Random(unchecked((int)spec.Seed ^ 0x51ED2701));
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float phase = (float)(rng.NextDouble() * Mathf.Tau);
            float plateau = HasBuildingPlateau(spec.Tier) ? BuildingPlateauRadius : 0f;
            // Path length stays Radius-based so inland habitat samples do not
            // collapse onto the spawn-safe ring when a bay pulls the shore in.
            // Carve already no-ops where mask ≈ 0, so the visible mouth is the
            // warped beach rather than a fake circular ring.
            return new RiverPath(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                phase,
                spec.Radius * 0.08f,
                Mathf.Clamp(spec.Radius * 0.045f, 2.5f, 5.5f),
                spec.Radius * 0.22f,
                spec.Radius * 0.92f,
                plateau);
        }

        /// <summary>Amount to subtract from h01 (0 outside the channel).</summary>
        public float Carve(float localX, float localZ, float dist, float mask)
        {
            if (mask <= 0.01f)
                return 0f;
            if (_plateau > 0f && dist < _plateau)
                return 0f;

            float t = localX * _cos + localZ * _sin;
            if (t < _start || t > _end)
                return 0f;

            float along = (t - _start) / (_end - _start);
            float offset = Mathf.Sin(along * 3f * Mathf.Pi + _phase) * _meander;
            float px = _cos * t - _sin * offset;
            float pz = _sin * t + _cos * offset;
            float d = new Vector2(localX - px, localZ - pz).Length();
            if (d >= _halfWidth)
                return 0f;

            float w = 1f - d / _halfWidth;
            return w * w * 0.22f * mask;
        }

        public Vector2 Sample(float along01)
        {
            float t = _start + along01 * (_end - _start);
            float offset = Mathf.Sin(along01 * 3f * Mathf.Pi + _phase) * _meander;
            float px = _cos * t - _sin * offset;
            float pz = _sin * t + _cos * offset;
            return new Vector2(px, pz);
        }

        public Vector2 SampleBank(float along01, float side)
        {
            var p = Sample(along01);
            float bank = _halfWidth + 2.5f;
            return p + new Vector2(-_sin, _cos) * bank * side;
        }

        /// <summary>
        ///   True when <paramref name="localX"/>/<paramref name="localZ"/> is
        ///   inside the carved channel plus <paramref name="extraReach"/> of
        ///   bank (same start/end/plateau/meander as <see cref="Carve"/>).
        ///   Mask is the caller's job — this is geometry only.
        /// </summary>
        public bool Contains(float localX, float localZ, float extraReach)
        {
            float dist = new Vector2(localX, localZ).Length();
            if (_plateau > 0f && dist < _plateau)
                return false;

            float t = localX * _cos + localZ * _sin;
            if (t < _start || t > _end)
                return false;

            float along = (t - _start) / (_end - _start);
            float offset = Mathf.Sin(along * 3f * Mathf.Pi + _phase) * _meander;
            float px = _cos * t - _sin * offset;
            float pz = _sin * t + _cos * offset;
            float d = new Vector2(localX - px, localZ - pz).Length();
            return d <= _halfWidth + extraReach;
        }
    }
}
