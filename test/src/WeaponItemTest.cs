// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   T8.5.4 (iter8.5 plan): the new tool/weapon/armor/backpack mechanics.
///   - Throwing a spear no longer consumes it (Task A: the pre-T8.5.4 behavior
///     removed one spear per throw).
///   - Bows still consume arrows (unchanged behavior, regression-guarded).
///   - Armor equips with F and reduces incoming damage in PlayerStats.
///   - The fishing rod triggers a guide line and is never consumed (the catch
///     itself is a GD.Randf chance, so it is not asserted deterministically).
///   - The backpack widens the inventory grid exactly once without being
///     consumed.
///   Fixture mirrors test/src/FoodUseTest.cs: PlayerController + PlayerStats +
///   InventorySystem + WeaponSystem under a GoDotTest Fixture.
/// </summary>
public class WeaponItemTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public WeaponItemTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player" };
    _stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(_stats);
    _player.Stats = _stats;

    // Empty inventory: each test adds exactly the items it needs.
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);

    _weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem"
    };
    _player.AddChild(_weapon);

    // Defensive: CraftUILogicTest toggles the static gameplay input lock;
    // this fixture expects unlocked input so the use/secondary branches run.
    GameEvents.RaiseGameplayInputLockChanged(false);

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

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  private void PressSecondary() =>
    _weapon._UnhandledInput(
      new InputEventAction
      {
        Action = WeaponSystem.SecondaryAttackAction,
        Pressed = true
      }
    );

  private void PressUseItem() =>
    _weapon._UnhandledInput(
      new InputEventAction { Action = WeaponSystem.UseItemAction, Pressed = true }
    );

  /// <summary>
  ///   T8.5.4 (Task A): throwing the wooden spear spawns a projectile but the
  ///   spear stays in the inventory — the count must remain 1 (the pre-T8.5.4
  ///   behavior dropped it to 0).
  /// </summary>
  [Test]
  public void ThrowSpearDoesNotConsumeSpear()
  {
    _inventory.AddItem(LoadItem("wooden_spear"), 1);

    PressSecondary();

    _inventory.GetItemCount("wooden_spear").ShouldBe(1);
  }

  /// <summary>
  ///   T8.5.4 (Task A): the bow still consumes exactly one arrow per shot —
  ///   arrow ammo consumption was deliberately left unchanged.
  /// </summary>
  [Test]
  public void BowStillConsumesArrows()
  {
    _inventory.AddItem(LoadItem("wooden_bow"), 1);
    _inventory.AddItem(LoadItem("arrow"), 2);

    PressSecondary();

    _inventory.GetItemCount("arrow").ShouldBe(1);
  }

  /// <summary>
  ///   T8.5.4 (Task C1): without armor a 30-damage hit removes 30 health;
  ///   with ArmorReduction = 10 the same hit removes only 20.
  /// </summary>
  [Test]
  public void EquippingArmorReducesDamage()
  {
    _stats.TakeDamage(30f);
    _stats.Health.ShouldBe(70f);

    _stats.ArmorReduction = 10f;
    _stats.TakeDamage(30f);
    _stats.Health.ShouldBe(50f);
  }

  /// <summary>
  ///   T8.5.4 (Task C1): pressing F with an armor Tool selected copies its
  ///   ArmorReduction onto the stats and does NOT consume the armor.
  /// </summary>
  [Test]
  public void EquippingArmorViaUseItemKeepsArmor()
  {
    _inventory.AddItem(LoadItem("cloth_armor"), 1);

    PressUseItem();

    _stats.ArmorReduction.ShouldBe(5f);
    _inventory.GetItemCount("cloth_armor").ShouldBe(1);
  }

  /// <summary>
  ///   T8.5.4 (Task C2): the fishing rod shows the fishing guide line and is
  ///   never consumed. The catch is a GD.Randf chance, so the deterministic
  ///   assertions are the guide line and the rod count (a caught fish may or
  ///   may not be added). The static event is unsubscribed in finally.
  /// </summary>
  [Test]
  public void FishingRodTriggersGuideLine()
  {
    var lines = new List<string>();
    void OnGuideLine(string text) => lines.Add(text);
    GameEvents.GuideLine += OnGuideLine;
    try
    {
      _inventory.AddItem(LoadItem("fishing_rod"), 1);

      PressUseItem();

      lines.ShouldContain(text => text.Contains("钓鱼"));
      _inventory.GetItemCount("fishing_rod").ShouldBe(1);
    }
    finally
    {
      GameEvents.GuideLine -= OnGuideLine;
    }
  }

  /// <summary>
  ///   T8.5.4 (Task C3): widening the grid to 12 columns updates
  ///   InventoryWidth to 12, marks BackpackExpanded and preserves every old
  ///   slot's content 1:1 (content keeps its position; the new columns are
  ///   empty).
  /// </summary>
  [Test]
  public void BackpackExpandsInventory()
  {
    _inventory.GetInventorySlot(3, 2).Item = LoadItem("wood");
    _inventory.GetInventorySlot(3, 2).Amount = 7;

    _inventory.ExpandInventory(12);

    _inventory.InventoryWidth.ShouldBe(12);
    _inventory.BackpackExpanded.ShouldBeTrue();
    _inventory.GetInventorySlot(3, 2).Item?.Id.ShouldBe("wood");
    _inventory.GetInventorySlot(3, 2).Amount.ShouldBe(7);
    _inventory.GetInventorySlot(11, 3).Item.ShouldBeNull();
  }

  /// <summary>
  ///   T8.5.4 (Task C3): pressing F with the backpack selected expands the
  ///   grid once and does not consume the backpack; a second press is a
  ///   no-op thanks to the BackpackExpanded guard.
  /// </summary>
  [Test]
  public void BackpackUseExpandsGridOnce()
  {
    _inventory.AddItem(LoadItem("backpack"), 1);

    PressUseItem();
    _inventory.InventoryWidth.ShouldBe(12);
    _inventory.BackpackExpanded.ShouldBeTrue();
    _inventory.GetItemCount("backpack").ShouldBe(1);

    PressUseItem();
    _inventory.InventoryWidth.ShouldBe(12);
  }
}
