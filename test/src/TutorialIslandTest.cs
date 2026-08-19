// Original — no upstream port
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
///   Dedicated tutorial island: built only on the 新手教程 path, stocked
///   for every chapter-1 step, and handed off to the product spawn when
///   chapter 1 finishes.
/// </summary>
public class TutorialIslandTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private IslandBuilder _builder = default!;
  private PlayerController _player = default!;

  public TutorialIslandTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    GC.Collect();
    GC.WaitForPendingFinalizers();

    _fixture = new Fixture(TestScene.GetTree());
    SweepLeakedNodes(TestScene.GetTree().Root);

    _player = new PlayerController { Name = "Player", Gravity = 0f };
    _player.GlobalPosition = new Vector3(0f, 2f, 0f);
    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    if (_builder != null && GodotObject.IsInstanceValid(_builder) && _builder.IsInsideTree())
      _builder.GetParent()!.RemoveChild(_builder);

    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    _builder?.Dispose();
    _builder = null!;
    _player?.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

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

  private static StaticBody3D TutorialBody(IslandBuilder builder) =>
    FindDescendants<StaticBody3D>(builder)
      .Single(b => b.Name.ToString().StartsWith("Island_Tutorial", StringComparison.Ordinal));

  [Test]
  public async Task GenerateLeavesTutorialIslandUnbuilt()
  {
    _builder = new IslandBuilder { WorldSeed = 12345 };
    await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

    _builder.TutorialIslandReady.ShouldBeFalse();
    FindDescendants<StaticBody3D>(_builder)
      .Any(b => b.Name.ToString().StartsWith("Island_Tutorial", StringComparison.Ordinal))
      .ShouldBeFalse();
  }

  [Test]
  public async Task TutorialIslandHasChapterOneResources()
  {
    _builder = new IslandBuilder { WorldSeed = 12345 };
    await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);
    _builder.EnsureTutorialIsland();

    _builder.TutorialIslandReady.ShouldBeTrue();
    var island = TutorialBody(_builder);
    var spec = _builder.TutorialSpec!;
    island.Position.X.ShouldBe(spec.Center.X, 0.001);
    island.Position.Z.ShouldBe(spec.Center.Y, 0.001);

    var trees = FindDescendants<WoodTree>(island);
    trees.Count.ShouldBe(IslandBuilder.TutorialTreeCount);
    int wood = trees.Sum(t => t.MaxHarvests);

    var campfire = GD.Load<BuildableResource>("res://assets/buildables/campfire.tres")!;
    var bed = GD.Load<BuildableResource>("res://assets/buildables/bed.tres")!;
    campfire.CostItemId.ShouldBe("wood");
    bed.CostItemId.ShouldBe("wood");
    wood.ShouldBeGreaterThanOrEqualTo(campfire.CostAmount + bed.CostAmount);

    var palms = FindDescendants<CoconutPalm>(island);
    palms.Count.ShouldBe(IslandBuilder.TutorialPalmCount);
    palms.Sum(p => p.MaxHarvests).ShouldBeGreaterThanOrEqualTo(1);

    var grass = FindDescendants<VegetationProp>(island)
      .Where(p => p.Name.ToString().StartsWith("Grass_", StringComparison.Ordinal))
      .ToList();
    grass.Count.ShouldBeGreaterThanOrEqualTo(20);
    grass.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialGrassCount);

    var shrubs = FindDescendants<VegetationProp>(island)
      .Where(p => p.Name.ToString().StartsWith("Shrub_", StringComparison.Ordinal))
      .ToList();
    shrubs.Count.ShouldBeGreaterThanOrEqualTo(6);
    shrubs.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialShrubCount);

    var rocks = FindDescendants<VegetationProp>(island)
      .Where(p => p.Name.ToString().StartsWith("Rock_", StringComparison.Ordinal))
      .ToList();
    rocks.Count.ShouldBeGreaterThanOrEqualTo(2);
    rocks.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialRockCount);
    foreach (var rock in rocks)
    {
      float xz = new Vector2(rock.Position.X, rock.Position.Z).Length();
      xz.ShouldBeGreaterThan(IslandHeightmap.BuildingPlateauRadius);
    }

    var decoTrees = FindDescendants<VegetationProp>(island)
      .Where(p => p.Name.ToString().StartsWith("DecoTree_", StringComparison.Ordinal))
      .ToList();
    decoTrees.Count.ShouldBeGreaterThanOrEqualTo(3);
    decoTrees.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialDecoTreeCount);

    var decoPalms = FindDescendants<VegetationProp>(island)
      .Where(p => p.Name.ToString().StartsWith("DecoPalm_", StringComparison.Ordinal))
      .ToList();
    decoPalms.Count.ShouldBeGreaterThanOrEqualTo(2);
    decoPalms.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialDecoPalmCount);

    foreach (var tree in trees.Concat<Node3D>(decoTrees))
    {
      float xz = new Vector2(tree.Position.X, tree.Position.Z).Length();
      xz.ShouldBeLessThan(spec.Radius * EnemyHabitat.BeachMinRadial + 0.5f);
    }

    var crabs = FindDescendants<EnemyBase>(island)
      .Where(e => e.Name.ToString().StartsWith("Enemy_crab_", StringComparison.Ordinal))
      .ToList();
    crabs.Count.ShouldBe(1);
    FindDescendants<EnemyBase>(island)
      .Any(e => e.Name.ToString().StartsWith("Enemy_wolf_", StringComparison.Ordinal)
        || e.Name.ToString().StartsWith("Enemy_boar_", StringComparison.Ordinal))
      .ShouldBeFalse();

    float crabXz = new Vector2(crabs[0].Position.X, crabs[0].Position.Z).Length();
    crabXz.ShouldBeGreaterThan(spec.Radius * 0.50f);

    var chests = FindDescendants<StorageBox>(island)
      .Where(c => c.Name.ToString() == "Landmark_WoodenChest")
      .ToList();
    chests.Count.ShouldBe(1);
    chests[0].ShouldBeOfType<WoodenChest>();
    chests[0].GetInteractionPrompt().ShouldBe("[E] 打开储物箱");
    chests[0].CanInteract().ShouldBeTrue();
    chests[0].Inventory.IsEmpty.ShouldBeFalse();
    chests[0].Inventory.GetSlot(0).Item!.Id.ShouldBe("wood");
    chests[0].Inventory.GetSlot(0).Amount.ShouldBe(3);
    float chestXz = new Vector2(chests[0].Position.X, chests[0].Position.Z).Length();
    chestXz.ShouldBeGreaterThan(IslandHeightmap.BuildingPlateauRadius);
    chestXz.ShouldBeLessThan(spec.Radius * IslandHeightmap.MaskFalloffStart + 1f);

    FindDescendants<VegetationProp>(island)
      .Any(p => p.Name.ToString() == "Landmark_Chest")
      .ShouldBeFalse();

    var map = IslandHeightmap.Generate(spec, 129);
    int mid = 64;
    float plateauH01 = 0.5f + 0.5f / spec.HeightScale;
    map[mid * 129 + mid].ShouldBe(plateauH01, 0.02);

    float cell = spec.Radius * 2f / 128f;
    float half = 64f;
    for (int row = 0; row < 129; row++)
    {
      for (int col = 0; col < 129; col++)
      {
        float lx = (col - half) * cell;
        float lz = (row - half) * cell;
        if (new Vector2(lx, lz).Length() >= IslandHeightmap.BuildingPlateauRadius - 0.5f)
          continue;
        map[row * 129 + col].ShouldBeGreaterThan(0.50f);
      }
    }
  }

  [Test]
  public async Task TutorialSessionTeleportsThenHandsOffToProductSpawn()
  {
    var spawn = new Marker3D
    {
      Name = "PlayerSpawnPoint",
      Position = new Vector3(0f, 1.5f, 0f)
    };
    await _fixture.AddToRoot(spawn, autoRemoveFromRoot: true);

    _builder = new IslandBuilder { WorldSeed = 12345 };
    await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);

    _player.GlobalPosition.X.ShouldBe(0f, 0.01);
    _player.GlobalPosition.Z.ShouldBe(0f, 0.01);

    _builder.StartTutorialSession(_player, spawn);
    var spec = _builder.TutorialSpec!;
    _player.GlobalPosition.X.ShouldBe(spec.Center.X, 0.05);
    _player.GlobalPosition.Z.ShouldBe(spec.Center.Y, 0.05);
    _player.GlobalPosition.Y.ShouldBe(IslandBuilder.TutorialSpawnHeight, 0.05);

    _builder.EndTutorialSession();
    _player.GlobalPosition.X.ShouldBe(0f, 0.05);
    _player.GlobalPosition.Z.ShouldBe(0f, 0.05);
    _player.GlobalPosition.Y.ShouldBe(1.5f, 0.05);
  }

  [Test]
  public async Task SameSeedPlacesTutorialPropsIdentically()
  {
    _builder = new IslandBuilder { WorldSeed = 12345 };
    await _fixture.AddToRoot(_builder, autoRemoveFromRoot: true);
    _builder.EnsureTutorialIsland();
    var first = SnapshotTutorial(_builder);
    _builder.GetParent()!.RemoveChild(_builder);
    _builder.Free();

    _builder = new IslandBuilder { WorldSeed = 12345 };
    _player.GetParent()!.AddChild(_builder);
    _builder.EnsureTutorialIsland();
    SnapshotTutorial(_builder).ShouldBe(first);
  }

  private static List<(string Name, Vector3 Position)> SnapshotTutorial(IslandBuilder builder) =>
    FindDescendants<Node3D>(TutorialBody(builder))
      .Where(n => n.Name.ToString().StartsWith("WoodTree_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("CoconutPalm_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("Enemy_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("DecoTree_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("DecoPalm_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("Grass_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("Shrub_", StringComparison.Ordinal)
        || n.Name.ToString().StartsWith("Rock_", StringComparison.Ordinal)
        || n.Name.ToString() == "Landmark_WoodenChest")
      .Select(n => (n.Name.ToString(), n.Position))
      .OrderBy(t => t.Item1)
      .ToList();
}
