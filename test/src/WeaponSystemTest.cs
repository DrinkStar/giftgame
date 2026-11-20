// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   WeaponSystem dispatch tests (Iter6 plan Decision 13): the ammo-gated
///   attack of the currently selected item is resolved through
///   <see cref="CombatLogic.ResolveAttack"/> against a real InventorySystem
///   instance (spear = throw, bow + arrows = shoot, bow without arrows =
///   weak-melee fallback, axe = melee, non-tool / empty = unarmed melee).
/// </summary>
public class WeaponSystemTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private Node _player = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public WeaponSystemTest(Node testScene) : base(testScene) { }

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

  [Test]
  public void SpearSelected_ResolvesThrow()
  {
    _inventory.SelectedHotbarSlot = 0;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Throw);
  }

  [Test]
  public void BowSelectedWithArrows_ResolvesShoot()
  {
    _inventory.SelectedHotbarSlot = 1;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Shoot);
  }

  [Test]
  public void BowSelectedWithoutArrows_FallsBackToMelee()
  {
    _inventory.RemoveItem("arrow", 10).ShouldBeTrue();
    _inventory.SelectedHotbarSlot = 1;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Melee);
  }

  [Test]
  public void AxeSelected_ResolvesMelee()
  {
    _inventory.SelectedHotbarSlot = 3;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Melee);
  }

  [Test]
  public void NonToolSelected_ResolvesMelee()
  {
    _inventory.SelectedHotbarSlot = 4;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Melee);
  }

  [Test]
  public void EmptyHotbarSlot_ResolvesMelee()
  {
    _inventory.RemoveItem("wood", 5).ShouldBeTrue();
    _inventory.SelectedHotbarSlot = 4;
    _weapon.ResolveCurrentAttack().ShouldBe(AttackType.Melee);
    _weapon.ResolveMeleeDamage().ShouldBe(8f);
  }

  /// <summary>
  ///   Secondary fire with a spear must route through
  ///   <see cref="CombatFeedback"/> as Throw: one RangedAttack carrying the
  ///   launching weapon id, and no melee swing (the tutorial's chop step must
  ///   not advance on a throw).
  /// </summary>
  [Test]
  public void SecondaryWithSpear_RaisesRangedAttackOnly()
  {
    var ranged = new List<string>();
    var swings = new List<string>();
    void OnRanged(string id) => ranged.Add(id);
    void OnSwing(string id) => swings.Add(id);

    GameEvents.RangedAttack += OnRanged;
    GameEvents.AttackSwing += OnSwing;
    try
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      _inventory.SelectedHotbarSlot = 0;

      PressSecondary();

      ranged.ShouldBe(new List<string> { "wooden_spear" });
      swings.ShouldBeEmpty();
      _inventory.GetItemCount("wooden_spear").ShouldBe(1);
    }
    finally
    {
      GameEvents.RangedAttack -= OnRanged;
      GameEvents.AttackSwing -= OnSwing;
    }
  }

  /// <summary>
  ///   Secondary fire with a bow must raise RangedAttack with the bow id and
  ///   consume exactly one arrow (Throw and Shoot are the same seam but a
  ///   different resolution path).
  /// </summary>
  [Test]
  public void SecondaryWithBow_RaisesRangedAttackAndConsumesArrow()
  {
    var ranged = new List<string>();
    var swings = new List<string>();
    void OnRanged(string id) => ranged.Add(id);
    void OnSwing(string id) => swings.Add(id);

    GameEvents.RangedAttack += OnRanged;
    GameEvents.AttackSwing += OnSwing;
    try
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      _inventory.SelectedHotbarSlot = 1;
      var arrowsBefore = _inventory.GetItemCount("arrow");

      PressSecondary();

      ranged.ShouldBe(new List<string> { "wooden_bow" });
      swings.ShouldBeEmpty();
      _inventory.GetItemCount("arrow").ShouldBe(arrowsBefore - 1);
    }
    finally
    {
      GameEvents.RangedAttack -= OnRanged;
      GameEvents.AttackSwing -= OnSwing;
    }
  }

  /// <summary>
  ///   Primary fire with an axe must stay on the melee side of the seam:
  ///   AttackSwing only, never RangedAttack.
  /// </summary>
  [Test]
  public void PrimaryWithAxe_RaisesMeleeSwingOnly()
  {
    var ranged = new List<string>();
    var swings = new List<string>();
    void OnRanged(string id) => ranged.Add(id);
    void OnSwing(string id) => swings.Add(id);

    GameEvents.RangedAttack += OnRanged;
    GameEvents.AttackSwing += OnSwing;
    try
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      _inventory.SelectedHotbarSlot = 3;

      PressPrimary();

      swings.ShouldBe(new List<string> { "stone_axe" });
      ranged.ShouldBeEmpty();
    }
    finally
    {
      GameEvents.RangedAttack -= OnRanged;
      GameEvents.AttackSwing -= OnSwing;
    }
  }

  private void PressPrimary() =>
    _weapon._UnhandledInput(new InputEventAction
    {
      Action = WeaponSystem.AttackAction,
      Pressed = true
    });

  private void PressSecondary() =>
    _weapon._UnhandledInput(new InputEventAction
    {
      Action = WeaponSystem.SecondaryAttackAction,
      Pressed = true
    });
}
