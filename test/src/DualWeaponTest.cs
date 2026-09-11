// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Dual weapon slot tests (Iter6.1 todo 5 / Decision D3): the secondary
///   slot on <see cref="InventorySystem.SecondaryItem"/> drives the attack
///   when active — spear throws, bow + arrows shoots, empty falls back to
///   the hotbar, non-weapons never attack, and every slot change publishes
///   through the <see cref="GameEvents"/> bus. Resolution is exercised via
///   the pure <see cref="CombatLogic.EffectiveItem"/> seam plus
///   <see cref="WeaponSystem.ResolveCurrentAttack"/> against a real
///   InventorySystem instance.
/// </summary>
public class DualWeaponTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private Node _player = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public DualWeaponTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Starting items fill the 5 hotbar slots in order: spear(0), bow(1),
    // arrow(2), axe(3), wood(4).
    _inventory = new InventorySystem
    {
      Name = "InventorySystem",
      StartingItems = new Godot.Collections.Array<Godot.Collections.Dictionary>
      {
        BuildEntry("wooden_spear", 1),
        BuildEntry("wooden_bow", 1),
        BuildEntry("arrow", 10),
        BuildEntry("stone_axe", 1),
        BuildEntry("wood", 5)
      }
    };

    _player = new Node { Name = "Player" };
    _player.AddChild(_inventory);
    _weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem"
    };
    _player.AddChild(_weapon);

    _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
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

  private static Godot.Collections.Dictionary BuildEntry(string id, int amount) =>
    new()
    {
      ["item"] = GD.Load<ItemData>($"res://assets/items/{id}.tres"),
      ["amount"] = amount
    };

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  /// <summary>
  ///   EffectiveItem is the pure dual-slot seam: an active secondary slot
  ///   only wins when it actually holds an item.
  /// </summary>
  [Test]
  public void EffectiveItem_EmptySecondaryFallsBackToPrimary()
  {
    var spear = LoadItem("wooden_spear");

    CombatLogic
      .EffectiveItem(spear, null, secondarySlotActive: true)
      .ShouldBe(spear);
    CombatLogic
      .EffectiveItem(spear, null, secondarySlotActive: false)
      .ShouldBe(spear);

    var bow = LoadItem("wooden_bow");
    CombatLogic.EffectiveItem(spear, bow, secondarySlotActive: true).ShouldBe(bow);
    CombatLogic.EffectiveItem(spear, bow, secondarySlotActive: false).ShouldBe(spear);
  }

  [Test]
  public void SecondarySpearEquipped_ResolvesThrow()
  {
    var spear = LoadItem("wooden_spear");
    _inventory.SecondaryItem = spear;
    _weapon.SecondarySlotActive = true;

    _weapon.ResolveEffectiveItem().ShouldBe(spear);
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Throw);
  }

  [Test]
  public void SecondaryBowWithArrows_ResolvesShoot()
  {
    _inventory.SecondaryItem = LoadItem("wooden_bow");
    _weapon.SecondarySlotActive = true;

    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Shoot);
  }

  [Test]
  public void SecondaryEmpty_FallsBackToPrimary()
  {
    // Primary hotbar slot 0 holds the spear; the empty secondary must not
    // shadow it even while the secondary slot is the active one.
    _inventory.SelectedHotbarSlot = 0;
    _inventory.SecondaryItem.ShouldBeNull();
    _weapon.SecondarySlotActive = true;

    _weapon.ResolveEffectiveItem()?.Id.ShouldBe("wooden_spear");
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Throw);
  }

  [Test]
  public void SlotToggle_RaisesWeaponSlotChanged()
  {
    var spear = LoadItem("wooden_spear");
    _inventory.SecondaryItem = spear;
    _inventory.SelectedHotbarSlot = 0;

    var raised = 0;
    ItemData? primary = null;
    ItemData? secondary = null;
    void Handler(ItemData? p, ItemData? s)
    {
      raised++;
      primary = p;
      secondary = s;
    }

    GameEvents.WeaponSlotChanged += Handler;
    try
    {
      _weapon.ToggleWeaponSlot();
      _weapon.SecondarySlotActive.ShouldBeTrue();
      raised.ShouldBe(1);
      primary.ShouldBe(_inventory.SelectedItem);
      secondary.ShouldBe(spear);

      _weapon.ToggleWeaponSlot();
      _weapon.SecondarySlotActive.ShouldBeFalse();
      raised.ShouldBe(2);
    }
    finally
    {
      GameEvents.WeaponSlotChanged -= Handler;
    }
  }

  [Test]
  public void SettingSecondaryItem_RaisesSecondarySlotChanged()
  {
    var spear = LoadItem("wooden_spear");
    var raised = 0;
    ItemData? received = null;
    void Handler(ItemData? item)
    {
      raised++;
      received = item;
    }

    GameEvents.SecondarySlotChanged += Handler;
    try
    {
      _inventory.SecondaryItem = spear;
      raised.ShouldBe(1);
      received.ShouldBe(spear);
      _inventory.SecondarySlot.Item.ShouldBe(spear);
      _inventory.SecondarySlot.Amount.ShouldBe(1);

      _inventory.SecondaryItem = null;
      raised.ShouldBe(2);
      received.ShouldBeNull();
      _inventory.SecondarySlot.IsEmpty.ShouldBeTrue();
    }
    finally
    {
      GameEvents.SecondarySlotChanged -= Handler;
    }
  }

  [Test]
  public void NonWeaponSecondary_ResolvesMelee()
  {
    _inventory.SecondaryItem = LoadItem("wood");
    _weapon.SecondarySlotActive = true;

    _weapon.ResolveEffectiveItem()?.Id.ShouldBe("wood");
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Melee);
  }
}
