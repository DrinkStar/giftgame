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
///   the radial mask; ④ tier distance ranges — storm &gt; 300 m, spawn
///   islands 120-180 m; ⑤ IslandBuilder (in a live tree) produces island
///   bodies plus the "radio" and "shark_king" StoryPointTriggers and never
///   a duplicate "ruin" trigger.
/// </summary>
public class IslandGeneratorTest : TestClass, IDisposable
{
    private Fixture _fixture = default!;
    private IslandBuilder _builder = default!;

    public IslandGeneratorTest(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _fixture = new Fixture(TestScene.GetTree());

        // Sweep leaked scene nodes from earlier test classes (see
        // QuestServiceTest for the rationale — leaked Game scenes keep their
        // GameManager subscribed to the static event bus).
        SweepLeakedNodes(TestScene.GetTree().Root);
    }

    [Cleanup]
    public void Cleanup()
    {
        if (_builder != null && _builder.IsInsideTree())
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

    /// <summary>③ Outside the radial mask the normalized height is ~0.</summary>
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

    /// <summary>④ Tier distance ranges: storm &gt; 300 m, spawn 120-180 m, main at origin.</summary>
    [Test]
    public void LayoutDistancesMatchTierRanges()
    {
        var layout = WorldLayout.Generate(12345);

        var spawns = layout.Where(s => s.Tier == IslandTier.Spawn).ToList();
        spawns.Count.ShouldBeInRange(WorldLayout.MinSpawnIslands, WorldLayout.MaxSpawnIslands);

        foreach (var spawn in spawns)
        {
            float distance = spawn.Center.Length();
            distance.ShouldBeGreaterThanOrEqualTo(WorldLayout.SpawnMinDistance);
            distance.ShouldBeLessThanOrEqualTo(WorldLayout.SpawnMaxDistance);
        }

        var storm = layout.Single(s => s.Tier == IslandTier.Storm);
        float stormDistance = storm.Center.Length();
        stormDistance.ShouldBeGreaterThan(300f);
        stormDistance.ShouldBeLessThanOrEqualTo(WorldLayout.StormMaxDistance);

        var main = layout.Single(s => s.Tier == IslandTier.Main);
        main.Center.ShouldBe(Vector2.Zero);
    }

    /// <summary>
    ///   ⑤ IslandBuilder places island bodies and the per-tier story points;
    ///   "ruin" stays scene-owned and is never duplicated.
    /// </summary>
    [Test]
    public async Task BuilderPlacesStoryPointsAndIslands()
    {
        _builder = new IslandBuilder { WorldSeed = 12345 };
        await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

        var triggers = FindDescendants<StoryPointTrigger>(_builder);
        triggers.Any(t => t.StoryPointId == "radio").ShouldBeTrue();
        triggers.Any(t => t.StoryPointId == "shark_king").ShouldBeTrue();
        triggers.Any(t => t.StoryPointId == "ruin").ShouldBeFalse();

        var islands = FindDescendants<StaticBody3D>(_builder)
            .Where(b => b.Name.ToString().StartsWith("Island_"))
            .ToList();
        // 1 main + 2-3 spawn + 1 storm.
        islands.Count.ShouldBeGreaterThanOrEqualTo(4);

        var main = islands.Single(b => b.Name.ToString().StartsWith("Island_Main"));
        main.Position.X.ShouldBe(0f, 0.0001);
        main.Position.Y.ShouldBe(0f, 0.0001);
        main.Position.Z.ShouldBe(0f, 0.0001);

        // Fail-closed: a second Generate call must be a no-op.
        _builder.Generate();
        FindDescendants<StaticBody3D>(_builder)
            .Count(b => b.Name.ToString().StartsWith("Island_"))
            .ShouldBe(islands.Count);
    }
}
