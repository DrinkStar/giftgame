// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   T8p.1 (iter8p-plan Decision 3): the <c>use_item</c> input (F) consumes
///   the hotbar's SelectedItem. Food restores hunger (and health when the
///   item has a health restore) and drinks restore thirst; both consume one
///   unit. No item or a non-food/drink selection does nothing, and a dead
///   player cannot use items (the death gate runs first). The
///   RequiresCooking gate is deliberately absent — it lands with T8.4b.
/// </summary>
public class FoodUseTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public FoodUseTest(Node testScene) : base(testScene) { }

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

  private void PressUseItem() =>
    _weapon._UnhandledInput(
      new InputEventAction { Action = WeaponSystem.UseItemAction, Pressed = true }
    );

  /// <summary>
  ///   T8p.1 (a): eating berries (Food) raises hunger by HungerRestore and
  ///   consumes one from the held stack.
  /// </summary>
  [Test]
  public void FoodUseIncreasesHungerAndConsumesItem()
  {
    _inventory.AddItem(LoadItem("berries"), 3);
    _stats.Hunger = 50f;

    PressUseItem();

    _stats.Hunger.ShouldBe(55f);
    _inventory.GetItemCount("berries").ShouldBe(2);
  }

  /// <summary>
  ///   T8p.1 (b): drinking a coconut (Drink) raises thirst by ThirstRestore
  ///   and consumes one from the held stack.
  /// </summary>
  [Test]
  public void DrinkUseIncreasesThirstAndConsumesItem()
  {
    _inventory.AddItem(LoadItem("coconut"), 2);
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Thirst.ShouldBe(75f);
    _inventory.GetItemCount("coconut").ShouldBe(1);
  }

  /// <summary>
  ///   T8p.1 (c): an empty selection changes nothing.
  /// </summary>
  [Test]
  public void EmptySelectionDoesNothing()
  {
    _stats.Hunger = 50f;
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Hunger.ShouldBe(50f);
    _stats.Thirst.ShouldBe(50f);
  }

  /// <summary>
  ///   T8p.1 (c): a non-food/non-drink selection (plain resource) changes
  ///   nothing and consumes nothing.
  /// </summary>
  [Test]
  public void NonFoodSelectionDoesNothing()
  {
    _inventory.AddItem(LoadItem("wood"), 5);
    _stats.Hunger = 50f;

    PressUseItem();

    _stats.Hunger.ShouldBe(50f);
    _inventory.GetItemCount("wood").ShouldBe(5);
  }

  /// <summary>
  ///   T8p.1: a dead player cannot use items — the death gate runs before
  ///   the use-item branch, so nothing is consumed and nothing changes.
  /// </summary>
  [Test]
  public void DeadPlayerCannotUseItem()
  {
    _inventory.AddItem(LoadItem("berries"), 3);
    _stats.Hunger = 50f;

    _stats.TakeDamage(1000f);
    _stats.IsAlive.ShouldBeFalse();

    PressUseItem();

    _stats.Hunger.ShouldBe(50f);
    _inventory.GetItemCount("berries").ShouldBe(3);
  }
}
