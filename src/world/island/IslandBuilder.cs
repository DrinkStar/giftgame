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
///     <description><see cref="IslandTier.Spawn"/>: 2-3 <see cref="WoodTree"/>
///     + 1 <see cref="CoconutPalm"/> + a <see cref="StoryPointTrigger"/> with
///     <c>StoryPointId = "radio"</c> (per spawn island).</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Main"/>: a couple of
///     <see cref="WoodTree"/>. The "ruin" trigger intentionally stays in
///     Game.tscn — this generator must NOT duplicate it (two nodes would
///     both raise the event).</description>
///   </item>
///   <item>
///     <description><see cref="IslandTier.Storm"/>: a <see cref="StoryPointTrigger"/>
///     with <c>StoryPointId = "shark_king"</c> — the island is the boss
///     arena; storm weather is handled by WeatherService / the main
///     orchestrator, not by this class.</description>
///   </item>
///   </list>
///
///   Everything is deterministic: the default <see cref="WorldSeed"/> of
///   12345 plus the per-island seeds drawn from the layout reproduce the
///   same archipelago on every run. Prop positions are drawn from a
///   per-island <see cref="Random"/> seeded with the island seed and always
///   placed on the heightmap surface at or above sea level — no hardcoded
///   world coordinates anywhere.
/// </summary>
public partial class IslandBuilder : Node3D
{
    /// <summary>World generation seed; the fixed default keeps the archipelago deterministic.</summary>
    [Export] public long WorldSeed = 12345;

    /// <summary>Heightmap / mesh / collision grid size per axis (129² default).</summary>
    [Export] public int Resolution = IslandHeightmap.DefaultResolution;

    /// <summary>Generate during <c>_Ready</c>; disable to call <see cref="Generate"/> manually.</summary>
    [Export] public bool GenerateOnReady = true;

    /// <summary>Lowest normalized height accepted for prop placement (keeps props above sea level).</summary>
    public const float MinLandHeight01 = 0.55f;

    /// <summary>Random attempts to find a land grid point before falling back to the highest point.</summary>
    private const int PropPlacementAttempts = 16;

    /// <summary>Extra height above the surface for story-point triggers (the sphere meets the player capsule).</summary>
    private const float TriggerHeightOffset = 1.5f;

    /// <summary>Main-island tree count ("少量").</summary>
    private const int MainTreeCount = 2;

    /// <summary>Fail-closed guard: the archipelago is generated exactly once.</summary>
    private bool _generated;

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
        for (int i = 0; i < specs.Count; i++)
            BuildIsland(specs[i], i);
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
        return new StandardMaterial3D
        {
            // Terrain must be visible from any angle (inside slopes, below
            // the waterline), so back-face culling is disabled.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            // T9.5: vertex colors carry the sand→land biome gradient written
            // by IslandMeshBuilder; albedo stays white so colors show as-is.
            VertexColorUseAsAlbedo = true,
            AlbedoColor = Colors.White
        };
    }

    private void PopulateIsland(IslandSpec spec, StaticBody3D body, float[] heightmap)
    {
        // Per-island RNG seeded from the island seed: props are deterministic.
        var rng = new Random(unchecked((int)spec.Seed ^ 0x51ED2701));

        switch (spec.Tier)
        {
            case IslandTier.Spawn:
                PlaceTrees(spec, body, heightmap, rng, count: 2 + rng.Next(2)); // 2-3
                PlacePalm(spec, body, heightmap, rng);
                PlaceStoryPoint(spec, body, heightmap, rng, "radio", reusable: false);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Main:
                // "ruin" is intentionally NOT placed here — Game.tscn already
                // owns the ruin trigger; duplicating it would raise the
                // event from two nodes.
                PlaceTrees(spec, body, heightmap, rng, MainTreeCount);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            case IslandTier.Storm:
                // The storm island IS the shark-king boss arena. Weather is
                // handled by WeatherService / the main orchestrator.
                PlaceStoryPoint(spec, body, heightmap, rng, "shark_king", reusable: true);
                PlaceEnemies(spec, body, heightmap, rng);
                break;

            default:
                throw new InvalidOperationException($"Unhandled island tier '{spec.Tier}'.");
        }
    }

    private void PlaceTrees(IslandSpec spec, StaticBody3D body, float[] heightmap, Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var point = PickLandGridPoint(heightmap, rng);
            body.AddChild(new WoodTree
            {
                Name = $"WoodTree_{i}",
                Position = GridPointToLocal(spec, point, heightmap)
            });
        }
    }

    private void PlacePalm(IslandSpec spec, StaticBody3D body, float[] heightmap, Random rng)
    {
        var point = PickLandGridPoint(heightmap, rng);
        body.AddChild(new CoconutPalm
        {
            Name = "CoconutPalm_0",
            Position = GridPointToLocal(spec, point, heightmap)
        });
    }

    private void PlaceStoryPoint(
        IslandSpec spec,
        StaticBody3D body,
        float[] heightmap,
        Random rng,
        string storyPointId,
        bool reusable
    )
    {
        var point = PickLandGridPoint(heightmap, rng);
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

    /// <summary>T9.x enemy roster per island tier (id, model, count).</summary>
    private static readonly Dictionary<IslandTier, (string EnemyId, string ModelPath, int Count)[]> TierEnemies =
        new()
        {
            [IslandTier.Spawn] = new[] { ("crab", "res://assets/models/enemies/crab/Crab.glb", 2) },
            [IslandTier.Main] = new[]
            {
                ("boar", "res://assets/models/enemies/boar/Pig.glb", 2),
                ("wolf", "res://assets/models/enemies/wolf/Wolf.gltf", 2)
            },
            [IslandTier.Storm] = new[] { ("storm_beast", "res://assets/models/enemies/storm_beast/Squidle.glb", 2) }
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
        var player = root.FindChild("Player", recursive: true, owned: false) as PlayerController;
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
                    enemy.AddChild(model);
                    enemy.ModelPath = new NodePath("EnemyModel");
                }

                enemy.Position = GridPointToLocal(spec, PickLandGridPoint(heightmap, rng), heightmap);
                body.AddChild(enemy);
            }
        }
    }

    /// <summary>
    ///   Picks a grid point inside the island's inner area whose normalized
    ///   height is at least <see cref="MinLandHeight01"/> (above sea level).
    ///   Falls back to the highest grid point — guaranteed land.
    /// </summary>
    private Vector2I PickLandGridPoint(float[] heightmap, Random rng)
    {
        int lo = Resolution / 5;
        int hi = Resolution - Resolution / 5;

        for (int i = 0; i < PropPlacementAttempts; i++)
        {
            var point = new Vector2I(rng.Next(lo, hi), rng.Next(lo, hi));
            if (heightmap[point.Y * Resolution + point.X] >= MinLandHeight01)
                return point;
        }

        // Fail-closed fallback: the highest point is always the most above
        // sea level.
        int best = 0;
        for (int i = 1; i < heightmap.Length; i++)
        {
            if (heightmap[i] > heightmap[best])
                best = i;
        }
        return new Vector2I(best % Resolution, best / Resolution);
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
}
