// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   T8.5.1 (procedural archipelago + story-point placement) tests:
///   ① heightmap determinism — the same spec yields identical output;
///   ② world-layout determinism — the same seed yields the same specs and a
///   different seed yields different ones; ③ normalized height is ~0 outside
///   the radial mask; ④ story-ring distances — harvest/ruin/mutant then storm;
///   ⑤ IslandBuilder (in a live tree) produces island bodies plus radio /
///   shark_king StoryPointTriggers and three Ruin StoryInteractables (final
///   log raises "ruin").
/// </summary>
public class IslandGeneratorTest : TestClass, IDisposable
{
    private Fixture _fixture = default!;
    private IslandBuilder _builder = default!;

    public IslandGeneratorTest(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        // Drain finalizers from earlier suites (HUD/inventory StyleBoxes etc.)
        // before IslandBuilder allocates; otherwise Godot can fatal on
        // already-released GCHandles (same guard as GameTest).
        GC.Collect();
        GC.WaitForPendingFinalizers();

        _fixture = new Fixture(TestScene.GetTree());

        // Sweep leaked scene nodes from earlier test classes (see
        // QuestServiceTest for the rationale — leaked Game scenes keep their
        // GameManager subscribed to the static event bus).
        SweepLeakedNodes(TestScene.GetTree().Root);
    }

    [Cleanup]
    public void Cleanup()
    {
        if (_builder != null && GodotObject.IsInstanceValid(_builder) && _builder.IsInsideTree())
            _builder.GetParent()!.RemoveChild(_builder);

        _fixture.Cleanup();
        Dispose();
    }

    /// <summary>
    ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it
    ///   so the disposable node field satisfies CA1001.
    /// </summary>
    public void Dispose()
    {
        _builder?.Dispose();
        _builder = null!;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///   Removes and frees leftover nodes from earlier test classes (same
    ///   contract as QuestServiceTest.SweepLeakedNodes).
    /// </summary>
    private void SweepLeakedNodes(Node root)
    {
        var current = TestScene.GetTree().CurrentScene;
        foreach (var child in root.GetChildren())
        {
            if (ReferenceEquals(child, TestScene) || ReferenceEquals(child, current))
                continue;

            if (child.Name == "Main")
                continue;

            root.RemoveChild(child);
            child.Free();
        }
    }

    private static List<T> FindDescendants<T>(Node root) where T : Node
    {
        var result = new List<T>();
        if (root is T typed)
            result.Add(typed);
        foreach (Node child in root.GetChildren())
            result.AddRange(FindDescendants<T>(child));
        return result;
    }

    /// <summary>① Same seed → byte-identical heightmap.</summary>
    [Test]
    public void HeightmapIsDeterministicForSameSeed()
    {
        var spec = new IslandSpec(
            20260815, Vector2.Zero, 24f, 10f, 0.05f, IslandTier.Spawn);

        var first = IslandHeightmap.Generate(spec, 129);
        var second = IslandHeightmap.Generate(spec, 129);

        first.Length.ShouldBe(129 * 129);
        first.SequenceEqual(second).ShouldBeTrue();
    }

    /// <summary>② Same seed → same layout; different seed → different layout.</summary>
    [Test]
    public void WorldLayoutIsDeterministicAndSeedSensitive()
    {
        var a = WorldLayout.Generate(12345);
        var b = WorldLayout.Generate(12345);
        var c = WorldLayout.Generate(54321);

        a.Count.ShouldBe(b.Count);
        a.SequenceEqual(b).ShouldBeTrue();

        var allIdentical = a.Zip(c, (x, y) => x == y).All(equal => equal);
        allIdentical.ShouldBeFalse();
    }

    /// <summary>③ Outside the bounding radius the normalized height is ~0.</summary>
    [Test]
    public void HeightmapOutsideMaskIsNearZero()
    {
        var spec = new IslandSpec(7, Vector2.Zero, 20f, 8f, 0.05f, IslandTier.Spawn);
        const int res = 129;
        var map = IslandHeightmap.Generate(spec, res);

        // Corners: distance ≈ 28.3 m > radius 20 m → mask is exactly 0.
        map[0].ShouldBe(0f, 0.000001);
        map[res - 1].ShouldBe(0f, 0.000001);
        map[(res - 1) * res].ShouldBe(0f, 0.000001);
        map[res * res - 1].ShouldBe(0f, 0.000001);

        // Points one cell in from the corner are also well outside.
        map[1].ShouldBe(0f, 0.000001);
        map[res * res - 2].ShouldBe(0f, 0.000001);
    }

    /// <summary>Different seeds produce different heightmaps (warp included).</summary>
    [Test]
    public void HeightmapDiffersAcrossSeeds()
    {
        var a = new IslandSpec(1, Vector2.Zero, 40f, 10f, 0.05f, IslandTier.Main);
        var b = new IslandSpec(2, Vector2.Zero, 40f, 10f, 0.05f, IslandTier.Main);
        IslandHeightmap.Generate(a, 65)
            .SequenceEqual(IslandHeightmap.Generate(b, 65))
            .ShouldBeFalse();
    }

    /// <summary>
    ///   Shoreline R(θ) is not a constant circle: samples at several angles
    ///   differ by more than a few percent, stay inside Radius, and stay
    ///   chunky (above the per-tier shape floor).
    /// </summary>
    [Test]
    public void ShorelineRadiusVariesWithAngle()
    {
        var layout = WorldLayout.Generate(12345);
        var specs = new[]
        {
            layout.Single(s => s.Tier == IslandTier.Main),
            layout.Single(s => s.Tier == IslandTier.Harvest),
            layout.Single(s => s.Tier == IslandTier.Ruin),
            layout.Single(s => s.Tier == IslandTier.Mutant),
            layout.Single(s => s.Tier == IslandTier.Storm),
            layout.Single(s => s.Tier == IslandTier.Wild),
            layout.Single(s => s.Tier == IslandTier.Atoll),
            layout.Single(s => s.Tier == IslandTier.Wreck),
            new IslandSpec(11, Vector2.Zero, 40f, 20f, 0.046f, IslandTier.Volcano),
            new IslandSpec(12, Vector2.Zero, 44f, 18f, 0.034f, IslandTier.Polar),
            WorldLayout.GenerateTutorialIsland(12345)
        };

        foreach (var spec in specs)
        {
            float minR = float.MaxValue;
            float maxR = 0f;
            for (int i = 0; i < 8; i++)
            {
                float theta = i * Mathf.Tau / 8f;
                float r = IslandHeightmap.ShorelineRadius(spec, theta);
                minR = Mathf.Min(minR, r);
                maxR = Mathf.Max(maxR, r);
                r.ShouldBeLessThanOrEqualTo(spec.Radius + 0.001f);
                r.ShouldBeGreaterThanOrEqualTo(
                    spec.Radius * IslandHeightmap.ShapeAmplitudeMin(spec.Tier) - 0.01f);
            }

            (maxR - minR).ShouldBeGreaterThan(spec.Radius * 0.035f);
        }
    }

    /// <summary>
    ///   Heightmap shoreline (last land along 8 rays) is irregular, and
    ///   tutorial / main plateau cells stay above sea and inland.
    /// </summary>
    [Test]
    public void HeightmapShorelineIsIrregularAndPlateauStaysLand()
    {
        AssertIrregularShore(WorldLayout.Generate(12345)[0]);
        AssertIrregularShore(WorldLayout.GenerateTutorialIsland(12345));
        AssertPlateauLand(WorldLayout.Generate(12345)[0]);
        AssertPlateauLand(WorldLayout.GenerateTutorialIsland(12345));
    }

    private static void AssertIrregularShore(IslandSpec spec)
    {
        const int res = 129;
        var map = IslandHeightmap.Generate(spec, res);
        float cell = spec.Radius * 2f / (res - 1);
        float half = (res - 1) * 0.5f;
        float minR = float.MaxValue;
        float maxR = 0f;

        for (int i = 0; i < 8; i++)
        {
            float theta = i * Mathf.Tau / 8f;
            float c = Mathf.Cos(theta);
            float s = Mathf.Sin(theta);
            float lastLand = 0f;
            for (float r = 0f; r <= spec.Radius; r += cell)
            {
                int col = Mathf.Clamp(Mathf.RoundToInt(c * r / cell + half), 0, res - 1);
                int row = Mathf.Clamp(Mathf.RoundToInt(s * r / cell + half), 0, res - 1);
                if (map[row * res + col] >= 0.48f)
                    lastLand = r;
            }

            lastLand.ShouldBeGreaterThan(spec.Radius * 0.55f);
            lastLand.ShouldBeLessThanOrEqualTo(spec.Radius + cell);
            minR = Mathf.Min(minR, lastLand);
            maxR = Mathf.Max(maxR, lastLand);
        }

        (maxR - minR).ShouldBeGreaterThan(spec.Radius * 0.03f);
    }

    private static void AssertPlateauLand(IslandSpec spec)
    {
        const int res = 129;
        var map = IslandHeightmap.Generate(spec, res);
        float cell = spec.Radius * 2f / (res - 1);
        float half = (res - 1) * 0.5f;
        float plateauH01 = 0.5f + 0.5f / spec.HeightScale;
        int inland = 0;

        for (int row = 0; row < res; row++)
        {
            for (int col = 0; col < res; col++)
            {
                float lx = (col - half) * cell;
                float lz = (row - half) * cell;
                float dist = new Vector2(lx, lz).Length();
                if (dist >= IslandHeightmap.BuildingPlateauRadius - 0.25f)
                    continue;

                float h = map[row * res + col];
                h.ShouldBeGreaterThan(0.50f);
                inland++;
                if (dist < 2.5f)
                    h.ShouldBe(plateauH01, 0.03);
            }
        }

        inland.ShouldBeGreaterThan(20);
    }

    /// <summary>④ Tier distance ranges: story ring, storm farther, main at origin.</summary>
    [Test]
    public void LayoutDistancesMatchTierRanges()
    {
        var layout = WorldLayout.Generate(12345);
        layout.Count.ShouldBeGreaterThanOrEqualTo(
            WorldLayout.ProductIslandCount + WorldLayout.EasterEggMinCount);
        layout.Count.ShouldBeLessThanOrEqualTo(
            WorldLayout.ProductIslandCount + WorldLayout.EasterEggMaxCount);
        layout.Count(s => !WorldLayout.IsEasterEgg(s.Tier))
            .ShouldBe(WorldLayout.ProductIslandCount);

        layout.Count(s => s.Tier == IslandTier.Harvest).ShouldBe(1);
        layout.Count(s => s.Tier == IslandTier.Ruin).ShouldBe(1);
        layout.Count(s => s.Tier == IslandTier.Mutant).ShouldBe(1);
        layout.Count(s => s.Tier == IslandTier.Wild).ShouldBe(1);
        layout.Count(s => s.Tier == IslandTier.Atoll).ShouldBe(1);
        layout.Count(s => s.Tier == IslandTier.Wreck).ShouldBe(1);
        layout.Any(s => s.Tier == IslandTier.Spawn).ShouldBeFalse();

        foreach (var story in layout.Where(s =>
            s.Tier is IslandTier.Harvest or IslandTier.Ruin or IslandTier.Mutant))
        {
            float distance = story.Center.Length();
            distance.ShouldBeGreaterThanOrEqualTo(WorldLayout.StoryMinDistance);
            distance.ShouldBeLessThanOrEqualTo(WorldLayout.StoryMaxDistance + 40f);
        }

        var storm = layout.Single(s => s.Tier == IslandTier.Storm);
        float stormDistance = storm.Center.Length();
        stormDistance.ShouldBeGreaterThan(WorldLayout.StoryMaxDistance);
        stormDistance.ShouldBeLessThanOrEqualTo(WorldLayout.StormMaxDistance + 8f);

        foreach (var explore in layout.Where(s => WorldLayout.IsExploration(s.Tier)))
        {
            float distance = explore.Center.Length();
            distance.ShouldBeGreaterThanOrEqualTo(WorldLayout.ExploreMinDistance - 0.5f);
            distance.ShouldBeLessThanOrEqualTo(WorldLayout.ExploreMaxDistance + 0.5f);
            (distance + explore.Radius + WorldLayout.TutorialMaxRadius + WorldLayout.IslandShoreGap)
                .ShouldBeLessThanOrEqualTo(WorldLayout.TutorialMinDistance + 0.5f);
        }

        var main = layout.Single(s => s.Tier == IslandTier.Main);
        main.Center.ShouldBe(Vector2.Zero);
        main.Radius.ShouldBe(WorldLayout.MainRadius);

        layout.Any(s => s.Tier == IslandTier.Tutorial).ShouldBeFalse();

        for (int i = 0; i < layout.Count; i++)
        {
            for (int j = i + 1; j < layout.Count; j++)
            {
                float gap = (layout[i].Center - layout[j].Center).Length()
                    - layout[i].Radius - layout[j].Radius;
                gap.ShouldBeGreaterThan(WorldLayout.IslandShoreGap - 0.01f);
            }
        }
    }

    /// <summary>
    ///   Tutorial island uses a separate seed stream: same seed → same spec,
    ///   never overlaps product islands, and is absent from Generate().
    /// </summary>
    [Test]
    public void TutorialIslandIsDeterministicAndOffTheProductRing()
    {
        var a = WorldLayout.GenerateTutorialIsland(12345);
        var b = WorldLayout.GenerateTutorialIsland(12345);
        a.ShouldBe(b);
        a.Tier.ShouldBe(IslandTier.Tutorial);

        var other = WorldLayout.GenerateTutorialIsland(54321);
        (a == other).ShouldBeFalse();

        float distance = a.Center.Length();
        distance.ShouldBeGreaterThanOrEqualTo(WorldLayout.TutorialMinDistance);
        distance.ShouldBeLessThanOrEqualTo(WorldLayout.TutorialMaxDistance);

        foreach (var spec in WorldLayout.Generate(12345))
        {
            float gap = (a.Center - spec.Center).Length();
            gap.ShouldBeGreaterThan(a.Radius + spec.Radius);
        }
    }

    /// <summary>
    ///   Starter island is walkable (not a tutorial clone, not an empty disc),
    ///   and story islands sit outside its radius so the rings never overlap.
    /// </summary>
    [Test]
    public void StarterIslandIsLargeAndDoesNotOverlapSpawnRing()
    {
        WorldLayout.MainRadius.ShouldBeGreaterThanOrEqualTo(108f);
        WorldLayout.MainRadius.ShouldBeLessThanOrEqualTo(132f);
        IslandBuilder.SpawnSafeRadius.ShouldBe(40f);
        WorldLayout.StoryMinDistance.ShouldBeGreaterThan(
            WorldLayout.MainRadius + WorldLayout.HarvestMaxRadius);
    }

    /// <summary>
    ///   ⑤ IslandBuilder places island bodies and per-tier story hooks;
    ///   radio Area on Main, shark_king Area on Storm, ruin via the third
    ///   StoryInteractable on the Ruin island (no walk-in Area).
    /// </summary>
    [Test]
    public async Task BuilderPlacesStoryPointsAndIslands()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var triggers = FindDescendants<StoryPointTrigger>(_builder);
        triggers.Count(t => t.StoryPointId == "radio").ShouldBe(1);
        triggers.Count(t => t.StoryPointId == "shark_king").ShouldBe(1);
        triggers.Count(t => t.StoryPointId == "ruin").ShouldBe(0);

        var islands = FindDescendants<StaticBody3D>(_builder)
            .Where(b => b.Name.ToString().StartsWith("Island_"))
            .ToList();
        islands.Count.ShouldBe(WorldLayout.Generate(12345).Count);

        var main = islands.Single(b => b.Name.ToString().StartsWith("Island_Main"));
        main.Position.X.ShouldBe(0f, 0.0001);
        main.Position.Y.ShouldBe(0f, 0.0001);
        main.Position.Z.ShouldBe(0f, 0.0001);

        FindDescendants<StoryPointTrigger>(main)
            .Any(t => t.StoryPointId == "radio").ShouldBeTrue();
        FindDescendants<StoryPointTrigger>(main)
            .Any(t => t.StoryPointId == "ruin").ShouldBeFalse();

        var ruinIsland = islands.Single(b => b.Name.ToString().StartsWith("Island_Ruin"));
        var ruinLogs = FindDescendants<StoryInteractable>(ruinIsland).ToList();
        ruinLogs.Count.ShouldBe(IslandLore.RuinQuestLogs.Length);
        ruinLogs.Count(l => l.StoryPointId == "ruin").ShouldBe(1);
        ruinLogs.Count(l => string.IsNullOrEmpty(l.StoryPointId)).ShouldBe(2);

        islands.Any(b => b.Name.ToString().StartsWith("Island_Tutorial"))
            .ShouldBeFalse();

        islands.Count(b => b.Name.ToString().StartsWith("Island_Wild")).ShouldBe(1);
        islands.Count(b => b.Name.ToString().StartsWith("Island_Atoll")).ShouldBe(1);
        islands.Count(b => b.Name.ToString().StartsWith("Island_Wreck")).ShouldBe(1);

        foreach (var extra in islands.Where(b =>
            b.Name.ToString().StartsWith("Island_Wild")
            || b.Name.ToString().StartsWith("Island_Atoll")
            || b.Name.ToString().StartsWith("Island_Wreck")
            || b.Name.ToString().StartsWith("Island_Volcano")
            || b.Name.ToString().StartsWith("Island_Polar")))
        {
            FindDescendants<StoryPointTrigger>(extra)
                .Any(t => t.StoryPointId is "radio" or "ruin" or "shark_king")
                .ShouldBeFalse();
        }

        // Fail-closed: a second Generate call must be a no-op.
        _builder.Generate();
        FindDescendants<StaticBody3D>(_builder)
            .Count(b => b.Name.ToString().StartsWith("Island_"))
            .ShouldBe(islands.Count);
    }

    /// <summary>
    ///   ⑥ T9.x: with a PlayerController in the tree, each tier's islands get
    ///   their enemy roster with real models wired (crab+boar+wolf on Main,
    ///   mutant on the woods island, storm_beast on the storm island);
    ///   without a player, no enemies are placed (fail-closed).
    /// </summary>
    [Test]
    public async Task BuilderPlacesEnemiesPerTierWhenPlayerExists()
    {
        var player = new PlayerController { Name = "Player" };
        await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var enemies = FindDescendants<EnemyBase>(_builder).ToList();
        enemies.Count.ShouldBeGreaterThan(0);

        var main = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Main"));
        var harvest = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Harvest"));
        var ruin = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Ruin"));
        var wild = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Wild"));
        var atoll = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Atoll"));

        FindDescendants<EnemyBase>(main)
            .Count(e => e.Name.ToString().StartsWith("Enemy_crab_")).ShouldBe(5);
        FindDescendants<EnemyBase>(main)
            .Count(e => e.Name.ToString().StartsWith("Enemy_boar_")).ShouldBe(5);
        FindDescendants<EnemyBase>(main)
            .Count(e => e.Name.ToString().StartsWith("Enemy_wolf_")).ShouldBe(5);
        FindDescendants<EnemyBase>(harvest).Count.ShouldBe(0);
        FindDescendants<EnemyBase>(ruin).Count.ShouldBe(0);
        FindDescendants<EnemyBase>(wild)
            .Count(e => e.Name.ToString().StartsWith("Enemy_wolf_")).ShouldBe(5);
        FindDescendants<EnemyBase>(wild)
            .Count(e => e.Name.ToString().StartsWith("Enemy_boar_")).ShouldBe(5);
        FindDescendants<EnemyBase>(atoll)
            .Count(e => e.Name.ToString().StartsWith("Enemy_crab_")).ShouldBe(7);

        var mutant = enemies.Count(e => e.Name.ToString().StartsWith("Enemy_mutant_"));
        var stormBeast = enemies.Count(e => e.Name.ToString().StartsWith("Enemy_storm_beast_"));
        var sharkKings = enemies
            .Where(e => e.Name.ToString().StartsWith("Enemy_shark_king_"))
            .ToList();
        mutant.ShouldBe(5);
        stormBeast.ShouldBeGreaterThan(0);
        sharkKings.Count.ShouldBe(1);

        // Product wiring, not just the storm_zone lab: the king owns the phase
        // controller and phase 2 is configured with the land shark-pup resource.
        var sharkKing = sharkKings.Single();
        var phase = sharkKing.GetNodeOrNull<BossPhaseController>("BossPhaseController");
        phase.ShouldNotBeNull();
        phase!.MinionData.ShouldNotBeNull();
        phase.MinionData!.Id.ShouldBe("shark_pup");
        phase.MinionData.Behavior.ShouldBe(EnemyBehavior.MeleeChase);
        phase.MinionModelScene.ShouldNotBeNull();
        phase.MinionSpawnParentPath.ToString().ShouldBe("../..");
        phase.MinionModelFootLiftY.ShouldBe(
            BossPhaseController.GobkitSharkFootLiftY, tolerance: 0.001f);

        // The product king must occupy the Storm island's central highland
        // habitat band: radius 0.1–0.4R and normalized height 0.7–1.0.
        var stormSpec = WorldLayout.Generate(12345)
            .Single(s => s.Tier == IslandTier.Storm);
        float kingRadius = new Vector2(
            sharkKing.Position.X, sharkKing.Position.Z).Length();
        float kingHeight01 = sharkKing.Position.Y / stormSpec.HeightScale + 0.5f;
        kingRadius.ShouldBeGreaterThanOrEqualTo(stormSpec.Radius * 0.1f - 0.01f);
        kingRadius.ShouldBeLessThanOrEqualTo(stormSpec.Radius * 0.4f + 0.01f);
        kingHeight01.ShouldBeGreaterThanOrEqualTo(0.7f - 0.001f);
        kingHeight01.ShouldBeLessThanOrEqualTo(1.0f + 0.001f);

        foreach (var enemy in enemies)
        {
            enemy.EnemyData.ShouldNotBeNull();
            enemy.GetNodeOrNull<Node3D>("EnemyModel").ShouldNotBeNull();
        }

        // Stop the old enemies before freeing their cached player reference;
        // otherwise they keep physics-processing a disposed PlayerController
        // while the fail-closed half of this test creates a second builder.
        _builder.GetParent()?.RemoveChild(_builder);
        _builder.Free();
        _builder = null!;

        // Fail-closed: without a player nothing is spawned — remove the player
        // and a fresh builder must place no enemies.
        player.GetParent()?.RemoveChild(player);
        player.QueueFree();

        var noPlayerBuilder = new IslandBuilder { WorldSeed = 999 };
        await _fixture.AddToRoot(noPlayerBuilder, autoRemoveFromRoot: true);
        FindDescendants<EnemyBase>(noPlayerBuilder).Count.ShouldBe(0);
    }

    /// <summary>
    ///   世界观剧情文本散落：每个非教程产品岛至少一个可读
    ///   StoryInteractable；非 Ruin 岛 StoryPointId 为空（纯叙述）；Ruin 岛
    ///   为有序三段，末段 StoryPointId="ruin"。教程岛保持干净。
    ///   StoryInteractable 不依赖 PlayerController，所以无玩家也会放置。
    /// </summary>
    [Test]
    public async Task BuilderPlacesStoryInteractablesOnEveryNonTutorialTier()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var islands = FindDescendants<StaticBody3D>(_builder)
            .Where(b => b.Name.ToString().StartsWith("Island_"))
            .ToList();

        // 主线 5 岛 + 探索 3 岛必定生成；彩蛋火山/雪山按 seed roll 1–2 个。
        var coreTiers = new[]
        {
            IslandTier.Main, IslandTier.Harvest, IslandTier.Ruin,
            IslandTier.Mutant, IslandTier.Storm,
            IslandTier.Wild, IslandTier.Atoll, IslandTier.Wreck
        };
        foreach (var tier in coreTiers)
        {
            var body = islands.Single(b =>
                b.Name.ToString().StartsWith($"Island_{tier}_"));
            if (tier == IslandTier.Ruin)
                AssertRuinQuestLogs(body);
            else
                AssertPureNarrationLogs(body);
        }
        islands.Count.ShouldBeGreaterThanOrEqualTo(coreTiers.Length + 1);

        // 彩蛋岛只要生成了，也必须带可读文本。
        foreach (var egg in islands.Where(b =>
            b.Name.ToString().StartsWith("Island_Volcano_")
            || b.Name.ToString().StartsWith("Island_Polar_")))
        {
            AssertPureNarrationLogs(egg);
        }

        // 教程岛不散落。
        _builder.EnsureTutorialIsland();
        var tutorial = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Tutorial"));
        FindDescendants<StoryInteractable>(tutorial).Count.ShouldBe(0);
    }

    /// <summary>
    ///   Asserts the island body carries at least one readable log, all of
    ///   them pure narration (empty StoryPointId, non-empty Text).
    /// </summary>
    private static void AssertPureNarrationLogs(StaticBody3D body)
    {
        var logs = FindDescendants<StoryInteractable>(body).ToList();
        logs.Count.ShouldBeGreaterThanOrEqualTo(1);
        foreach (var log in logs)
        {
            log.StoryPointId.ShouldBe("");
            log.Text.ShouldNotBeNullOrEmpty();
        }
    }

    /// <summary>
    ///   Ruin island: three ordered quest logs; only the last raises "ruin".
    /// </summary>
    private static void AssertRuinQuestLogs(StaticBody3D body)
    {
        var logs = FindDescendants<StoryInteractable>(body)
            .OrderBy(l => l.Name.ToString())
            .ToList();
        logs.Count.ShouldBe(IslandLore.RuinQuestLogs.Length);
        for (var i = 0; i < logs.Count; i++)
        {
            logs[i].Text.ShouldBe(IslandLore.RuinQuestLogs[i]);
            logs[i].StoryPointId.ShouldBe(i == logs.Count - 1 ? "ruin" : "");
        }
    }

    /// <summary>
    ///   Generated Main-island enemies stay outside the spawn-safe radius
    ///   so the player does not appear inside AttackRange.
    /// </summary>
    [Test]
    public async Task BuilderPlacesMainEnemiesOutsideSpawnSafeRadius()
    {
        var player = new PlayerController { Name = "Player" };
        await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var main = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Main"));
        var enemies = FindDescendants<EnemyBase>(main).ToList();
        enemies.Count.ShouldBeGreaterThan(0);

        foreach (var enemy in enemies)
        {
            float xz = new Vector2(enemy.Position.X, enemy.Position.Z).Length();
            xz.ShouldBeGreaterThanOrEqualTo(IslandBuilder.SpawnSafeRadius - 0.75f);
        }
    }

    /// <summary>
    ///   Wolves/boars stay inland of the beach ring; crabs sit on the
    ///   shore / shallow water. Nobody is placed inside the spawn-safe disk.
    /// </summary>
    [Test]
    public async Task BuilderPlacesEnemiesInSpeciesHabitats()
    {
        var player = new PlayerController { Name = "Player" };
        await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var main = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Main"));
        float beachStart = WorldLayout.MainRadius * 0.62f;

        foreach (var enemy in FindDescendants<EnemyBase>(main))
        {
            float xz = new Vector2(enemy.Position.X, enemy.Position.Z).Length();
            xz.ShouldBeGreaterThanOrEqualTo(IslandBuilder.SpawnSafeRadius - 0.75f);

            var id = enemy.Name.ToString();
            if (id.StartsWith("Enemy_boar_") || id.StartsWith("Enemy_wolf_"))
                xz.ShouldBeLessThan(beachStart + 2f);
        }

        foreach (var crab in FindDescendants<EnemyBase>(main)
            .Where(e => e.Name.ToString().StartsWith("Enemy_crab_")))
        {
            float xz = new Vector2(crab.Position.X, crab.Position.Z).Length();
            xz.ShouldBeGreaterThan(WorldLayout.MainRadius * 0.55f);
            xz.ShouldBeGreaterThanOrEqualTo(IslandBuilder.SpawnSafeRadius - 0.75f);
        }
    }

    /// <summary>
    ///   Same world seed yields the same generated enemy names and local
    ///   positions (archipelago determinism).
    /// </summary>
    [Test]
    public async Task SameSeedPlacesEnemiesInTheSameLayout()
    {
        var player = new PlayerController { Name = "Player" };
        await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

        var firstBuilder = new IslandBuilder { WorldSeed = 12345 };
        player.GetParent()!.AddChild(firstBuilder);
        var first = SnapshotEnemies(firstBuilder);
        firstBuilder.GetParent()!.RemoveChild(firstBuilder);
        firstBuilder.Free();

        _builder = new IslandBuilder { WorldSeed = 12345 };
        player.GetParent()!.AddChild(_builder);
        var second = SnapshotEnemies(_builder);

        first.Count.ShouldBeGreaterThan(0);
        first.ShouldBe(second);
    }

    private static List<(string Name, Vector3 Position)> SnapshotEnemies(Node root) =>
        FindDescendants<EnemyBase>(root)
            .Select(e => ($"{e.GetParent()?.Name}/{e.Name}", e.Position))
            .OrderBy(t => t.Item1)
            .ToList();

    /// <summary>
    ///   Exploration islands exist from Generate() (not after the boss), sit
    ///   between Storm and Tutorial, and never carry radio/ruin/shark_king.
    /// </summary>
    [Test]
    public void ExplorationIslandsAreOptionalAndOffTheQuestChain()
    {
        var a = WorldLayout.Generate(12345);
        var b = WorldLayout.Generate(12345);
        var c = WorldLayout.Generate(54321);

        a.Count(s => !WorldLayout.IsEasterEgg(s.Tier))
            .ShouldBe(WorldLayout.ProductIslandCount);
        a.Count(s => WorldLayout.IsExploration(s.Tier))
            .ShouldBe(WorldLayout.ExplorationIslandCount);
        a.SequenceEqual(b).ShouldBeTrue();

        var aExplore = a.Where(s => WorldLayout.IsExploration(s.Tier)).ToList();
        var cExplore = c.Where(s => WorldLayout.IsExploration(s.Tier)).ToList();
        aExplore.SequenceEqual(cExplore).ShouldBeFalse();

        var tutorial = WorldLayout.GenerateTutorialIsland(12345);
        foreach (var spec in aExplore)
        {
            spec.Tier.ShouldNotBe(IslandTier.Tutorial);
            float gap = (spec.Center - tutorial.Center).Length()
                - spec.Radius - tutorial.Radius;
            gap.ShouldBeGreaterThan(WorldLayout.IslandShoreGap - 0.01f);
        }

        a.Any(s => s.Tier == IslandTier.Tutorial).ShouldBeFalse();
    }

    /// <summary>
    ///   Builder vegetation quotas: wild extra trees, atoll extra palms,
    ///   wreck salvage props. No story-point IDs on those islands.
    /// </summary>
    [Test]
    public async Task BuilderPlacesExplorationVegetationAndSalvage()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var wild = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Wild"));
        FindDescendants<WoodTree>(wild).Count.ShouldBe(IslandBuilder.WildTreeCount);
        FindDescendants<VegetationProp>(wild)
            .Count(p => p.Name.ToString().StartsWith("DecoTree_"))
            .ShouldBeGreaterThan(0);
        FindDescendants<VegetationProp>(wild)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeGreaterThanOrEqualTo(24);

        var atoll = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Atoll"));
        FindDescendants<CoconutPalm>(atoll).Count.ShouldBe(IslandBuilder.AtollPalmCount);
        FindDescendants<VegetationProp>(atoll)
            .Count(p => p.Name.ToString().StartsWith("DecoPalm_"))
            .ShouldBeGreaterThan(0);
        FindDescendants<VegetationProp>(atoll)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeGreaterThanOrEqualTo(16);

        var wreck = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Wreck"));
        FindDescendants<VegetationProp>(wreck)
            .Count(p => p.Name.ToString().StartsWith("Crate_"))
            .ShouldBeGreaterThan(0);
        FindDescendants<VegetationProp>(wreck)
            .Count(p => p.Name.ToString().StartsWith("Barrel_"))
            .ShouldBeGreaterThan(0);
        FindDescendants<VegetationProp>(wreck)
            .Any(p => p.Name.ToString() == "Landmark_ShipWreck")
            .ShouldBeTrue();
        FindDescendants<StoryPointTrigger>(wreck).Count.ShouldBe(0);
    }

    /// <summary>
    ///   Biome vegetation: Main forest+grass off the 24 m plateau, harvest
    ///   field grass, mutant woods. Harvestable counts stay on the quota
    ///   constants; extra trunks are VegetationProp.
    /// </summary>
    [Test]
    public async Task BuilderPlacesDenseBiomeVegetationOffPlateau()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);
        var layout = WorldLayout.Generate(12345);

        var mainSpec = layout.Single(s => s.Tier == IslandTier.Main);
        var main = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Main"));
        FindDescendants<VegetationProp>(main)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeGreaterThanOrEqualTo(80);
        FindDescendants<VegetationProp>(main)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeLessThanOrEqualTo(IslandBuilder.MainGrassCount);
        FindDescendants<WoodTree>(main).Count.ShouldBeGreaterThanOrEqualTo(16);
        FindDescendants<VegetationProp>(main)
            .Count(p => p.Name.ToString().StartsWith("DecoTree_"))
            .ShouldBeGreaterThan(0);
        FindDescendants<CoconutPalm>(main).Count.ShouldBeGreaterThanOrEqualTo(6);

        foreach (var tree in FindDescendants<WoodTree>(main))
        {
            float xz = new Vector2(tree.Position.X, tree.Position.Z).Length();
            xz.ShouldBeGreaterThan(IslandHeightmap.BuildingPlateauRadius - 0.5f);
            xz.ShouldBeLessThan(mainSpec.Radius * 0.60f + 1f);
        }

        foreach (var deco in FindDescendants<VegetationProp>(main)
            .Where(p => p.Name.ToString().StartsWith("DecoTree_")))
        {
            float xz = new Vector2(deco.Position.X, deco.Position.Z).Length();
            xz.ShouldBeGreaterThan(IslandHeightmap.BuildingPlateauRadius - 0.5f);
            xz.ShouldBeLessThan(mainSpec.Radius * 0.60f + 1f);
        }

        var harvest = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Harvest"));
        FindDescendants<VegetationProp>(harvest)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeGreaterThanOrEqualTo(48);
        FindDescendants<VegetationProp>(harvest)
            .Count(p => p.Name.ToString().StartsWith("Grass_"))
            .ShouldBeLessThanOrEqualTo(IslandBuilder.HarvestGrassCount);
        FindDescendants<WoodTree>(harvest).Count.ShouldBeLessThanOrEqualTo(12);

        var mutant = FindDescendants<StaticBody3D>(_builder)
            .Single(b => b.Name.ToString().StartsWith("Island_Mutant"));
        int mutantCanopy = FindDescendants<WoodTree>(mutant).Count
            + FindDescendants<VegetationProp>(mutant)
                .Count(p => p.Name.ToString().StartsWith("DecoTree_"));
        mutantCanopy.ShouldBeGreaterThanOrEqualTo(30);
        mutantCanopy.ShouldBeLessThanOrEqualTo(110);
    }

    /// <summary>
    ///   Easter eggs: same seed → same presence / poles / centers; 1 or 2
    ///   islands; locked to far ±Z; never overlap tutorial or quest IDs.
    ///   Different seeds can pick a different set.
    /// </summary>
    [Test]
    public void EasterEggsAreDeterministicPolesAndOffTheQuestChain()
    {
        var a = WorldLayout.Generate(12345);
        var b = WorldLayout.Generate(12345);
        a.SequenceEqual(b).ShouldBeTrue();

        var eggs = a.Where(s => WorldLayout.IsEasterEgg(s.Tier)).ToList();
        eggs.Count.ShouldBeGreaterThanOrEqualTo(WorldLayout.EasterEggMinCount);
        eggs.Count.ShouldBeLessThanOrEqualTo(WorldLayout.EasterEggMaxCount);
        eggs.Count(s => s.Tier == IslandTier.Volcano).ShouldBeLessThanOrEqualTo(1);
        eggs.Count(s => s.Tier == IslandTier.Polar).ShouldBeLessThanOrEqualTo(1);

        var tutorial = WorldLayout.GenerateTutorialIsland(12345);
        foreach (var egg in eggs)
        {
            Mathf.Abs(egg.Center.Y).ShouldBeGreaterThanOrEqualTo(
                WorldLayout.EasterMinDistance - 0.5f);
            Mathf.Abs(egg.Center.Y).ShouldBeLessThanOrEqualTo(
                WorldLayout.EasterMaxDistance + 0.5f);
            Mathf.Abs(egg.Center.X).ShouldBeLessThan(80f);

            float gap = (egg.Center - tutorial.Center).Length()
                - egg.Radius - tutorial.Radius;
            gap.ShouldBeGreaterThan(WorldLayout.IslandShoreGap - 0.01f);
        }

        if (eggs.Count == 2)
            (eggs[0].Center.Y * eggs[1].Center.Y).ShouldBeLessThan(0f);

        var signatures = new HashSet<string>();
        for (int seed = 1; seed <= 60; seed++)
        {
            var set = WorldLayout.Generate(seed)
                .Where(s => WorldLayout.IsEasterEgg(s.Tier))
                .Select(s => s.Tier.ToString())
                .OrderBy(t => t);
            signatures.Add(string.Join("+", set));
        }

        signatures.Count.ShouldBeGreaterThan(1);
    }

    [Test]
    public async Task BuilderPlacesEasterEggBiomesWithoutQuestPoints()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var layout = WorldLayout.Generate(12345);
        foreach (var spec in layout.Where(s => WorldLayout.IsEasterEgg(s.Tier)))
        {
            var body = FindDescendants<StaticBody3D>(_builder)
                .Single(b => b.Name.ToString().StartsWith($"Island_{spec.Tier}"));
            FindDescendants<StoryPointTrigger>(body).Count.ShouldBe(0);
            FindDescendants<CoconutPalm>(body).Count.ShouldBe(0);

            if (spec.Tier == IslandTier.Volcano)
            {
                FindDescendants<MeshInstance3D>(body)
                    .Count(m => m.Name.ToString().StartsWith("LavaPool_"))
                    .ShouldBeGreaterThan(0);
                FindDescendants<WoodTree>(body).Count.ShouldBe(IslandBuilder.VolcanoTreeCount);
            }

            if (spec.Tier == IslandTier.Polar)
                FindDescendants<WoodTree>(body).Count.ShouldBe(IslandBuilder.PolarTreeCount);
        }
    }
}
