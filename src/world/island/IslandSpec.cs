// Original (Iter8.5)
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Difficulty tier of a procedurally generated island (T8.5.1). The tier
///   drives the island's size/height parameters in <see cref="WorldLayout"/>
///   and the props / story points that <see cref="IslandBuilder"/> places on
///   the island.
/// </summary>
public enum IslandTier
{
    /// <summary>Low-risk birth island near the origin (trees / palm / radio trigger).</summary>
    Spawn,

    /// <summary>
    ///   Mid-risk main island at the world origin. The existing Ground and
    ///   BuildingSystem stay at the origin — the generator never moves them.
    /// </summary>
    Main,

    /// <summary>High-risk far-sea storm island — the shark-king boss arena.</summary>
    Storm
}

/// <summary>
///   Immutable description of one procedurally generated island (T8.5.1).
///   Every field is produced deterministically from the world seed by
///   <see cref="WorldLayout.Generate"/>, and the spec alone is enough for
///   <see cref="IslandHeightmap"/> / <see cref="IslandMeshBuilder"/> to
///   rebuild the island. No world coordinate is ever hardcoded — centers
///   always come from the layout.
/// </summary>
/// <param name="Seed">
///   Per-island noise seed, drawn deterministically from the world seed
///   stream. Also seeds the prop-placement RNG inside
///   <see cref="IslandBuilder"/> so props are deterministic too.
/// </param>
/// <param name="Center">Island center in world XZ, stored as (X, Z).</param>
/// <param name="Radius">Island radius in meters; terrain spans ±Radius around <paramref name="Center"/>.</param>
/// <param name="HeightScale">
///   Terrain amplitude in meters. Surface world Y maps the normalized
///   heightmap onto [-<paramref name="HeightScale"/> / 2, +<paramref name="HeightScale"/> / 2],
///   so the island interior rises above sea level (Y = 0).
/// </param>
/// <param name="NoiseScale">FastNoiseLite frequency; feature size ≈ 1 / <paramref name="NoiseScale"/> meters.</param>
/// <param name="Tier">Difficulty tier; see <see cref="IslandTier"/>.</param>
public sealed record IslandSpec(
    long Seed,
    Vector2 Center,
    float Radius,
    float HeightScale,
    float NoiseScale,
    IslandTier Tier);
