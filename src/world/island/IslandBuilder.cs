// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   T8.5.1: builds the procedural archipelago into the scene tree. On
///   <c>_Ready</c> (or an explicit <see cref="Generate"/> call) it asks
///   <see cref="WorldLayout"/> for the deterministic island specs, then for
///   each island generates the heightmap, the terrain ArrayMesh and the
///   HeightMapShape3D collision, and finally populates the tier contents:
///   <list type="bullet">
///   <item>
///     <description><see cref="IslandTier.Main"/>: product spawn. Radio
///     on the beach (walkable), trees, palms, crabs on the shore;
///     wolves/boars stay outside <see cref="SpawnSafeRadius"/>. The
///     "ruin" trigger is NOT here — it belongs on the Ruin island.</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Harvest"/>: farmland — grass
///     clearing, sparse trees, no hostiles.</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Ruin"/>: stone islet plus three
///     ordered <see cref="StoryInteractable"/> quest logs; the third raises
///     <c>StoryPointId = "ruin"</c> (no walk-in Area trigger).</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Mutant"/>: dense woods and
///     mutants for quest_mutant.</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Storm"/>: a <see cref="StoryPointTrigger"/>
///     with <c>StoryPointId = "shark_king"</c> — the island is the boss
///     arena; storm weather is handled by WeatherService / the main
///     orchestrator, not by this class.</description>
///   </item>
///   <item>
    ///     <description><see cref="IslandTier.Tutorial"/>: built only by
    ///     <see cref="EnsureTutorialIsland"/> (新手教程). Enough wood, coconut
    ///     palms, Kenney grass/shrubs/rocks along the mountain ridges, a
    ///     building plateau, one beach crab, and one interactable driftwood
    ///     chest on the inner shore. 开始游戏 never generates or
    ///     spawns here.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Wild"/>: optional hunting
    ///     islet — extra trees/rocks and wolves/boars. Not quest-gated.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Atoll"/>: extra beach — palms,
    ///     crabs, sand. Not quest-gated.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Wreck"/>: salvage landmark —
    ///     crates, barrels, driftwood, ship wreck. No ruin story point.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Volcano"/>: far-pole easter egg.
    ///     Ash splat, rocks, emissive lava pools. No quest story points.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Polar"/>: far ±Z snow mountain.
    ///     Pines, no palms, no crabs. No quest story points.</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="IslandTier.Spawn"/>: legacy grassland +
    ///     radio (not emitted by product <see cref="WorldLayout.Generate"/>).</description>
    ///   </item>
///   </list>
///
///   Every non-tutorial tier also gets one readable
///   <see cref="StoryInteractable"/> (texts in <see cref="IslandLore"/>):
///   pure narration with an empty StoryPointId so the quest chain is never
///   re-fired. The tutorial island deliberately stays clean.
///
///   Everything is deterministic: the default <see cref="WorldSeed"/> of
///   12345 plus the per-island seeds drawn from the layout reproduce the
///   same archipelago on every run. Harvestable trees and decorative
///   vegetation follow the heightmap ridge/slope (see
///   <see cref="IslandVegetation"/>); enemy / story-point RNG stays on the
///   original per-island stream so combat layout is unchanged. Props sit
///   on the heightmap surface at or above sea level — no hardcoded world
///   coordinates.
/// </summary>
public partial class IslandBuilder : Node3D
{
    /// <summary>World generation seed; the fixed default keeps the archipelago deterministic.</summary>
    [Export] public long WorldSeed = 12345;

    /// <summary>Heightmap / mesh / collision grid size per axis (193² default).</summary>
    [Export] public int Resolution = IslandHeightmap.DefaultResolution;

    /// <summary>Generate during <c>_Ready</c>; disable to call <see cref="Generate"/> manually.</summary>
    [Export] public bool GenerateOnReady = true;

    /// <summary>Lowest normalized height accepted for prop placement (keeps props above sea level).</summary>
    public const float MinLandHeight01 = 0.55f;

    /// <summary>
    ///   Horizontal meters from the Main island center (player spawn) that
    ///   generated enemies must stay outside of. Larger than AttackRange
    ///   (~1.5 m) so the player is not in combat on appearance.
    /// </summary>
    public const float SpawnSafeRadius = 40f;

    /// <summary>Random attempts to find a land grid point before falling back to the highest point.</summary>
    private const int PropPlacementAttempts = 48;

    /// <summary>Extra height above the surface for story-point triggers (the sphere meets the player capsule).</summary>
    private const float TriggerHeightOffset = 1.5f;

    /// <summary>
    ///   Real-ish greedy spacing floors (meters). Grass 1.2–2.5, shrubs 3–5,
    ///   canopy 5–8 so trunks do not intersect, beach palms 6–10, rocks sparse.
    /// </summary>
    private const float GrassSpacing = 1.6f;
    private const float GrassSpacingField = 1.4f;
    private const float GrassSpacingSparse = 2.4f;
    private const float ShrubSpacing = 3.2f;
    private const float ShrubSpacingSparse = 4.2f;
    private const float TreeSpacing = 5.5f;
    private const float TreeSpacingWoods = 4.8f;
    private const float TreeSpacingStunted = 7f;
    private const float PalmSpacing = 6.5f;
    private const float PalmSpacingAtoll = 5.8f;
    private const float RockSpacing = 5.2f;
    private const float RockSpacingRuin = 4.5f;

    /// <summary>Main-island harvestable grassland trees (plus deco canopy).</summary>
    private const int MainTreeCount = 50;

    /// <summary>Main-island decorative Kenney canopy (not harvestable).</summary>
    private const int MainDecoTreeCount = 36;

    /// <summary>Main-island beach palm count.</summary>
    private const int MainPalmCount = 27;

    /// <summary>Main-island decorative beach palms (not harvestable).</summary>
    private const int MainDecoPalmCount = 14;

    /// <summary>Main Kenney grass clumps off the 24 m plateau (cap 200–360).</summary>
    public const int MainGrassCount = 315;

    /// <summary>Main understory shrubs off the plateau.</summary>
    private const int MainShrubCount = 63;

    /// <summary>Main rocks on steep / high ground — never on the plateau.</summary>
    private const int MainRockCount = 18;

    /// <summary>
    ///   Tutorial-island grassland trees. Each <see cref="WoodTree"/> yields
    ///   3 wood; campfire (5) + bed (6) need 11, so 5 trees = 15 harvests.
    /// </summary>
    public const int TutorialTreeCount = 5;

    /// <summary>Tutorial-island coconut palms (drink / gather step).</summary>
    public const int TutorialPalmCount = 2;

    /// <summary>Tutorial decorative Kenney trees (not harvestable).</summary>
    public const int TutorialDecoTreeCount = 10;

    /// <summary>Tutorial decorative beach palms (not harvestable).</summary>
    public const int TutorialDecoPalmCount = 6;

    /// <summary>Tutorial Kenney grass clumps along grassland / ridge flanks.</summary>
    public const int TutorialGrassCount = 48;

    /// <summary>Tutorial shrubs on mid-slope below the ridge trees.</summary>
    public const int TutorialShrubCount = 14;

    /// <summary>Tutorial rocks on steep / high ground — never on the plateau.</summary>
    public const int TutorialRockCount = 4;

    /// <summary>Harvest farmland trees (few — field look).</summary>
    private const int HarvestTreeCount = 9;

    /// <summary>Harvest decorative trees at the field edge.</summary>
    private const int HarvestDecoTreeCount = 5;

    /// <summary>Harvest beach palms.</summary>
    private const int HarvestPalmCount = 5;

    /// <summary>Harvest grass — field, not a bald disc (cap ~220).</summary>
    public const int HarvestGrassCount = 216;

    /// <summary>Harvest hedgerow shrubs.</summary>
    private const int HarvestShrubCount = 27;

    /// <summary>Harvest rocks (almost none on the field).</summary>
    private const int HarvestRockCount = 5;

    /// <summary>Ruin canopy (sparse).</summary>
    private const int RuinTreeCount = 11;

    /// <summary>Ruin decorative trees in cracks.</summary>
    private const int RuinDecoTreeCount = 5;

    /// <summary>Ruin grass in stone cracks.</summary>
    private const int RuinGrassCount = 50;

    /// <summary>Ruin shrubs.</summary>
    private const int RuinShrubCount = 18;

    /// <summary>Ruin rocks — boulder-forward, not a field.</summary>
    private const int RuinRockCount = 41;

    /// <summary>Mutant woods harvestable trees.</summary>
    private const int MutantTreeCount = 59;

    /// <summary>Mutant decorative canopy (true woods, cap ~100 total).</summary>
    private const int MutantDecoTreeCount = 41;

    /// <summary>Mutant beach palms.</summary>
    private const int MutantPalmCount = 5;

    /// <summary>Mutant decorative palms.</summary>
    private const int MutantDecoPalmCount = 5;

    /// <summary>Mutant understory grass.</summary>
    private const int MutantGrassCount = 180;

    /// <summary>Mutant understory shrubs.</summary>
    private const int MutantShrubCount = 54;

    /// <summary>Mutant rocks.</summary>
    private const int MutantRockCount = 14;

    /// <summary>Storm wind-stunted trees.</summary>
    private const int StormTreeCount = 3;

    /// <summary>Storm sparse grass.</summary>
    private const int StormGrassCount = 14;

    /// <summary>Storm shrubs.</summary>
    private const int StormShrubCount = 6;

    /// <summary>Storm rocks.</summary>
    private const int StormRockCount = 14;

    /// <summary>Wild hunting-islet grassland trees.</summary>
    public const int WildTreeCount = 36;

    /// <summary>Wild decorative hunting-woods canopy.</summary>
    private const int WildDecoTreeCount = 27;

    /// <summary>Wild hunting-islet beach palms.</summary>
    public const int WildPalmCount = 7;

    /// <summary>Wild decorative beach palms.</summary>
    private const int WildDecoPalmCount = 5;

    /// <summary>Wild Kenney grass clumps.</summary>
    public const int WildGrassCount = 144;

    /// <summary>Wild shrubs.</summary>
    public const int WildShrubCount = 41;

    /// <summary>Wild rocks (includes large Kenney pieces).</summary>
    public const int WildRockCount = 27;

    /// <summary>Atoll inland trees (sparse — beach-first).</summary>
    public const int AtollTreeCount = 5;

    /// <summary>Atoll coconut palms.</summary>
    public const int AtollPalmCount = 27;

    /// <summary>Atoll decorative palms along the ring.</summary>
    private const int AtollDecoPalmCount = 14;

    /// <summary>Atoll grass clumps.</summary>
    public const int AtollGrassCount = 108;

    /// <summary>Atoll shrubs.</summary>
    public const int AtollShrubCount = 23;

    /// <summary>Atoll sand-rock props.</summary>
    public const int AtollRockCount = 11;

    /// <summary>Wreck islet trees.</summary>
    public const int WreckTreeCount = 7;

    /// <summary>Wreck decorative trees (sparse salvage grove).</summary>
    private const int WreckDecoTreeCount = 5;

    /// <summary>Wreck islet palms.</summary>
    public const int WreckPalmCount = 9;

    /// <summary>Wreck grass clumps.</summary>
    public const int WreckGrassCount = 45;

    /// <summary>Wreck shrubs.</summary>
    public const int WreckShrubCount = 14;

    /// <summary>Wreck rocks.</summary>
    public const int WreckRockCount = 18;

    /// <summary>Wreck Kenney crates.</summary>
    public const int WreckCrateCount = 14;

    /// <summary>Wreck Kenney barrels.</summary>
    public const int WreckBarrelCount = 11;

    /// <summary>Wreck driftwood / logs.</summary>
    public const int WreckDriftwoodCount = 9;

    /// <summary>Volcano scorched trees.</summary>
    public const int VolcanoTreeCount = 7;

    /// <summary>Volcano decorative scorched trees.</summary>
    private const int VolcanoDecoTreeCount = 5;

    /// <summary>Volcano has no tropical palms.</summary>
    public const int VolcanoPalmCount = 0;

    /// <summary>Volcano ash shrubs.</summary>
    public const int VolcanoShrubCount = 9;

    /// <summary>Volcano rocks / boulders.</summary>
    public const int VolcanoRockCount = 45;

    /// <summary>Emissive lava-pool landmarks on the volcano.</summary>
    public const int VolcanoLavaPoolCount = 9;

    /// <summary>Polar alpine pines.</summary>
    public const int PolarTreeCount = 32;

    /// <summary>Polar decorative pine clumps (not harvestable).</summary>
    private const int PolarDecoTreeCount = 23;

    /// <summary>Polar has no tropical palms.</summary>
    public const int PolarPalmCount = 0;

    /// <summary>Polar alpine grass among snow.</summary>
    public const int PolarGrassCount = 54;

    /// <summary>Polar alpine shrubs.</summary>
    public const int PolarShrubCount = 27;

    /// <summary>Polar ridge rocks.</summary>
    public const int PolarRockCount = 23;

    /// <summary>Meters the player stands above the tutorial plateau (Y = 0.5).</summary>
    public const float TutorialSpawnHeight = 2f;

    /// <summary>Fail-closed guard: the archipelago is generated exactly once.</summary>
    private bool _generated;

    /// <summary>Tutorial island is built at most once per builder.</summary>
    private bool _tutorialBuilt;

    /// <summary>True while 新手教程 has teleported the player off Main.</summary>
    private bool _tutorialSessionActive;

    private Vector3 _sessionRestorePosition;
    private Vector3 _sessionBuildingOrigin;
    private PlayerController? _sessionPlayer;
    private BuildingSystem? _sessionBuilding;
    private GameManager? _sessionGame;

    /// <summary>
    ///   Specs produced by the last <see cref="Generate"/> call. Empty until
    ///   generation; tutorial island is <see cref="TutorialSpec"/>, not here.
    /// </summary>
    public IReadOnlyList<IslandSpec> GeneratedSpecs { get; private set; } =
        Array.Empty<IslandSpec>();

    /// <summary>Spec of the built tutorial island, or null before Ensure.</summary>
    public IslandSpec? TutorialSpec { get; private set; }

    /// <summary>True after <see cref="EnsureTutorialIsland"/> has run.</summary>
    public bool TutorialIslandReady => _tutorialBuilt;

    public override void _Ready()
    {
        if (GenerateOnReady)
            Generate();
    }

    /// <summary>
    ///   Generates the archipelago as children of this node. Fail-closed:
    ///   a second call after the first generation is a no-op, and an invalid
    ///   <see cref="Resolution"/> throws instead of producing a broken world.
    /// </summary>
    public void Generate()
    {
        if (_generated)
            return;
        _generated = true;

        if (Resolution < 2)
            throw new InvalidOperationException($"Resolution must be >= 2 (got {Resolution}).");

        var specs = WorldLayout.Generate(WorldSeed);
        GeneratedSpecs = specs;
        for (int i = 0; i < specs.Count; i++)
            BuildIsland(specs[i], i);
    }

    /// <summary>
    ///   Builds the chapter-1 tutorial island if it is not already in the
    ///   tree. Product <see cref="Generate"/> does not call this — 开始游戏
    ///   never visits the island as the spawn. Idempotent.
    /// </summary>
    public void EnsureTutorialIsland()
    {
        if (_tutorialBuilt)
            return;

        if (Resolution < 2)
            throw new InvalidOperationException($"Resolution must be >= 2 (got {Resolution}).");

        TutorialSpec = WorldLayout.GenerateTutorialIsland(WorldSeed);
        BuildIsland(TutorialSpec, GetChildCount());
        _tutorialBuilt = true;
    }

    /// <summary>
    ///   World-space spawn on the tutorial plateau (center, Y =
    ///   <see cref="TutorialSpawnHeight"/>). Zero until the island is built.
    /// </summary>
    public Vector3 TutorialWorldSpawn()
    {
        if (TutorialSpec == null)
            return new Vector3(0f, TutorialSpawnHeight, 0f);

        return new Vector3(
            TutorialSpec.Center.X,
            TutorialSpawnHeight,
            TutorialSpec.Center.Y);
    }

    /// <summary>
    ///   新手教程 arrival: build the tutorial island, teleport the player,
    ///   park the 40×40 building grid on the tutorial plateau, and set the
    ///   death respawn slot to the tutorial spawn. Idempotent.
    /// </summary>
    public void StartTutorialSession(
        PlayerController? player = null,
        Node3D? productSpawn = null,
        BuildingSystem? building = null,
        GameManager? game = null)
    {
        if (_tutorialSessionActive)
            return;

        EnsureTutorialIsland();

        _sessionPlayer = player;
        if (_sessionPlayer == null)
        {
            var root = GetTree()?.Root;
            if (root != null)
                _sessionPlayer = FindPlayerController(root);
        }
        _sessionBuilding = building ?? FindNamed<BuildingSystem>("BuildingSystem");
        _sessionGame = game ?? FindNamed<GameManager>("GameManager");

        var spawn = TutorialWorldSpawn();
        if (_sessionPlayer != null)
        {
            _sessionRestorePosition =
                productSpawn?.GlobalPosition ?? _sessionPlayer.GlobalPosition;
            _sessionPlayer.GlobalPosition = spawn;
            _sessionGame?.SetRespawnPoint(spawn);
        }

        if (_sessionBuilding != null)
        {
            _sessionBuildingOrigin = _sessionBuilding.GlobalPosition;
            _sessionBuilding.GlobalPosition = new Vector3(
                spawn.X, _sessionBuildingOrigin.Y, spawn.Z);
        }

        _tutorialSessionActive = true;
    }

    /// <summary>
    ///   Chapter-1 complete handoff: teleport back to the product spawn
    ///   (Main / PlayerSpawnPoint) and restore the building grid. No-op when
    ///   no tutorial session is active (开始游戏 / tests).
    /// </summary>
    public void EndTutorialSession()
    {
        if (!_tutorialSessionActive)
            return;

        if (_sessionPlayer != null && GodotObject.IsInstanceValid(_sessionPlayer))
        {
            _sessionPlayer.GlobalPosition = _sessionRestorePosition;
            _sessionGame?.SetRespawnPoint(_sessionRestorePosition);
        }

        if (_sessionBuilding != null && GodotObject.IsInstanceValid(_sessionBuilding))
            _sessionBuilding.GlobalPosition = _sessionBuildingOrigin;

        _tutorialSessionActive = false;
        _sessionPlayer = null;
        _sessionBuilding = null;
        _sessionGame = null;
    }

    private T? FindNamed<T>(string name) where T : Node
    {
        var tree = GetTree();
        if (tree == null)
            return null;

        return tree.Root.FindChild(name, recursive: true, owned: false) as T;
    }

    private void BuildIsland(IslandSpec spec, int index)
    {
        float[] heightmap = IslandHeightmap.Generate(spec, Resolution);
        var (mesh, minHeight, maxHeight) = IslandMeshBuilder.Build(spec, heightmap, Resolution);
        float gridScale = spec.Radius * 2f / (Resolution - 1);

        var body = new StaticBody3D
        {
            Name = $"Island_{spec.Tier}_{index}",
            Position = new Vector3(spec.Center.X, 0f, spec.Center.Y)
        };

        // HeightMapShape3D collision. MapData holds local-space heights (grid
        // points 1 unit apart); the CollisionShape3D is scaled UNIFORMLY by
        // the mesh cell size so physics matches the visual exactly (see
        // IslandMeshBuilder class docs for the Godot 4.7 semantics).
        body.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = IslandMeshBuilder.BuildCollision(heightmap, minHeight, maxHeight, Resolution, gridScale),
            Scale = new Vector3(gridScale, gridScale, gridScale)
        });

        body.AddChild(new MeshInstance3D
        {
            Name = "Mesh",
            Mesh = mesh,
            MaterialOverride = CreateTierMaterial(spec.Tier)
        });

        AddChild(body);

        PopulateIsland(spec, body, heightmap);
    }

    private static StandardMaterial3D CreateTierMaterial(IslandTier tier)
    {
        var mat = new StandardMaterial3D
        {
            // Terrain must be visible from any angle (inside slopes, below
            // the waterline), so back-face culling is disabled.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            // Vertex colors carry the sand→land biome gradient; a tiled
            // Poly Haven sand albedo grains the beach splat (and tints inland
            // grass) without a second material.
            VertexColorUseAsAlbedo = true,
            AlbedoColor = TierAlbedoTint(tier)
        };

        string albedoPath = tier switch
        {
            IslandTier.Atoll => VegetationModels.CoastSandAlbedo,
            IslandTier.Wreck => VegetationModels.RockAlbedo,
            IslandTier.Volcano => VegetationModels.VolcanoAlbedo,
            IslandTier.Polar => VegetationModels.SnowAlbedo,
            _ => VegetationModels.SandAlbedo
        };
        var sand = GD.Load<Texture2D>(albedoPath);
        if (sand == null && albedoPath != VegetationModels.SandAlbedo)
            sand = GD.Load<Texture2D>(VegetationModels.SandAlbedo);
        if (sand != null)
        {
            mat.AlbedoTexture = sand;
            // Local-meter UVs from IslandMeshBuilder: ~8 m tiles.
            mat.Uv1Scale = new Vector3(0.125f, 0.125f, 1f);
            mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
        }

        return mat;
    }

    private static Color TierAlbedoTint(IslandTier tier) => tier switch
    {
        IslandTier.Harvest => new Color(1.08f, 1.04f, 0.82f),
        IslandTier.Ruin => new Color(0.88f, 0.86f, 0.84f),
        IslandTier.Mutant => new Color(0.82f, 0.90f, 0.78f),
        IslandTier.Storm => new Color(0.78f, 0.82f, 0.88f),
        IslandTier.Main => new Color(1f, 1f, 0.96f),
        IslandTier.Wild => new Color(0.88f, 0.96f, 0.84f),
        IslandTier.Atoll => new Color(1.10f, 1.04f, 0.90f),
        IslandTier.Wreck => new Color(0.90f, 0.88f, 0.86f),
        IslandTier.Volcano => new Color(0.70f, 0.58f, 0.52f),
        IslandTier.Polar => new Color(1.18f, 1.20f, 1.24f),
        _ => Colors.White
    };

    private void PopulateIsland(IslandSpec spec, StaticBody3D body, float[] heightmap)
    {
        // Enemy / story-point stream — unchanged mix so combat layout stays
        // on the original sequence. Vegetation uses a dedicated mix plus
        // greedy ridge scoring (no extra draws from this stream).
        var rng = new Random(unchecked((int)spec.Seed ^ 0x51ED2701));
        var vegRng = new Random(unchecked((int)spec.Seed ^ 0x7E6E7A70));
        var samples = IslandVegetation.Analyze(spec, heightmap, Resolution);

        switch (spec.Tier)
        {
            case IslandTier.Spawn:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    treeCount: 6 + vegRng.Next(4),
                    palmCount: 2 + vegRng.Next(2),
                    grassCount: 40,
                    shrubCount: 10,
                    rockCount: 2,
                    decoTreeCount: 8,
                    decoPalmCount: 2);
                PlaceStoryPoint(spec, body, heightmap, rng, "radio", reusable: false);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Main:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    MainTreeCount, MainPalmCount,
                    MainGrassCount, MainShrubCount, MainRockCount,
                    MainDecoTreeCount, MainDecoPalmCount);
                PlaceStoryPoint(
                    spec, body, heightmap, rng, "radio", reusable: false,
                    minRadius: spec.Radius * 0.62f,
                    maxRadius: spec.Radius * IslandHeightmap.MaskFalloffStart,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.64f);
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Main, rng),
                    minRadius: spec.Radius * 0.62f,
                    maxRadius: spec.Radius * IslandHeightmap.MaskFalloffStart,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.64f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Harvest:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    HarvestTreeCount, HarvestPalmCount,
                    HarvestGrassCount, HarvestShrubCount, HarvestRockCount,
                    HarvestDecoTreeCount);
                // 田岛：放在田地里带，玩家穿行会经过。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Harvest, rng),
                    minRadius: spec.Radius * 0.15f,
                    maxRadius: spec.Radius * 0.55f,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.78f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Ruin:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    RuinTreeCount, palmCount: 0,
                    RuinGrassCount, RuinShrubCount, RuinRockCount,
                    RuinDecoTreeCount);
                // Chapter 2: three ordered logs. Only the last raises
                // StoryPointReached("ruin") — no walk-in Area soft-complete.
                PlaceRuinQuestLogs(spec, body, heightmap, rng);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Mutant:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    MutantTreeCount, MutantPalmCount,
                    MutantGrassCount, MutantShrubCount, MutantRockCount,
                    MutantDecoTreeCount, MutantDecoPalmCount);
                // 变异林：跟树冠带同高程，玩家在林中穿行能捡到。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Mutant, rng),
                    minRadius: spec.Radius * 0.18f,
                    maxRadius: spec.Radius * 0.60f,
                    minHeight01: 0.56f,
                    maxHeight01: 0.82f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Storm:
                // The storm island IS the shark-king boss arena. Weather is
                // handled by WeatherService / the main orchestrator.
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    StormTreeCount, palmCount: 0,
                    StormGrassCount, StormShrubCount, StormRockCount);
                PlaceStoryPoint(spec, body, heightmap, rng, "shark_king", reusable: true);
                PlaceEnemies(spec, body, heightmap, rng);
                // 放在敌人之后：不搅动已调好的 Boss/杂兵 rng 落点流。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Storm, rng),
                    minRadius: spec.Radius * 0.12f,
                    maxRadius: spec.Radius * 0.55f,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.85f);
                break;

            case IslandTier.Tutorial:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    TutorialTreeCount, TutorialPalmCount,
                    TutorialGrassCount, TutorialShrubCount, TutorialRockCount,
                    TutorialDecoTreeCount, TutorialDecoPalmCount);
                PlaceTutorialChest(spec, body, samples);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Wild:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    WildTreeCount, WildPalmCount,
                    WildGrassCount, WildShrubCount, WildRockCount,
                    WildDecoTreeCount, WildDecoPalmCount);
                // 猎岛：林子到山脊之间，打猎路线能碰到。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Wild, rng),
                    minRadius: spec.Radius * 0.20f,
                    maxRadius: spec.Radius * 0.65f,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.82f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Atoll:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    AtollTreeCount, AtollPalmCount,
                    AtollGrassCount, AtollShrubCount, AtollRockCount,
                    decoPalmCount: AtollDecoPalmCount);
                // 环礁：跟椰树/沙滩同一条滩带，登岛沿岸走就能读到。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Atoll, rng),
                    minRadius: spec.Radius * 0.38f,
                    maxRadius: spec.Radius * IslandHeightmap.MaskFalloffStart,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.64f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Wreck:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    WreckTreeCount, WreckPalmCount,
                    WreckGrassCount, WreckShrubCount, WreckRockCount,
                    WreckDecoTreeCount);
                PlaceSalvageProps(spec, body, samples);
                // 沉船岛：内陆中带，靠近打捞物一侧。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Wreck, rng),
                    minRadius: spec.Radius * 0.12f,
                    maxRadius: spec.Radius * 0.55f,
                    minHeight01: MinLandHeight01,
                    maxHeight01: 0.80f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Volcano:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    VolcanoTreeCount, VolcanoPalmCount,
                    grassCount: 0, VolcanoShrubCount, VolcanoRockCount,
                    VolcanoDecoTreeCount);
                PlaceLavaPools(spec, body, samples);
                // 彩蛋火山：故意放偏高海拔，要爬上去才读得到。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Volcano, rng),
                    minRadius: spec.Radius * 0.15f,
                    maxRadius: spec.Radius * 0.62f,
                    minHeight01: 0.70f,
                    maxHeight01: 0.95f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Polar:
                PlaceRidgeVegetation(
                    spec, body, heightmap, samples, vegRng,
                    PolarTreeCount, PolarPalmCount,
                    PolarGrassCount, PolarShrubCount, PolarRockCount,
                    PolarDecoTreeCount);
                // 彩蛋雪山：同样放偏高海拔的雪线一带。
                PlaceStoryInteractable(
                    spec, body, heightmap, rng,
                    IslandLore.Pick(IslandTier.Polar, rng),
                    minRadius: spec.Radius * 0.15f,
                    maxRadius: spec.Radius * 0.62f,
                    minHeight01: 0.70f,
                    maxHeight01: 0.95f);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            default:
                throw new InvalidOperationException($"Unhandled island tier '{spec.Tier}'.");
        }
    }

    /// <summary>
    ///   Places harvestable trees along inland ridges, coconut palms on the
    ///   beach ring, decorative Kenney canopy where quotas allow extra trunks,
    ///   and grass/shrubs/rocks on matching height/slope bands. Spacing follows
    ///   real-ish plant type (dense grass, patchy shrubs, 5–8 m canopy). Rocks
    ///   stay off the building plateau; trees stay inland of the crab beach so
    ///   the tutorial shore is not a dense forest. Extra deco trees are
    ///   <see cref="VegetationProp"/> so chapter-1 harvest counts stay intact.
    /// </summary>
    private void PlaceRidgeVegetation(
        IslandSpec spec,
        StaticBody3D body,
        float[] heightmap,
        VegetationSample[] samples,
        Random vegRng,
        int treeCount,
        int palmCount,
        int grassCount,
        int shrubCount,
        int rockCount,
        int decoTreeCount = 0,
        int decoPalmCount = 0)
    {
        bool plateau = IslandHeightmap.HasBuildingPlateau(spec.Tier);
        float treeMin = plateau
            ? (spec.Tier == IslandTier.Main ? IslandHeightmap.BuildingPlateauRadius : 16f)
            : 4f;
        if (spec.Tier == IslandTier.Harvest)
            treeMin = spec.Radius * 0.38f;
        float treeMax = spec.Radius * Mathf.Min(IslandHeightmap.MaskFalloffStart * 0.92f, 0.60f);
        if (spec.Tier == IslandTier.Wild)
            treeMax = spec.Radius * 0.68f;
        if (spec.Tier is IslandTier.Polar or IslandTier.Volcano)
            treeMax = spec.Radius * 0.62f;
        float treeHMax = spec.Tier is IslandTier.Polar or IslandTier.Volcano ? 0.95f : 0.82f;
        float treeSpacing = spec.Tier switch
        {
            IslandTier.Mutant or IslandTier.Wild => TreeSpacingWoods,
            IslandTier.Storm or IslandTier.Volcano => TreeSpacingStunted,
            _ => TreeSpacing
        };
        var trees = IslandVegetation.Pick(
            samples,
            s => s.RadialMeters >= treeMin && s.RadialMeters <= treeMax
                && s.Height01 >= 0.56f && s.Height01 <= treeHMax
                && s.Slope < 0.95f,
            s => s.RidgeScore,
            treeCount + decoTreeCount,
            treeSpacing);
        FillHarvestable(
            spec, heightmap, vegRng, trees, treeCount,
            treeMin, treeMax, 0.56f, treeHMax,
            (i, sample, fallback) =>
            {
                ResolveCanopyVisual(
                    spec, i, sample?.Height01 ?? 0.6f, out string model, out float scale);
                var tree = new WoodTree
                {
                    Name = $"WoodTree_{i}",
                    Position = sample?.Local ?? fallback,
                    Rotation = new Vector3(0f, sample?.Yaw ?? 0f, 0f),
                    VisualModelPath = model,
                    VisualScale = scale
                };
                body.AddChild(tree);
            });
        PlaceDecoCanopy(body, spec, trees, treeCount);

        float beachLo = spec.Radius * 0.62f;
        float beachHi = spec.Radius * IslandHeightmap.MaskFalloffStart;
        if (spec.Tier == IslandTier.Atoll)
            beachLo = spec.Radius * 0.38f;
        float palmSpacing = spec.Tier is IslandTier.Atoll or IslandTier.Tutorial
            ? PalmSpacingAtoll
            : PalmSpacing;
        var palms = IslandVegetation.Pick(
            samples,
            s => s.RadialMeters >= beachLo && s.RadialMeters <= beachHi
                && s.Height01 >= MinLandHeight01 && s.Height01 <= 0.64f
                && s.Slope < 0.7f,
            s => (1f - Mathf.Clamp(s.Slope, 0f, 1f)) * 2f + (0.64f - s.Height01),
            palmCount + decoPalmCount,
            palmSpacing);
        FillHarvestable(
            spec, heightmap, vegRng, palms, palmCount,
            beachLo, beachHi, MinLandHeight01, 0.64f,
            (i, sample, fallback) =>
            {
                bool bent = spec.Tier == IslandTier.Atoll && i % 2 == 0;
                body.AddChild(new CoconutPalm
                {
                    Name = $"CoconutPalm_{i}",
                    Position = sample?.Local ?? fallback,
                    Rotation = new Vector3(0f, sample?.Yaw ?? 0f, 0f),
                    VisualModelPath = bent ? VegetationModels.PalmBend : VegetationModels.Palm
                });
            });
        PlaceDecoPalms(body, spec, palms, palmCount);

        float grassMin = plateau
            ? (spec.Tier == IslandTier.Main ? IslandHeightmap.BuildingPlateauRadius : 10f)
            : 3f;
        if (spec.Tier == IslandTier.Harvest)
            grassMin = 2f;
        float grassHi = spec.Radius * IslandHeightmap.MaskFalloffStart * 0.98f;
        float grassSpacing = spec.Tier switch
        {
            IslandTier.Harvest => GrassSpacingField,
            IslandTier.Storm or IslandTier.Volcano or IslandTier.Wreck => GrassSpacingSparse,
            IslandTier.Ruin or IslandTier.Polar => 2.0f,
            _ => GrassSpacing
        };
        PlaceDecor(
            body, samples, "Grass", grassCount, grassSpacing,
            s => s.RadialMeters >= grassMin
                && s.RadialMeters <= grassHi
                && s.Height01 >= MinLandHeight01 && s.Height01 <= 0.82f
                && s.Slope < 0.95f,
            s => (1f - Mathf.Abs(s.Slope - 0.22f)) + s.RidgeScore * 0.25f,
            i => i % 3 == 0 ? VegetationModels.GrassLarge : VegetationModels.Grass,
            VegetationModels.GrassScale);

        // Tutorial islands are only ~36–42 m across; keep shrubs on the
        // outer plateau rim / lower ridge, not the camp center. Main skips
        // the 24 m build pad entirely.
        float shrubMin = plateau
            ? (spec.Tier == IslandTier.Main ? IslandHeightmap.BuildingPlateauRadius : 16f)
            : 6f;
        float shrubSpacing = spec.Tier is IslandTier.Storm or IslandTier.Volcano
            ? ShrubSpacingSparse
            : ShrubSpacing;
        PlaceDecor(
            body, samples, "Shrub", shrubCount, shrubSpacing,
            s => s.RadialMeters >= shrubMin
                && s.RadialMeters <= spec.Radius * 0.72f
                && s.Height01 >= 0.56f && s.Height01 <= 0.80f
                && s.Slope < 0.9f,
            s => s.RidgeScore * 0.6f + (1f - Mathf.Clamp(s.Slope, 0f, 1f)),
            i => i % 2 == 0 ? VegetationModels.Shrub : VegetationModels.ShrubSmall,
            VegetationModels.ShrubScale);

        // Rocks never sit in the building-grid disk; the band must still
        // exist on a 36 m tutorial island (plateau 24 m, warped falloff).
        float rockMin = plateau ? IslandHeightmap.BuildingPlateauRadius + 2f : spec.Radius * 0.28f;
        float rockSpacing = spec.Tier is IslandTier.Ruin or IslandTier.Volcano
            ? RockSpacingRuin
            : RockSpacing;
        PlaceDecor(
            body, samples, "Rock", rockCount, rockSpacing,
            s => s.RadialMeters >= rockMin
                && s.RadialMeters <= spec.Radius * IslandHeightmap.MaskFalloffStart
                && s.Height01 >= 0.64f,
            s => s.Height01 * 0.5f + s.Slope + s.RidgeScore * 0.3f,
            i => RockModelPath(spec.Tier, i),
            spec.Tier == IslandTier.Atoll ? VegetationModels.RockSandScale : VegetationModels.RockScale);
    }

    private static void PlaceDecoCanopy(
        StaticBody3D body, IslandSpec spec, List<VegetationSample> scored, int harvestCount)
    {
        for (int i = harvestCount; i < scored.Count; i++)
        {
            var sample = scored[i];
            ResolveCanopyVisual(spec, i, sample.Height01, out string model, out float scale);
            body.AddChild(new VegetationProp
            {
                Name = $"DecoTree_{i - harvestCount}",
                Position = sample.Local,
                Rotation = new Vector3(0f, sample.Yaw, 0f),
                ModelPath = model,
                ModelScale = scale
            });
        }
    }

    private static void PlaceDecoPalms(
        StaticBody3D body, IslandSpec spec, List<VegetationSample> scored, int harvestCount)
    {
        for (int i = harvestCount; i < scored.Count; i++)
        {
            bool bent = spec.Tier == IslandTier.Atoll && i % 2 == 0;
            body.AddChild(new VegetationProp
            {
                Name = $"DecoPalm_{i - harvestCount}",
                Position = scored[i].Local,
                Rotation = new Vector3(0f, scored[i].Yaw, 0f),
                ModelPath = bent ? VegetationModels.PalmBend : VegetationModels.Palm,
                ModelScale = VegetationModels.PalmScale
            });
        }
    }

    private static void ResolveCanopyVisual(
        IslandSpec spec, int index, float height01, out string model, out float scale)
    {
        bool pine = height01 > 0.68f;
        model = pine ? VegetationModels.Pine : VegetationModels.Oak;
        scale = pine ? VegetationModels.PineScale : VegetationModels.OakScale;
        if (spec.Tier == IslandTier.Polar)
        {
            model = index % 2 == 0 ? VegetationModels.Pine : VegetationModels.PineDefault;
            scale = VegetationModels.PineScale;
        }
        else if (spec.Tier == IslandTier.Volcano)
        {
            model = VegetationModels.OakDark;
            scale = VegetationModels.OakScale;
        }
        else if (spec.Tier == IslandTier.Wild)
        {
            if (pine)
            {
                model = index % 2 == 0 ? VegetationModels.PineDefault : VegetationModels.Pine;
                scale = VegetationModels.PineScale;
            }
            else
            {
                model = index % 2 == 0 ? VegetationModels.OakDark : VegetationModels.Oak;
            }
        }
        else if (spec.Tier == IslandTier.Mutant && pine)
        {
            model = VegetationModels.OakDark;
            scale = VegetationModels.OakScale;
        }
    }

    private static string RockModelPath(IslandTier tier, int index)
    {
        if (tier == IslandTier.Atoll)
            return VegetationModels.RockSand;
        if (tier == IslandTier.Volcano)
            return index % 2 == 0 ? VegetationModels.RockLarge : VegetationModels.RockTall;
        if (tier is IslandTier.Wild or IslandTier.Polar && index % 3 == 0)
            return VegetationModels.RockLarge;
        return index % 2 == 0 ? VegetationModels.RockTall : VegetationModels.RockSmall;
    }

    /// <summary>
    ///   Ridge-filtered lava-pool samples. When the crater band is empty,
    ///   fall back to the first sample so placement never indexes an empty
    ///   <see cref="IslandVegetation.Pick"/> list.
    /// </summary>
    public static List<VegetationSample> SelectLavaPoolSamples(
        IslandSpec spec, VegetationSample[] samples)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(samples);
        var picked = IslandVegetation.Pick(
            samples,
            s => s.Height01 >= 0.60f && s.Slope < 1.1f
                && s.RadialMeters <= spec.Radius * 0.42f,
            s => s.Height01 + s.RidgeScore,
            VolcanoLavaPoolCount,
            minSpacing: 6f);
        if (picked.Count == 0 && samples.Length > 0)
            return new List<VegetationSample> { samples[0] };
        return picked;
    }

    private static void PlaceLavaPools(
        IslandSpec spec, StaticBody3D body, VegetationSample[] samples)
    {
        var lavaMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.28f, 0.06f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.38f, 0.05f),
            EmissionEnergyMultiplier = 2.6f,
            Roughness = 0.35f
        };
        var pools = SelectLavaPoolSamples(spec, samples);
        for (int i = 0; i < pools.Count; i++)
        {
            var sample = pools[i];
            body.AddChild(new MeshInstance3D
            {
                Name = $"LavaPool_{i}",
                Position = sample.Local + new Vector3(0f, 0.06f, 0f),
                Mesh = new CylinderMesh
                {
                    TopRadius = 2.1f,
                    BottomRadius = 2.4f,
                    Height = 0.16f,
                    Material = lavaMat
                }
            });
        }
    }

    private static void PlaceSalvageProps(
        IslandSpec spec, StaticBody3D body, VegetationSample[] samples)
    {
        PlaceDecor(
            body, samples, "Crate", WreckCrateCount, 3.6f,
            s => s.Height01 >= MinLandHeight01 && s.Slope < 0.85f
                && s.RadialMeters <= spec.Radius * 0.70f,
            s => (1f - Mathf.Clamp(s.Slope, 0f, 1f)) + s.RidgeScore * 0.2f,
            _ => VegetationModels.Crate,
            VegetationModels.SalvageScale);

        PlaceDecor(
            body, samples, "Barrel", WreckBarrelCount, 3.8f,
            s => s.Height01 >= MinLandHeight01 && s.Slope < 0.9f
                && s.RadialMeters >= spec.Radius * 0.18f
                && s.RadialMeters <= spec.Radius * 0.72f,
            s => s.RidgeScore * 0.4f + (1f - Mathf.Clamp(s.Slope, 0f, 1f)),
            _ => VegetationModels.Barrel,
            VegetationModels.SalvageScale);

        PlaceDecor(
            body, samples, "Driftwood", WreckDriftwoodCount, 5f,
            s => s.Height01 >= 0.50f && s.Height01 <= 0.66f
                && s.RadialMeters >= spec.Radius * 0.50f,
            s => (0.66f - s.Height01) + (1f - Mathf.Clamp(s.Slope, 0f, 1f)),
            i => i % 2 == 0 ? VegetationModels.DriftwoodLarge : VegetationModels.Driftwood,
            VegetationModels.SalvageScale);

        PlaceDecor(
            body, samples, "Stump", 2, 6f,
            s => s.Height01 >= 0.56f && s.Height01 <= 0.78f
                && s.RadialMeters <= spec.Radius * 0.55f,
            s => s.RidgeScore,
            _ => VegetationModels.Stump,
            VegetationModels.SalvageScale);

        var wrecks = IslandVegetation.Pick(
            samples,
            s => s.Height01 >= 0.56f && s.Slope < 0.9f
                && s.RadialMeters >= spec.Radius * 0.12f
                && s.RadialMeters <= spec.Radius * 0.52f,
            s => s.RidgeScore + s.Height01,
            count: 1,
            minSpacing: 8f);
        VegetationSample? wreckSample = wrecks.Count > 0 ? wrecks[0] : null;
        if (wreckSample == null && samples.Length > 0)
        {
            var best = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i].Height01 > best.Height01)
                    best = samples[i];
            }

            wreckSample = best;
        }

        if (wreckSample is { } wreck)
        {
            body.AddChild(new VegetationProp
            {
                Name = "Landmark_ShipWreck",
                Position = wreck.Local,
                Rotation = new Vector3(0f, wreck.Yaw, 0f),
                ModelPath = VegetationModels.ShipWreck,
                ModelScale = VegetationModels.ShipWreckScale
            });
        }

        var chests = IslandVegetation.Pick(
            samples,
            s => s.Height01 >= MinLandHeight01 && s.Slope < 0.7f
                && s.RadialMeters <= spec.Radius * 0.45f,
            s => (1f - Mathf.Clamp(s.Slope, 0f, 1f)) * 2f,
            count: 1,
            minSpacing: 6f);
        if (chests.Count > 0)
        {
            var sample = chests[0];
            body.AddChild(new VegetationProp
            {
                Name = "Landmark_Chest",
                Position = sample.Local,
                Rotation = new Vector3(0f, sample.Yaw, 0f),
                ModelPath = VegetationModels.Chest,
                ModelScale = VegetationModels.SalvageScale
            });
        }
    }

    /// <summary>
    ///   One washed-up driftwood storage chest on the tutorial inner shore
    ///   (just outside the building plateau). Deterministic: greedy pick,
    ///   no extra RNG. Wreck-island Kenney <see cref="VegetationModels.Chest"/>
    ///   landmarks stay on <see cref="PlaceSalvageProps"/>.
    /// </summary>
    private static void PlaceTutorialChest(
        IslandSpec spec, StaticBody3D body, VegetationSample[] samples)
    {
        VegetationSample? sample = PickTutorialChestSample(spec, samples);
        if (sample is not { } place)
            return;

        var chest = InstantiateTutorialChest();
        chest.Name = "Landmark_WoodenChest";
        chest.Position = place.Local;
        chest.Rotation = new Vector3(0f, place.Yaw, 0f);
        body.AddChild(chest);
        SeedTutorialChestStash(chest);
    }

    private static VegetationSample? PickTutorialChestSample(
        IslandSpec spec, VegetationSample[] samples)
    {
        float minRadial = IslandHeightmap.BuildingPlateauRadius + 1.5f;
        float maxRadial = spec.Radius * IslandHeightmap.MaskFalloffStart;
        var shore = IslandVegetation.Pick(
            samples,
            s => s.RadialMeters >= minRadial && s.RadialMeters <= maxRadial
                && s.Height01 >= MinLandHeight01 && s.Height01 <= 0.64f
                && s.Slope < 0.7f,
            s => (1f - Mathf.Clamp(s.Slope, 0f, 1f)) * 3f
                + (1f - s.RadialMeters / spec.Radius),
            count: 1,
            minSpacing: 4f);
        if (shore.Count > 0)
            return shore[0];

        var fallback = IslandVegetation.Pick(
            samples,
            s => s.RadialMeters >= minRadial
                && s.Height01 >= MinLandHeight01
                && s.Slope < 0.9f,
            s => (1f - Mathf.Clamp(s.Slope, 0f, 1f))
                + (1f - s.RadialMeters / spec.Radius),
            count: 1,
            minSpacing: 4f);
        return fallback.Count > 0 ? fallback[0] : null;
    }

    private static StorageBox InstantiateTutorialChest()
    {
        if (ResourceLoader.Exists(WoodenChest.ScenePath))
        {
            var packed = GD.Load<PackedScene>(WoodenChest.ScenePath);
            if (packed?.Instantiate() is StorageBox fromScene)
                return fromScene;
        }

        return new WoodenChest();
    }

    /// <summary>
    ///   Small washed-up stash for chapter-1 (wood / coconut / berries).
    ///   Missing item resources skip that slot; empty is still valid storage.
    /// </summary>
    private static void SeedTutorialChestStash(StorageBox chest)
    {
        TryAddChestItem(chest, "wood", 3);
        TryAddChestItem(chest, "coconut", 1);
        TryAddChestItem(chest, "berries", 2);
    }

    private static void TryAddChestItem(StorageBox chest, string itemId, int amount)
    {
        var item = GD.Load<ItemData>($"res://assets/items/{itemId}.tres");
        if (item == null)
            return;

        chest.Inventory.AddItem(item, amount);
    }

    private void FillHarvestable(
        IslandSpec spec,
        float[] heightmap,
        Random vegRng,
        List<VegetationSample> scored,
        int count,
        float minRadius,
        float maxRadius,
        float minHeight01,
        float maxHeight01,
        Action<int, VegetationSample?, Vector3> spawn)
    {
        for (int i = 0; i < count; i++)
        {
            if (i < scored.Count)
            {
                spawn(i, scored[i], scored[i].Local);
                continue;
            }

            var point = PickLandGridPoint(
                spec, heightmap, vegRng,
                minRadius, maxRadius, minHeight01, maxHeight01);
            spawn(i, null, GridPointToLocal(spec, point, heightmap));
        }
    }

    private static void PlaceDecor(
        StaticBody3D body,
        VegetationSample[] samples,
        string prefix,
        int count,
        float spacing,
        Func<VegetationSample, bool> predicate,
        Func<VegetationSample, float> score,
        Func<int, string> modelPath,
        float modelScale)
    {
        var picked = IslandVegetation.Pick(samples, predicate, score, count, spacing);
        for (int i = 0; i < picked.Count; i++)
        {
            var sample = picked[i];
            body.AddChild(new VegetationProp
            {
                Name = $"{prefix}_{i}",
                Position = sample.Local,
                Rotation = new Vector3(0f, sample.Yaw, 0f),
                ModelPath = modelPath(i),
                ModelScale = modelScale
            });
        }
    }

    private void PlaceStoryPoint(
        IslandSpec spec,
        StaticBody3D body,
        float[] heightmap,
        Random rng,
        string storyPointId,
        bool reusable,
        float minRadius = 0f,
        float maxRadius = float.MaxValue,
        float minHeight01 = MinLandHeight01,
        float maxHeight01 = 1f
    )
    {
        var point = PickLandGridPoint(
            spec, heightmap, rng, minRadius, maxRadius, minHeight01, maxHeight01);
        var local = GridPointToLocal(spec, point, heightmap);
        body.AddChild(new StoryPointTrigger
        {
            Name = $"StoryPointTrigger_{storyPointId}",
            StoryPointId = storyPointId,
            // FIX(code-review): quest-gated story points must not be consumed
            // by an early visit (chapter gating is quest-side); re-fire on
            // every entry so the quest/tutorial step can still trigger later.
            // "radio" stays one-shot: quest_radio is the current quest from
            // the start, so the first visit is always the intended one.
            Reusable = reusable,
            Position = new Vector3(local.X, local.Y + TriggerHeightOffset, local.Z)
        });
    }

    /// <summary>
    ///   Places the three ordered Ruin quest logs on distinct radial bands.
    ///   Logs 0–1 are narration-only; log 2 raises <c>StoryPointId = "ruin"</c>.
    /// </summary>
    private void PlaceRuinQuestLogs(
        IslandSpec spec, StaticBody3D body, float[] heightmap, Random rng)
    {
        // Three non-overlapping radial bands so tablets stay walkably apart.
        float[] minR = { 0.12f, 0.28f, 0.40f };
        float[] maxR = { 0.26f, 0.38f, 0.48f };
        var logs = IslandLore.RuinQuestLogs;
        for (var i = 0; i < logs.Length; i++)
        {
            PlaceStoryInteractable(
                spec, body, heightmap, rng,
                logs[i],
                storyPointId: i == logs.Length - 1 ? "ruin" : "",
                nameSuffix: $"_{i}",
                minRadius: spec.Radius * minR[i],
                maxRadius: spec.Radius * maxR[i],
                minHeight01: 0.68f,
                maxHeight01: 1f);
        }
    }

    /// <summary>
    ///   Places a readable StoryInteractable (book / tablet / log) on the
    ///   island. Empty <paramref name="storyPointId"/> is pure narration;
    ///   a non-empty id raises <see cref="GameEvents.StoryPointReached"/> on
    ///   first read (quest logs). Position uses the deterministic grid picker.
    /// </summary>
    private void PlaceStoryInteractable(
        IslandSpec spec,
        StaticBody3D body,
        float[] heightmap,
        Random rng,
        string text,
        string storyPointId = "",
        string nameSuffix = "",
        float minRadius = 0f,
        float maxRadius = float.MaxValue,
        float minHeight01 = MinLandHeight01,
        float maxHeight01 = 1f)
    {
        var point = PickLandGridPoint(
            spec, heightmap, rng, minRadius, maxRadius, minHeight01, maxHeight01);
        var local = GridPointToLocal(spec, point, heightmap);
        body.AddChild(new StoryInteractable
        {
            Name = $"StoryInteractable_{spec.Tier}{nameSuffix}",
            StoryPointId = storyPointId,
            Text = text,
            // Scattered lore logs are re-readable: the player can press E
            // again after the subtitle fades to re-read the worldbuilding.
            // StoryPointReached still fires only on the first read.
            Reusable = true,
            Position = new Vector3(local.X, local.Y + TriggerHeightOffset, local.Z)
        });
    }

    /// <summary>T9.x enemy roster per island tier (id, model, count).</summary>
    private static readonly Dictionary<IslandTier, (string EnemyId, string ModelPath, int Count)[]> TierEnemies =
        new()
        {
            [IslandTier.Spawn] = new[] { ("crab", "res://assets/models/enemies/crab/Crab.glb", 2) },
            [IslandTier.Main] = new[]
            {
                ("boar", "res://assets/models/enemies/boar/Pig.glb", 5),
                ("wolf", "res://assets/models/enemies/wolf/Wolf.gltf", 5),
                ("crab", "res://assets/models/enemies/crab/Crab.glb", 5)
            },
            [IslandTier.Harvest] = Array.Empty<(string, string, int)>(),
            [IslandTier.Ruin] = Array.Empty<(string, string, int)>(),
            [IslandTier.Mutant] = new[]
            {
                ("mutant", "res://assets/models/enemies/mutant/Alien.glb", 5)
            },
            [IslandTier.Storm] = new[]
            {
                ("storm_beast", "res://assets/models/enemies/storm_beast/Squidle.glb", 3),
                ("shark_king", "res://assets/models/enemies/shark_king/Shark.glb", 1)
            },
            [IslandTier.Tutorial] = new[] { ("crab", "res://assets/models/enemies/crab/Crab.glb", 1) },
            [IslandTier.Wild] = new[]
            {
                ("boar", "res://assets/models/enemies/boar/Pig.glb", 5),
                ("wolf", "res://assets/models/enemies/wolf/Wolf.gltf", 5)
            },
            [IslandTier.Atoll] = new[] { ("crab", "res://assets/models/enemies/crab/Crab.glb", 7) },
            [IslandTier.Wreck] = new[] { ("crab", "res://assets/models/enemies/crab/Crab.glb", 2) },
            [IslandTier.Volcano] = new[] { ("bat", "res://assets/models/enemies/bat/Bat.fbx", 5) },
            [IslandTier.Polar] = new[] { ("wolf", "res://assets/models/enemies/wolf/Wolf.gltf", 5) }
        };

    /// <summary>
    ///   Places the tier's enemy roster on the island with their models wired
    ///   (the same enemy.tscn + ModelPath mechanism Game.tscn uses). Fail-closed:
    ///   without a PlayerController in the scene root there is nothing for the
    ///   AI to chase, so no enemies are spawned (tests and unwired scenes stay
    ///   empty). Player/DayNight paths are absolute (/root/&lt;scene&gt;/...) so
    ///   placement works regardless of the island's tree depth.
    /// </summary>
    private void PlaceEnemies(IslandSpec spec, StaticBody3D body, float[] heightmap, Random rng)
    {
        // Fail-closed: without a player there is nothing for the AI to chase,
        // so no enemies are spawned (tests and unwired scenes stay empty).
        // Paths are resolved from the tree root (absolute) so placement works
        // regardless of the island's tree depth or the scene's node name.
        var tree = GetTree();
        if (tree == null)
            return;

        var root = tree.Root;
        var player = FindPlayerController(root);
        if (player == null)
            return;

        if (!TierEnemies.TryGetValue(spec.Tier, out var roster))
            return;

        var packed = GD.Load<PackedScene>("res://scenes/combat/enemy.tscn");
        if (packed == null)
            return;

        var playerPath = new NodePath(player.GetPath());
        var dayNight = root.FindChild("DayNightService", recursive: true, owned: false);
        var dayNightPath = dayNight == null
            ? new NodePath()
            : new NodePath(dayNight.GetPath());

        foreach (var (enemyId, modelPath, count) in roster)
        {
            var enemyData = GD.Load<EnemyData>($"res://assets/enemies/{enemyId}.tres");
            for (int i = 0; i < count; i++)
            {
                var enemy = packed.Instantiate<EnemyBase>();
                enemy.Name = $"Enemy_{enemyId}_{i}";
                enemy.EnemyData = enemyData;
                enemy.Player = playerPath;
                enemy.DayNightServicePath = dayNightPath;

                // Mount the real model as a child; EnemyBase hides the capsule
                // Visual and uses the model as the tint/flash target.
                var model = GD.Load<PackedScene>(modelPath)?.Instantiate<Node3D>();
                if (model != null)
                {
                    model.Name = "EnemyModel";
                    // Gobkit shark_king rest AABB sits below origin; lift feet to y=0.
                    if (enemyId is "shark_king" or "shark_pup")
                        model.Position = new Vector3(0f, BossPhaseController.GobkitSharkFootLiftY, 0f);
                    enemy.AddChild(model);
                    enemy.ModelPath = new NodePath("EnemyModel");
                }

                enemy.Position = GridPointToLocal(
                    spec,
                    PickHabitatGridPoint(spec, heightmap, rng, enemyId),
                    heightmap);

                // Child must exist before EnemyBase._Ready caches BossPhaseController.
                if (enemyId == "shark_king")
                    AttachSharkKingBossPhase(enemy, playerPath, dayNightPath);

                body.AddChild(enemy);
            }
        }
    }

    /// <summary>
    ///   Product Storm wiring: BossPhaseController with land shark_pup minions
    ///   (phase-2 summons + phase-3 rage). Fail-closed if scenes/data missing.
    /// </summary>
    private static void AttachSharkKingBossPhase(
        EnemyBase boss, NodePath playerPath, NodePath dayNightPath)
    {
        var minionScene = GD.Load<PackedScene>("res://scenes/combat/enemy.tscn");
        var minionData = GD.Load<EnemyData>("res://assets/enemies/shark_pup.tres");
        var minionModel = GD.Load<PackedScene>(
            "res://assets/models/enemies/shark_king/Shark.glb");
        if (minionScene == null || minionData == null)
            return;

        var phase = new BossPhaseController
        {
            Name = "BossPhaseController",
            BossId = "shark_king",
            MinionScene = minionScene,
            MinionData = minionData,
            MinionModelScene = minionModel,
            MinionModelFootLiftY = BossPhaseController.GobkitSharkFootLiftY,
            PlayerPath = playerPath,
            DayNightServicePath = dayNightPath,
            // Controller -> boss -> island body: minions must be boss siblings
            // so they do not inherit the moving boss transform.
            MinionSpawnParentPath = new NodePath("../.."),
            MinionSpawnInterval = 6f,
            MaxAliveMinions = 5
        };
        boss.AddChild(phase);
    }

    /// <summary>
    ///   Species biome band: wolves/boars inland grass near the river,
    ///   crabs on the beach / shallow water, others on any land outside
    ///   the spawn-safe ring. Always stays outside
    ///   <see cref="SpawnSafeRadius"/> on the Main island.
    /// </summary>
    private Vector2I PickHabitatGridPoint(
        IslandSpec spec, float[] heightmap, Random rng, string enemyId)
    {
        GetHabitatBand(spec, enemyId, out float minR, out float maxR, out float minH, out float maxH);

        if (enemyId is "wolf" or "boar" or "mutant")
        {
            float along = 0.28f + (float)rng.NextDouble() * 0.36f;
            float side = rng.Next(0, 2) == 0 ? 1f : -1f;
            var bank = IslandHeightmap.SampleRiverBankLocal(spec, along, side);
            var near = FindNearestValidGrid(
                spec, heightmap, bank, minR, maxR, minH, maxH);
            if (near.HasValue)
                return near.Value;
        }

        return PickLandGridPoint(
            spec, heightmap, rng,
            minRadius: minR, maxRadius: maxR,
            minHeight01: minH, maxHeight01: maxH);
    }

    private static void GetHabitatBand(
        IslandSpec spec,
        string enemyId,
        out float minR,
        out float maxR,
        out float minH,
        out float maxH)
    {
        float safe = spec.Tier == IslandTier.Main ? SpawnSafeRadius + 1.25f : 0f;
        if (enemyId == "crab")
        {
            // Tutorial beach spawn stays clear: the crab sits on the outer
            // shore, not next to the plateau spawn. Product spawn islands
            // keep the existing beach band (safe = 0).
            float tutorialClear = spec.Tier == IslandTier.Tutorial
                ? spec.Radius * 0.55f
                : 0f;
            minR = Mathf.Max(safe, Mathf.Max(tutorialClear, spec.Radius * EnemyHabitat.BeachMinRadial));
            maxR = spec.Radius * EnemyHabitat.BeachMaxRadial;
            minH = EnemyHabitat.ShoreMinHeight01;
            maxH = EnemyHabitat.ShoreMaxHeight01;
            return;
        }

        if (enemyId is "boar" or "wolf" or "mutant")
        {
            minR = Mathf.Max(safe, spec.Radius * 0.32f);
            maxR = spec.Radius * EnemyHabitat.ForestMaxRadial;
            if (spec.Tier == IslandTier.Main)
            {
                maxR = Mathf.Max(maxR, safe + 12f);
                maxR = Mathf.Min(maxR, spec.Radius * 0.60f);
            }

            minH = EnemyHabitat.ForestMinHeight01;
            maxH = EnemyHabitat.ForestMaxHeight01;
            return;
        }

        // Boss: storm island central highland — never on the beach or in the
        // surf. Storm islands are far offshore (255–310m radius) so the inner
        // plateau is the natural arena.
        if (enemyId == "shark_king")
        {
            minR = spec.Radius * 0.1f;
            maxR = spec.Radius * 0.4f;
            minH = 0.7f;
            maxH = 1.0f;
            return;
        }

        minR = safe;
        maxR = spec.Radius * IslandHeightmap.MaskFalloffStart * 0.95f;
        minH = MinLandHeight01;
        maxH = 1f;
    }

    /// <summary>
    ///   Closest grid point in the biome band to <paramref name="localXz"/>.
    ///   Null when the band is empty (caller falls back to random pick).
    /// </summary>
    private Vector2I? FindNearestValidGrid(
        IslandSpec spec,
        float[] heightmap,
        Vector2 localXz,
        float minRadius,
        float maxRadius,
        float minHeight01,
        float maxHeight01)
    {
        float cell = spec.Radius * 2f / (Resolution - 1);
        float half = (Resolution - 1) * 0.5f;
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < heightmap.Length; i++)
        {
            var point = new Vector2I(i % Resolution, i / Resolution);
            if (!IsValidLandPoint(heightmap, point, cell, half,
                    minRadius, maxRadius, minHeight01, maxHeight01))
                continue;

            float lx = (point.X - half) * cell;
            float lz = (point.Y - half) * cell;
            float d = new Vector2(lx - localXz.X, lz - localXz.Y).LengthSquared();
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        if (best < 0)
            return null;

        return new Vector2I(best % Resolution, best / Resolution);
    }

    /// <summary>
    ///   Picks a grid point inside the island's inner area whose normalized
    ///   height is at least <see cref="MinLandHeight01"/> (above sea level).
    ///   Optional radial / height bands keep trees on grassland, palms on
    ///   the beach, and enemies outside the spawn-safe ring. Falls back to
    ///   the best matching land point — guaranteed land.
    /// </summary>
    private Vector2I PickLandGridPoint(
        IslandSpec spec,
        float[] heightmap,
        Random rng,
        float minRadius = 0f,
        float maxRadius = float.MaxValue,
        float minHeight01 = MinLandHeight01,
        float maxHeight01 = 1f)
    {
        int lo = Resolution / 5;
        int hi = Resolution - Resolution / 5;
        float cell = spec.Radius * 2f / (Resolution - 1);
        float half = (Resolution - 1) * 0.5f;

        for (int i = 0; i < PropPlacementAttempts; i++)
        {
            var point = new Vector2I(rng.Next(lo, hi), rng.Next(lo, hi));
            if (IsValidLandPoint(heightmap, point, cell, half,
                    minRadius, maxRadius, minHeight01, maxHeight01))
                return point;
        }

        int best = -1;
        float bestH = -1f;
        int bestAny = 0;
        for (int i = 0; i < heightmap.Length; i++)
        {
            if (heightmap[i] > heightmap[bestAny])
                bestAny = i;
            var point = new Vector2I(i % Resolution, i / Resolution);
            if (!IsValidLandPoint(heightmap, point, cell, half,
                    minRadius, maxRadius, minHeight01, maxHeight01))
                continue;
            if (heightmap[i] > bestH)
            {
                bestH = heightmap[i];
                best = i;
            }
        }

        int pick = best >= 0 ? best : bestAny;
        return new Vector2I(pick % Resolution, pick / Resolution);
    }

    private bool IsValidLandPoint(
        float[] heightmap,
        Vector2I point,
        float cell,
        float half,
        float minRadius,
        float maxRadius,
        float minHeight01,
        float maxHeight01)
    {
        float h = heightmap[point.Y * Resolution + point.X];
        if (h < minHeight01 || h > maxHeight01)
            return false;
        float lx = (point.X - half) * cell;
        float lz = (point.Y - half) * cell;
        float d = new Vector2(lx, lz).Length();
        return d >= minRadius && d <= maxRadius;
    }

    /// <summary>
    ///   World-local position of a grid point on the terrain surface — the
    ///   same formula the mesh uses, so props sit exactly on the ground.
    /// </summary>
    private Vector3 GridPointToLocal(IslandSpec spec, Vector2I grid, float[] heightmap)
    {
        float cell = spec.Radius * 2f / (Resolution - 1);
        float half = (Resolution - 1) * 0.5f;
        float h01 = heightmap[grid.Y * Resolution + grid.X];
        float y = (h01 - 0.5f) * spec.HeightScale;
        return new Vector3((grid.X - half) * cell, y, (grid.Y - half) * cell);
    }

    /// <summary>
    ///   First <see cref="PlayerController"/> in the tree. Skips leftover
    ///   nodes named Player that are not the controller (test leaks).
    /// </summary>
    private static PlayerController? FindPlayerController(Node root)
    {
        if (root is PlayerController player)
            return player;

        foreach (Node child in root.GetChildren())
        {
            var found = FindPlayerController(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
