// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8.5.8 storage box tests: the lightweight 20-slot
///   <see cref="StorageInventory"/> round trip (stacking up to MaxStack,
///   partial/full removal), the StorageUI whole-stack transfer logic in both
///   directions (with the remainder rolled back when the destination fills),
///   the modal contract (input lock + mutual-exclusion gate + Esc close),
///   and the SaveService StorageBoxes snapshot / position-matched restore.
///   Nodes live in the tree root (Fixture) like the other UI suites.
/// </summary>
public class StorageBoxTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;

  public StorageBoxTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes (see
    // QuestServiceTest for the rationale — leaked nodes keep their
    // subscriptions on the static bus alive across test classes).
    SweepLeakedNodes(TestScene.GetTree().Root);
  }

  [Cleanup]
  public void Cleanup()
  {
    // Static lock hygiene (plan Decision 13): never leak a held
    // gameplay-input lock into the next test case.
    GameEvents.RaiseGameplayInputLockChanged(false);
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    GC.SuppressFinalize(this);
  }

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  /// <summary>Builds a bare player controller with an InventorySystem child.</summary>
  private async Task<PlayerController> BuildPlayer()
  {
    var player = new PlayerController { Name = "Player" };
    var inventory = new InventorySystem { Name = "InventorySystem" };
    player.AddChild(inventory);
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);
    return player;
  }

  private static int TotalWood(StorageBox box)
  {
    var total = 0;
    for (var i = 0; i < StorageBox.SlotCount; i++)
    {
      var slot = box.Inventory.GetSlot(i);
      if (slot.Item?.Id == "wood")
        total += slot.Amount;
    }

    return total;
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
  ///   ① StorageInventory round trip: stacking up to MaxStack (wood = 20),
  ///   slot order, partial removal draining the first slot, and a removal
  ///   larger than the present amount returning false after draining.
  /// </summary>
  [Test]
  public async Task AddRemoveRoundTrip()
  {
    var box = new StorageBox();
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);

    // The box group-registers in _Ready (SaveService collection hook).
    box.IsInGroup("storage_boxes").ShouldBeTrue();

    box.Inventory.IsEmpty.ShouldBeTrue();
    box.Inventory.GetSlot(0).IsEmpty.ShouldBeTrue();

    box.Inventory.AddItem(LoadItem("wood"), 10).ShouldBe(0);
    box.Inventory.IsEmpty.ShouldBeFalse();
    box.Inventory.GetSlot(0).Item?.Id.ShouldBe("wood");
    box.Inventory.GetSlot(0).Amount.ShouldBe(10);

    // Stacking up to MaxStack (20); the overflow lands in slot 1.
    box.Inventory.AddItem(LoadItem("wood"), 15).ShouldBe(0);
    box.Inventory.GetSlot(0).Amount.ShouldBe(20);
    box.Inventory.GetSlot(1).Amount.ShouldBe(5);

    // Removing 15 drains slot 0 first, leaving both partially filled.
    box.Inventory.RemoveItem("wood", 15).ShouldBeTrue();
    box.Inventory.GetSlot(0).Amount.ShouldBe(5);
    box.Inventory.GetSlot(1).Amount.ShouldBe(5);

    // Removing more than present drains everything and returns false.
    box.Inventory.RemoveItem("wood", 30).ShouldBeFalse();
    box.Inventory.IsEmpty.ShouldBeTrue();

    // An unknown id removes nothing and fails.
    box.Inventory.RemoveItem("stone", 1).ShouldBeFalse();
    box.Inventory.IsEmpty.ShouldBeTrue();
  }

  /// <summary>
  ///   ②a Whole-stack transfer player → chest → player: the full stack moves
  ///   each way and the UI opens/closes with the input lock.
  /// </summary>
  [Test]
  public async Task WholeStackTransferMovesAndReturns()
  {
    var player = await BuildPlayer();
    var inventory = player.GetNode<InventorySystem>("InventorySystem");
    var box = new StorageBox();
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);

    // Seed the player's GRID slot (0,0) directly (row-major index 0).
    inventory.GetInventorySlot(0, 0).Item = LoadItem("wood");
    inventory.GetInventorySlot(0, 0).Amount = 10;

    var ui = new StorageUI();
    await _fixture.AddToRoot(ui, autoRemoveFromRoot: true);
    ui.Open(box, inventory);

    ui.IsOpen.ShouldBeTrue();
    ui.Visible.ShouldBeTrue();
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    // Player grid slot 0 → chest slot 0: the whole stack moves.
    ui.TransferPlayerToChest(0);
    inventory.GetItemCount("wood").ShouldBe(0);
    box.Inventory.GetSlot(0).Item?.Id.ShouldBe("wood");
    box.Inventory.GetSlot(0).Amount.ShouldBe(10);

    // And back: chest → player (AddItem fills the hotbar first, so the total
    // count is the stable assertion).
    ui.TransferChestToPlayer(0);
    box.Inventory.IsEmpty.ShouldBeTrue();
    inventory.GetItemCount("wood").ShouldBe(10);

    // Esc closes and releases the lock (deferred to the next frame — Esc in
    // the same input pass must not let GameManager pause).
    ui._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
    ui.IsOpen.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }

  /// <summary>
  ///   ②b Full-chest rollback: with every chest slot full, a player transfer
  ///   cannot place anything — the whole stack is rolled back into the freed
  ///   player slot (nothing lost, nothing partially placed).
  /// </summary>
  [Test]
  public async Task FullChestTransferRollsRemainderBack()
  {
    var player = await BuildPlayer();
    var inventory = player.GetNode<InventorySystem>("InventorySystem");
    var box = new StorageBox();
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);

    // Fill every chest slot to MaxStack (wood MaxStack = 20).
    for (var i = 0; i < StorageBox.SlotCount; i++)
      box.Inventory.AddItem(LoadItem("wood"), 20);

    TotalWood(box).ShouldBe(20 * StorageBox.SlotCount);

    inventory.GetInventorySlot(0, 0).Item = LoadItem("wood");
    inventory.GetInventorySlot(0, 0).Amount = 20;

    var ui = new StorageUI();
    await _fixture.AddToRoot(ui, autoRemoveFromRoot: true);
    ui.Open(box, inventory);

    ui.TransferPlayerToChest(0);

    // The chest could not take the stack: it was rolled back into the player
    // inventory (hotbar first) — nothing lost, nothing partially placed.
    inventory.GetItemCount("wood").ShouldBe(20);
    TotalWood(box).ShouldBe(20 * StorageBox.SlotCount);
  }

  /// <summary>
  ///   Modal contract: opening is blocked while ANOTHER modal holds the
  ///   gameplay-input lock (e.g. the forced tutorial) and works again once
  ///   released; Esc closes and releases the lock.
  /// </summary>
  [Test]
  public async Task OpenBlockedWhileAnotherModalHoldsLock()
  {
    var player = await BuildPlayer();
    var inventory = player.GetNode<InventorySystem>("InventorySystem");
    var box = new StorageBox();
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);
    var ui = new StorageUI();
    await _fixture.AddToRoot(ui, autoRemoveFromRoot: true);

    GameEvents.RaiseGameplayInputLockChanged(true);
    try
    {
      ui.Open(box, inventory);
      ui.IsOpen.ShouldBeFalse();
    }
    finally
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
    }

    ui.Open(box, inventory);
    ui.IsOpen.ShouldBeTrue();
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    ui._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
    ui.IsOpen.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }

  /// <summary>
  ///   ③ SaveService snapshot contains the placed storage box (position-keyed,
  ///   positional 20-slot list) and LoadGame re-attaches it by position
  ///   matching — the FarmSaveData pattern.
  /// </summary>
  [Test]
  public async Task SnapshotContainsStorageBoxesAndRestoresByPosition()
  {
    var box = new StorageBox { Name = "StorageBox" };
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);
    box.Inventory.AddItem(LoadItem("wood"), 7);
    box.Inventory.AddItem(LoadItem("stone"), 3);

    var service = new SaveService();
    await _fixture.AddToRoot(service, autoRemoveFromRoot: true);

    var snapshot = service.Snapshot();
    snapshot.StorageBoxes.Count.ShouldBe(1);

    var saved = snapshot.StorageBoxes[0];
    saved.PositionX.ShouldBe(0f);
    saved.PositionZ.ShouldBe(0f);
    saved.Slots.Count.ShouldBe(StorageBox.SlotCount);
    saved.Slots[0].ItemId.ShouldBe("wood");
    saved.Slots[0].Amount.ShouldBe(7);
    saved.Slots.ShouldContain(s => s.ItemId == "stone" && s.Amount == 3);

    // Reset the same instance, then prove LoadGame re-attaches the saved
    // contents by position (the box stays at the origin).
    box.Inventory.Clear();

    var service2 = new SaveService();
    await _fixture.AddToRoot(service2, autoRemoveFromRoot: true);
    service2.LoadGame(snapshot).ShouldBeTrue();

    box.Inventory.GetSlot(0).Item?.Id.ShouldBe("wood");
    box.Inventory.GetSlot(0).Amount.ShouldBe(7);
    box.Inventory.GetSlot(1).Item?.Id.ShouldBe("stone");
    box.Inventory.GetSlot(1).Amount.ShouldBe(3);
  }
}
