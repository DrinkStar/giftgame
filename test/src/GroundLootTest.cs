// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8p.3 (iter8p-plan Decision 5): ground loot. The static
///   <see cref="GroundLoot.Spawn"/> factory instantiates the loot scene at a
///   world position, interacting with it moves its items into the player's
///   inventory and frees it, and the death drop in GameManager drops only
///   non-tool stacks while tools stay in the inventory. An empty inventory
///   drops nothing (and crashes nothing).
/// </summary>
public class GroundLootTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private GameManager _manager = default!;

  public GroundLootTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes (see
    // QuestServiceTest for the rationale — leaked Game scenes keep their
    // GameManager subscribed to the static PlayerDied bus).
    SweepLeakedNodes(TestScene.GetTree().Root);
  }

  [Cleanup]
  public void Cleanup()
  {
    if (_manager != null && _manager.IsInsideTree())
      _manager.GetParent()!.RemoveChild(_manager);
    if (_player != null && _player.IsInsideTree())
      _player.GetParent()!.RemoveChild(_player);

    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    _manager?.Dispose();
    _manager = null!;

    if (_player == null)
      return;

    _player.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///   Removes and frees leftover nodes from earlier test classes (same
  ///   contract as QuestServiceTest.SweepLeakedNodes): everything except the
  ///   test runner scene and the current scene is test garbage.
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

  private static Godot.Collections.Dictionary BuildEntry(string id, int amount) =>
    new()
    {
      ["item"] = GD.Load<ItemData>($"res://assets/items/{id}.tres"),
      ["amount"] = amount
    };

  /// <summary>
  ///   Builds the test player (controller + stats + inventory) and adds it
  ///   and the death-drop GameManager to the root.
  /// </summary>
  private async Task<InventorySystem> BuildPlayerWithItems(
    params (string Id, int Amount)[] items
  )
  {
    _player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(stats);
    _player.Stats = stats;

    var inventory = new InventorySystem
    {
      Name = "InventorySystem",
      StartingItems = new Godot.Collections.Array<Godot.Collections.Dictionary>(
        items.Select(i => BuildEntry(i.Id, i.Amount))
      )
    };
    _player.AddChild(inventory);

    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);

    _manager = new GameManager { Player = _player };
    await _fixture.AddToRoot(_manager, autoRemoveFromRoot: true);

    return inventory;
  }

  private static List<GroundLoot> GroundLootInScene(Node scene) =>
    scene.GetChildren().OfType<GroundLoot>().ToList();

  /// <summary>
  ///   T8p.3 (a): Spawn creates a live loot node under the current scene at
  ///   the requested world position with the requested item and amount.
  /// </summary>
  [Test]
  public void SpawnCreatesNodeAtPosition()
  {
    var scene = TestScene.GetTree().CurrentScene;

    var loot = GroundLoot.Spawn("wood", 3, new Vector3(5, 0.5f, -3));
    try
    {
      loot = loot.ShouldNotBeNull<GroundLoot>();
      loot.IsInsideTree().ShouldBeTrue();
      loot.GetParent().ShouldBeSameAs(scene);
      loot.ItemId.ShouldBe("wood");
      loot.Amount.ShouldBe(3);

      loot.GlobalPosition.X.ShouldBe(5f, 0.001);
      loot.GlobalPosition.Y.ShouldBe(0.5f, 0.001);
      loot.GlobalPosition.Z.ShouldBe(-3f, 0.001);
    }
    finally
    {
      loot?.QueueFree();
    }
  }

  /// <summary>
  ///   T8p.3 (b): interacting with loot moves its items into the player's
  ///   inventory and queues the node for deletion.
  /// </summary>
  [Test]
  public async Task InteractAddsItemsAndFrees()
  {
    var inventory = await BuildPlayerWithItems();

    var loot = new GroundLoot { Name = "Loot", ItemId = "wood", Amount = 2 };
    await _fixture.AddToRoot(loot, autoRemoveFromRoot: true);

    loot.GetInteractionPrompt().ShouldBe("[E] 拾取 木头");

    loot.Interact(_player);

    inventory.GetItemCount("wood").ShouldBe(2);
    loot.IsQueuedForDeletion().ShouldBeTrue();
  }

  /// <summary>
  ///   T8p.3 (c): dying drops only non-tool stacks at the death position
  ///   (half of the stack per drop, 1-3 drops), while tools stay in the
  ///   inventory. Loot attribution is position-filtered so leaked scene
  ///   drops from earlier suites cannot pollute the count.
  /// </summary>
  [Test]
  public async Task DeathDropDropsOnlyNonToolStacksAndKeepsTools()
  {
    var scene = TestScene.GetTree().CurrentScene;
    var before = GroundLootInScene(scene).ToHashSet();

    var inventory = await BuildPlayerWithItems(("stone_axe", 1), ("wood", 10));
    var deathPos = new Vector3(20, 1, 20);
    _player.GlobalPosition = deathPos;

    _player.GetNode<PlayerStats>("PlayerStats").TakeDamage(1000f);
    await TestScene.ProcessFrame(2);

    // The respawn chain ran (player revived), tools never left the inventory.
    _player.GetNode<PlayerStats>("PlayerStats").IsAlive.ShouldBeTrue();
    inventory.GetItemCount("stone_axe").ShouldBe(1);

    // Only wood could drop; the total dropped amount matches the loss.
    var woodRemaining = inventory.GetItemCount("wood");
    woodRemaining.ShouldBeLessThan(10);

    var spawned = GroundLootInScene(scene)
      .Where(l => !before.Contains(l))
      .Where(l =>
        new Vector2(l.GlobalPosition.X, l.GlobalPosition.Z)
          .DistanceTo(new Vector2(deathPos.X, deathPos.Z)) <= 3f
      )
      .ToList();

    spawned.Count.ShouldBeGreaterThanOrEqualTo(1);
    spawned.ShouldAllBe(l => l.ItemId == "wood");
    spawned.Sum(l => l.Amount).ShouldBe(10 - woodRemaining);
  }

  /// <summary>
  ///   T8p.3 (d): an empty inventory drops nothing and the respawn chain
  ///   still completes without crashing.
  /// </summary>
  [Test]
  public async Task EmptyInventoryDropsNothing()
  {
    var scene = TestScene.GetTree().CurrentScene;
    var before = GroundLootInScene(scene).ToHashSet();

    await BuildPlayerWithItems();
    var deathPos = new Vector3(-15, 1, -15);
    _player.GlobalPosition = deathPos;

    _player.GetNode<PlayerStats>("PlayerStats").TakeDamage(1000f);
    await TestScene.ProcessFrame(2);

    _player.GetNode<PlayerStats>("PlayerStats").IsAlive.ShouldBeTrue();

    var spawned = GroundLootInScene(scene)
      .Where(l => !before.Contains(l))
      .Where(l =>
        new Vector2(l.GlobalPosition.X, l.GlobalPosition.Z)
          .DistanceTo(new Vector2(deathPos.X, deathPos.Z)) <= 3f
      )
      .ToList();

    spawned.ShouldBeEmpty();
  }
}
