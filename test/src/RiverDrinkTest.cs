// Original — river-bank drinking (F)
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   River drinking: channel geometry, consumable priority, WeaponSystem F
///   integration. Tests inject <see cref="WeaponSystem.RiverSpecsOverride"/>
///   so they do not generate the full archipelago.
/// </summary>
public class RiverDrinkTest : TestClass, IDisposable
{
  private const float AlongChannel = 0.55f;

  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;

  public RiverDrinkTest(Node testScene) : base(testScene) { }

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

  private static IslandSpec WildSpec(Vector2 center) =>
    new(20260819, center, 40f, 10f, 0.05f, IslandTier.Wild);

  private static IslandSpec MainSpec(Vector2 center) =>
    new(20260819, center, 40f, 10f, 0.05f, IslandTier.Main);

  private static Vector2 WorldFromLocal(IslandSpec spec, Vector2 local) =>
    spec.Center + local;

  /// <summary>Channel centerline and bank are drinkable; sea / plateau are not.</summary>
  [Test]
  public void IsNearRiverChannelMatchesCarveGeometry()
  {
    var spec = WildSpec(new Vector2(120f, -80f));
    var channel = WorldFromLocal(spec, IslandHeightmap.SampleRiverLocal(spec, AlongChannel));
    var bank = WorldFromLocal(
      spec, IslandHeightmap.SampleRiverBankLocal(spec, AlongChannel, 1f));

    IslandHeightmap.IsNearRiverChannel(spec, channel).ShouldBeTrue();
    IslandHeightmap.IsNearRiverChannel(spec, bank).ShouldBeTrue();

    IslandHeightmap.IsNearRiverChannel(
      spec, spec.Center + new Vector2(spec.Radius + 12f, 0f)
    ).ShouldBeFalse();
    IslandHeightmap.IsNearRiverChannel(spec, new Vector2(8000f, 8000f)).ShouldBeFalse();

    var main = MainSpec(new Vector2(40f, 40f));
    IslandHeightmap.IsNearRiverChannel(main, main.Center).ShouldBeFalse();
  }

  [Test]
  public void HasUsableConsumableRecognizesFoodAndDrink()
  {
    RiverDrink.HasUsableConsumable(LoadItem("berries")).ShouldBeTrue();
    RiverDrink.HasUsableConsumable(LoadItem("coconut")).ShouldBeTrue();
    RiverDrink.HasUsableConsumable(LoadItem("wood")).ShouldBeFalse();
    RiverDrink.HasUsableConsumable(LoadItem("wooden_spear")).ShouldBeFalse();
    RiverDrink.HasUsableConsumable(null).ShouldBeFalse();
  }

  [Test]
  public void EmptyHandsAtRiverDrinks()
  {
    PlacePlayerOnRiver();
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Thirst.ShouldBe(75f);
  }

  [Test]
  public void WoodAtRiverDrinksWithoutConsumingWood()
  {
    PlacePlayerOnRiver();
    _inventory.AddItem(LoadItem("wood"), 5);
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Thirst.ShouldBe(75f);
    _inventory.GetItemCount("wood").ShouldBe(5);
  }

  [Test]
  public void CoconutAtRiverDrinksCoconutNotRiver()
  {
    PlacePlayerOnRiver();
    _inventory.AddItem(LoadItem("coconut"), 2);
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Thirst.ShouldBe(75f);
    _inventory.GetItemCount("coconut").ShouldBe(1);
  }

  [Test]
  public void OpenOceanDoesNotQuenchThirst()
  {
    var spec = WildSpec(Vector2.Zero);
    _weapon.RiverSpecsOverride = new[] { spec };
    _player.GlobalPosition = new Vector3(5000f, 1f, 5000f);
    _stats.Thirst = 50f;

    PressUseItem();

    _stats.Thirst.ShouldBe(50f);
  }

  [Test]
  public void TryDrinkFalseOffIsland()
  {
    var spec = WildSpec(Vector2.Zero);
    _stats.Thirst = 50f;
    RiverDrink.TryDrink(
      _stats,
      new Vector3(5000f, 1f, 5000f),
      new[] { spec }
    ).ShouldBeFalse();
    _stats.Thirst.ShouldBe(50f);
  }

  private IslandSpec PlacePlayerOnRiver()
  {
    var spec = WildSpec(new Vector2(80f, -40f));
    var channel = WorldFromLocal(spec, IslandHeightmap.SampleRiverLocal(spec, AlongChannel));
    _weapon.RiverSpecsOverride = new[] { spec };
    _player.GlobalPosition = new Vector3(channel.X, 1f, channel.Y);
    return spec;
  }
}
