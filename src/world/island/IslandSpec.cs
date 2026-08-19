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
    /// <summary>
    ///   Legacy low-risk islet (radio + crabs). Product
    ///   <see cref="WorldLayout.Generate"/> no longer emits this tier — radio
    ///   lives on <see cref="Main"/>. Kept so heightmap tests can still
    ///   construct a generic spec.
    /// </summary>
    Spawn,

    /// <summary>
    ///   Product spawn at the world origin after 开始游戏. Radio, trees,
    ///   beach, building plateau; wolves/boars stay outside the 40 m
    ///   spawn-safe ring. The existing Ground and BuildingSystem stay at
    ///   the origin — the generator never moves them.
    /// </summary>
    Main,

    /// <summary>High-risk far-sea storm island — the shark-king boss arena.</summary>
    Storm,

    /// <summary>
    ///   Dedicated chapter-1 tutorial island. Not the product spawn: generated
    ///   from a separate seed stream and visited only by 新手教程.
    /// </summary>
    Tutorial,

    /// <summary>Farmland / harvest islet — rolling fields, grass, few trees.</summary>
    Harvest,

    /// <summary>Ruin / stone islet — tall rock, inscriptions (quest_ruin).</summary>
    Ruin,

    /// <summary>Mutant woods — dense canopy and quest_mutant hostiles.</summary>
    Mutant,

    /// <summary>
    ///   Optional hunting islet beyond the storm ring. Extra trees and
    ///   wolves/boars on this island only — not on the quest chain.
    /// </summary>
    Wild,

    /// <summary>
    ///   Optional atoll / extra beach beyond the storm ring. Palms, crabs,
    ///   and sand; no story points.
    /// </summary>
    Atoll,

    /// <summary>
    ///   Optional wreck / salvage islet. Crates, barrels, driftwood and a
    ///   visible ship wreck landmark. Not the ruin quest island.
    /// </summary>
    Wreck,

    /// <summary>
    ///   Easter-egg volcano far off the quest rings. Jagged ash / lava look;
    ///   not on the main chain.
    /// </summary>
    Volcano,

    /// <summary>
    ///   Easter-egg alpine / snow mountain locked to far +Z or −Z. Not on
    ///   the main chain.
    /// </summary>
    Polar
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
