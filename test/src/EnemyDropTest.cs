// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   T8.5.7 enemy kill-drop tests: driving an EnemyBase to death (TakeDamage
///   past zero → Die → TryDropLoot) spawns a <see cref="GroundLoot"/> node
///   under the current scene at the enemy's position carrying the
///   DropItemId/DropAmount from the EnemyData resource — instead of the old
///   straight-to-inventory drop. An empty DropItemId spawns nothing.
/// </summary>
public class EnemyDropTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private EnemyBase _enemy = default!;

  public EnemyDropTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes (see
    // QuestServiceTest for the rationale) so leftover ground loot or Game
    // scenes cannot pollute the current-scene assertions.
    SweepLeakedNodes(TestScene.GetTree().Root);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_enemy == null)
      return;

    _enemy.Dispose();
    _enemy = null!;
    GC.SuppressFinalize(this);
  }

  private static EnemyData CreateData(string dropItemId, int dropAmount = 1) =>
    new()
    {
      Id = "crab",
      MaxHealth = 30f,
      Damage = 5f,
      MoveSpeed = 2f,
      AttackRange = 1.5f,
      AttackCooldown = 1f,
      Behavior = EnemyBehavior.MeleeChase,
      Boss = false,
      Scale = 1f,
      DropItemId = dropItemId,
      DropAmount = dropAmount
    };

  /// <summary>
  ///   Finds the first GroundLoot directly under the current scene within 1 m
  ///   (XZ) of <paramref name="position"/> — GroundLoot.Spawn attaches loot
  ///   directly under the current scene at the exact spawn position.
  /// </summary>
  private static GroundLoot? FindGroundLootNear(Vector3 position)
  {
    var scene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
    if (scene == null)
      return null;

    foreach (var node in scene.GetChildren())
    {
      if (
        node is GroundLoot loot
        && new Vector2(loot.GlobalPosition.X, loot.GlobalPosition.Z)
          .DistanceTo(new Vector2(position.X, position.Z)) <= 1f
      )
      {
        return loot;
      }
    }

    return null;
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

  /// <summary>
  ///   A kill drops the configured item as ground loot at the enemy's exact
  ///   position under the current scene (no player or inventory needed).
  /// </summary>
  [Test]
  public async Task KillDropSpawnsGroundLootAtEnemyPosition()
  {
    var dropPos = new Vector3(55f, 1f, 77f);
    _enemy = new EnemyBase
    {
      Name = "TestEnemy",
      EnemyData = CreateData("wood", 3)
    };
    await _fixture.AddToRoot(_enemy, autoRemoveFromRoot: true);
    _enemy.GlobalPosition = dropPos;

    _enemy.TakeDamage(999f);

    var loot = FindGroundLootNear(dropPos);
    loot = loot.ShouldNotBeNull<GroundLoot>();
    loot.ItemId.ShouldBe("wood");
    loot.Amount.ShouldBe(3);
    loot.GetParent().ShouldBeSameAs(TestScene.GetTree().CurrentScene);
    loot.GlobalPosition.X.ShouldBe(dropPos.X, 0.001);
    loot.GlobalPosition.Z.ShouldBe(dropPos.Z, 0.001);

    loot.QueueFree();
  }

  /// <summary>An enemy with an empty DropItemId dies without spawning loot.</summary>
  [Test]
  public async Task KillWithoutDropItemSpawnsNoLoot()
  {
    var dropPos = new Vector3(60f, 1f, 80f);
    _enemy = new EnemyBase
    {
      Name = "TestEnemy",
      EnemyData = CreateData("")
    };
    await _fixture.AddToRoot(_enemy, autoRemoveFromRoot: true);
    _enemy.GlobalPosition = dropPos;

    _enemy.TakeDamage(999f);

    FindGroundLootNear(dropPos).ShouldBeNull();
  }
}
