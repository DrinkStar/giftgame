// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   FIX(iter8-plan): T8.4b — the cooking gate inside WeaponSystem.TryUseItem:
///   Food with RequiresCooking=true (raw_meat) is refused with a GuideLine hint
///   and nothing is consumed; cooked food and drinks keep working. The gate
///   sits INSIDE the Food branch, so the existing T8p.1 behavior is untouched.
/// </summary>
public class FoodGateTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public FoodGateTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player" };
    _stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(_stats);
    _player.Stats = _stats;

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

  /// <summary>T8.4b (a): raw meat is refused — nothing consumed, hint shown.</summary>
  [Test]
  public void RawMeatCannotBeEatenRaw()
  {
    var hint = "";
    void OnGuideLine(string text) => hint = text;
    GameEvents.GuideLine += OnGuideLine;
    try
    {
      _inventory.AddItem(LoadItem("raw_meat"), 2);
      _stats.Hunger = 50f;

      PressUseItem();

      _stats.Hunger.ShouldBe(50f);
      _inventory.GetItemCount("raw_meat").ShouldBe(2);
      hint.ShouldNotBeEmpty();
    }
    finally
    {
      GameEvents.GuideLine -= OnGuideLine;
    }
  }

  /// <summary>T8.4b (b): cooked meat (RequiresCooking=false) can be eaten.</summary>
  [Test]
  public void CookedMeatCanBeEaten()
  {
    _inventory.AddItem(LoadItem("cooked_meat"), 2);
    _stats.Hunger = 50f;

    PressUseItem();

    _stats.Hunger.ShouldBeGreaterThan(50f);
    _inventory.GetItemCount("cooked_meat").ShouldBe(1);
  }

  /// <summary>T8.4b (c): berries (no cooking requirement) still work.</summary>
  [Test]
  public void BerriesStillEatable()
  {
    _inventory.AddItem(LoadItem("berries"), 3);
    _stats.Hunger = 50f;

    PressUseItem();

    _stats.Hunger.ShouldBe(55f);
    _inventory.GetItemCount("berries").ShouldBe(2);
  }
}
