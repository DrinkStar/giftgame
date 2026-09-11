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
///   Integration test for the 3D game scene (src/Game.tscn): the scene must
///   load cleanly and contain the player node used by the controller cluster.
/// </summary>
public class GameTest : TestClass
{
  private Game _game = default!;
  private Fixture _fixture = default!;

  public GameTest(Node testScene) : base(testScene) { }

  [SetupAll]
  public async Task Setup()
  {
    // Drain finalizers from earlier suites before the heavy Game.tscn load;
    // otherwise Godot can fatal on already-released GCHandles.
    GC.Collect();
    GC.WaitForPendingFinalizers();

    _fixture = new Fixture(TestScene.GetTree());
    SweepLeakedNodes(TestScene.GetTree().Root);
    _game = await _fixture.LoadAndAddScene<Game>();
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

  [CleanupAll]
  public void Cleanup() => _fixture.Cleanup();

  [Test]
  public void GameSceneLoadsWithRootNode()
  {
    _game.ShouldNotBeNull();
    _game.ShouldBeOfType<Game>();
    _game.ShouldBeAssignableTo<Node3D>();
  }

  [Test]
  public void PlayerNodeExists()
  {
    var player = _game.GetNode("Player");
    player.ShouldNotBeNull();
    player.ShouldBeAssignableTo<CharacterBody3D>();

    var hurtbox = player.GetNodeOrNull<Hurtbox>("Hurtbox");
    hurtbox.ShouldNotBeNull();
    hurtbox!.CollisionLayer.ShouldBe(CombatLayers.PlayerHurtboxMask);
    hurtbox.CollisionMask.ShouldBe(0u);
  }

  [Test]
  public void GameSceneHasNoHandPlacedEnemyRoster()
  {
    _game.GetNodeOrNull("Wolf1").ShouldBeNull();
    _game.GetNodeOrNull("Wolf2").ShouldBeNull();
    _game.GetNodeOrNull("Boar1").ShouldBeNull();
    _game.GetNodeOrNull("Boar2").ShouldBeNull();
    _game.GetNodeOrNull("Crab1").ShouldBeNull();

    foreach (var child in _game.GetChildren())
    {
      if (child is not EnemyBase)
        continue;

      var name = child.Name.ToString();
      name.StartsWith("Wolf").ShouldBeFalse();
      name.StartsWith("Boar").ShouldBeFalse();
      name.StartsWith("Crab").ShouldBeFalse();
    }
  }

  [Test]
  public void GeneratedEnemiesRespectSafeRadiusAndHabitats()
  {
    IslandBuilder.SpawnSafeRadius.ShouldBe(40f);

    var builder = _game.GetNode<IslandBuilder>("IslandBuilder");
    builder.ShouldNotBeNull();

    var generated = FindDescendants<EnemyBase>(builder).ToList();
    generated.Count.ShouldBeGreaterThan(0);

    var main = FindDescendants<StaticBody3D>(builder)
      .Single(b => b.Name.ToString().StartsWith("Island_Main"));
    FindDescendants<EnemyBase>(main)
      .Count(e => e.Name.ToString().StartsWith("Enemy_boar_")).ShouldBe(5);
    FindDescendants<EnemyBase>(main)
      .Count(e => e.Name.ToString().StartsWith("Enemy_wolf_")).ShouldBe(5);
    FindDescendants<EnemyBase>(main)
      .Count(e => e.Name.ToString().StartsWith("Enemy_crab_"))
      .ShouldBeGreaterThan(0);

    float beachStart = WorldLayout.MainRadius * 0.62f;

    foreach (var enemy in FindDescendants<EnemyBase>(main))
    {
      float xz = new Vector2(enemy.Position.X, enemy.Position.Z).Length();
      xz.ShouldBeGreaterThanOrEqualTo(IslandBuilder.SpawnSafeRadius - 0.75f);

      var id = enemy.Name.ToString();
      if (id.StartsWith("Enemy_boar_") || id.StartsWith("Enemy_wolf_"))
        xz.ShouldBeLessThan(beachStart + 2f);
    }
  }

  private static List<T> FindDescendants<T>(Node root) where T : Node
  {
    var result = new List<T>();
    Collect(root, result);
    return result;
  }

  private static void Collect<T>(Node node, List<T> into) where T : Node
  {
    if (node is T typed)
      into.Add(typed);
    foreach (Node child in node.GetChildren())
      Collect(child, into);
  }
}
