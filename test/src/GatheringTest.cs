// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8p.2 (iter8p-plan Decision 4): harvestable trees. Interacting with a
///   WoodTree adds one wood to the player's inventory per harvest; a
///   CoconutPalm adds one coconut. Each has a finite number of harvests,
///   after which it stops interacting and shrinks away to nothing.
/// </summary>
public class GatheringTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private InventorySystem _inventory = default!;

  public GatheringTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);

    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
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
    if (_player == null)
      return;

    _player.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///   T8p.2 (a): interacting with a WoodTree adds one wood and decrements
  ///   the remaining harvest count.
  /// </summary>
  [Test]
  public async Task WoodTreeInteractAddsWoodAndDecrements()
  {
    var tree = new WoodTree { Name = "Tree", MaxHarvests = 3 };
    await _fixture.AddToRoot(tree, autoRemoveFromRoot: true);

    tree.RemainingHarvests.ShouldBe(3);
    tree.CanInteract().ShouldBeTrue();
    tree.GetInteractionPrompt().ShouldContain("Gather wood");

    tree.Interact(_player);

    _inventory.GetItemCount("wood").ShouldBe(1);
    tree.RemainingHarvests.ShouldBe(2);
    tree.CanInteract().ShouldBeTrue();
  }

  /// <summary>
  ///   T8p.2 (b): after MaxHarvests the tree stops interacting and frees
  ///   itself once the shrink tween finishes.
  /// </summary>
  [Test]
  public async Task WoodTreeExhaustsAndFreesAfterMaxHarvests()
  {
    var tree = new WoodTree { Name = "Tree", MaxHarvests = 3 };
    await _fixture.AddToRoot(tree, autoRemoveFromRoot: true);

    tree.Interact(_player);
    tree.Interact(_player);
    tree.Interact(_player);

    _inventory.GetItemCount("wood").ShouldBe(3);
    tree.RemainingHarvests.ShouldBe(0);
    tree.CanInteract().ShouldBeFalse();

    // Further interactions are no-ops.
    tree.Interact(_player);
    _inventory.GetItemCount("wood").ShouldBe(3);

    // The shrink tween (0.25 s) queues the free after it finishes.
    await TestScene.ProcessFrame(40);
    GodotObject.IsInstanceValid(tree).ShouldBeFalse();
  }

  /// <summary>
  ///   T8p.2 (c): interacting with a CoconutPalm adds one coconut and
  ///   decrements the remaining harvest count.
  /// </summary>
  [Test]
  public async Task CoconutPalmInteractAddsCoconut()
  {
    var palm = new CoconutPalm { Name = "Palm", MaxHarvests = 2 };
    await _fixture.AddToRoot(palm, autoRemoveFromRoot: true);

    palm.RemainingHarvests.ShouldBe(2);
    palm.GetInteractionPrompt().ShouldContain("Gather coconut");

    palm.Interact(_player);

    _inventory.GetItemCount("coconut").ShouldBe(1);
    palm.RemainingHarvests.ShouldBe(1);
    palm.CanInteract().ShouldBeTrue();
  }
}
