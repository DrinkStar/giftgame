// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Behavioral tests for <see cref="InventorySystem"/>. The node is added to
///   the tree root so _Ready allocates the grid/hotbar. All assertions run in
///   one synchronous block, so no engine frame can interleave. Hotbar
///   selection is exercised through the SelectedHotbarSlot property setter
///   only — the hotbar_1..5 input actions do not exist until W3 (Decision 11).
/// </summary>
public class InventorySystemTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private InventorySystem _inventory = default!;

  public InventorySystemTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _inventory = new InventorySystem();
    _fixture.AddToRoot(_inventory, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable <see cref="InventorySystem"/> field satisfies CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_inventory == null)
      return;

    _inventory.Dispose();
    _inventory = null!;
    GC.SuppressFinalize(this);
  }

  private static ItemData MakeItem(string id, int maxStack = 64) =>
    new() { Id = id, DisplayName = id, MaxStack = maxStack };

  [Test]
  public void AddItemStacksUpToMaxStackAndSpillsToNextSlot()
  {
    var wood = MakeItem("test_wood", maxStack: 10);

    _inventory.AddItem(wood, 6).ShouldBe(0);
    _inventory.AddItem(wood, 6).ShouldBe(0);

    // 4 stack into slot 0 (hitting MaxStack 10), the remaining 2 spill into
    // an empty slot.
    _inventory.GetItemCount("test_wood").ShouldBe(12);
    _inventory.GetHotbarSlot(0).Amount.ShouldBe(10);
    _inventory.GetHotbarSlot(1).Amount.ShouldBe(2);
  }

  [Test]
  public void AddItemSpillsFromHotbarIntoInventoryGrid()
  {
    var wood = MakeItem("test_wood", maxStack: 10);

    // 60 items = 6 full stacks: hotbar first (5), then the grid.
    _inventory.AddItem(wood, 60).ShouldBe(0);

    _inventory.GetItemCount("test_wood").ShouldBe(60);
    _inventory.GetHotbarSlot(4).Amount.ShouldBe(10);
    _inventory.GetInventorySlot(0, 0).Amount.ShouldBe(10);
  }

  [Test]
  public void AddItemPartialAddReturnsRemainingAndEmitsPlacedPortion()
  {
    var wood = MakeItem("test_wood", maxStack: 10);

    var added = 0;
    Action<string, int> onAdded = (id, amount) =>
    {
      if (id == "test_wood")
        added += amount;
    };

    GameEvents.ItemAdded += onAdded;
    try
    {
      // 45 slots (5 hotbar + 40 grid) x MaxStack 10 = 450 capacity.
      _inventory.AddItem(wood, 460).ShouldBe(10);
    }
    finally
    {
      GameEvents.ItemAdded -= onAdded;
    }

    _inventory.GetItemCount("test_wood").ShouldBe(450);
    added.ShouldBe(450);
  }

  [Test]
  public void RemoveItemDecrementsExactlyAndClearsEmptySlots()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    _inventory.AddItem(wood, 5);

    _inventory.RemoveItem("test_wood", 3).ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(2);

    _inventory.RemoveItem("test_wood", 2).ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(0);
    _inventory.GetHotbarSlot(0).IsEmpty.ShouldBeTrue();
  }

  [Test]
  public void RemoveItemReturnsFalseWhenInsufficient()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    _inventory.AddItem(wood, 5);

    // Upstream semantics: RemoveItem takes everything it can find and
    // reports false when the full request cannot be satisfied.
    _inventory.RemoveItem("test_wood", 10).ShouldBeFalse();
    _inventory.GetItemCount("test_wood").ShouldBe(0);
  }

  [Test]
  public void HasItemAndGetItemCountReflectContents()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    _inventory.AddItem(wood, 3);

    _inventory.GetItemCount("test_wood").ShouldBe(3);
    _inventory.HasItem("test_wood", 3).ShouldBeTrue();
    _inventory.HasItem("test_wood", 4).ShouldBeFalse();
    _inventory.HasItem("other").ShouldBeFalse();
  }

  [Test]
  public void SelectedHotbarSlotSetterEmitsEventAndClamps()
  {
    int? selected = null;
    Action<int> onSelected = slot => selected = slot;

    GameEvents.HotbarSelectionChanged += onSelected;
    try
    {
      _inventory.SelectedHotbarSlot = 2;
      _inventory.SelectedHotbarSlot.ShouldBe(2);
      selected.ShouldBe(2);

      _inventory.SelectedHotbarSlot = 99;
      _inventory.SelectedHotbarSlot.ShouldBe(4);
      selected.ShouldBe(4);

      _inventory.SelectedHotbarSlot = -5;
      _inventory.SelectedHotbarSlot.ShouldBe(0);
      selected.ShouldBe(0);
    }
    finally
    {
      GameEvents.HotbarSelectionChanged -= onSelected;
    }
  }

  [Test]
  public void SelectedItemTracksSelectedHotbarSlot()
  {
    var wood = MakeItem("test_wood", maxStack: 10);

    // Empty inventory: the item lands in hotbar slot 0, which is selected.
    _inventory.AddItem(wood, 3);

    _inventory.SelectedItem.ShouldNotBeNull();
    _inventory.SelectedItem!.Id.ShouldBe("test_wood");

    _inventory.SelectedHotbarSlot = 1;
    _inventory.SelectedItem.ShouldBeNull();
  }

  [Test]
  public void SwapSlotsSwapsItemAndAmount()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    var stone = MakeItem("test_stone", maxStack: 10);

    _inventory.AddItem(wood, 3);
    _inventory.AddItem(stone, 5);

    _inventory.SwapSlots(
      _inventory.GetHotbarSlot(0),
      _inventory.GetHotbarSlot(1)
    );

    _inventory.GetHotbarSlot(0).Item!.Id.ShouldBe("test_stone");
    _inventory.GetHotbarSlot(0).Amount.ShouldBe(5);
    _inventory.GetHotbarSlot(1).Item!.Id.ShouldBe("test_wood");
    _inventory.GetHotbarSlot(1).Amount.ShouldBe(3);
  }

  [Test]
  public void StartingItemsDictionaryContractPopulatesInventory()
  {
    // Decision 10: StartingItems entries use the {"item", "amount"} keys.
    var wood = MakeItem("test_wood", maxStack: 10);
    var starting = new Godot.Collections.Dictionary
    {
      ["item"] = wood,
      ["amount"] = 3
    };

    var inv = new InventorySystem
    {
      StartingItems = new Godot.Collections.Array<Godot.Collections.Dictionary>
      {
        starting
      }
    };

    try
    {
      inv._Ready();
      inv.GetItemCount("test_wood").ShouldBe(3);
    }
    finally
    {
      inv.Dispose();
    }
  }
}
