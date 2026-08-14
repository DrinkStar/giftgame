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
///   T8p.6 (iter8p-plan Decision 7): the torch light. The TorchLight
///   OmniLight3D under the player is visible exactly while the hotbar's
///   SelectedItem is a torch; switching to another item (or empty hands)
///   turns it off. Torch acquisition is out of scope this iteration — the
///   test injects the item directly.
/// </summary>
public class TorchTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;
  private OmniLight3D _light = default!;

  public TorchTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(stats);
    _player.Stats = stats;

    // Empty inventory: each test adds exactly the items it needs.
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);

    _light = new OmniLight3D { Name = "TorchLight", Visible = false };
    _player.AddChild(_light);

    _weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem",
      TorchLightPath = "../TorchLight"
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

  /// <summary>
  ///   Drives the light-update seam the same way the game loop does.
  /// </summary>
  private void TickWeapon() => _weapon._Process(1.0 / 60.0);

  /// <summary>
  ///   T8p.6 (a): holding a torch in the selected hotbar slot turns the
  ///   TorchLight on.
  /// </summary>
  [Test]
  public void HoldingTorchTurnsLightOn()
  {
    _light.Visible.ShouldBeFalse();

    _inventory.AddItem(LoadItem("torch"), 1);
    // Lands in the first empty hotbar slot, which is the default selection.
    _inventory.SelectedItem!.Id.ShouldBe("torch");

    TickWeapon();

    _light.Visible.ShouldBeTrue();
  }

  /// <summary>
  ///   T8p.6 (b): switching the hotbar selection away from the torch (to an
  ///   axe) turns the light back off.
  /// </summary>
  [Test]
  public void SwitchingAwayTurnsLightOff()
  {
    _inventory.AddItem(LoadItem("torch"), 1);
    _inventory.AddItem(LoadItem("stone_axe"), 1);

    TickWeapon();
    _light.Visible.ShouldBeTrue();

    _inventory.SelectedHotbarSlot = 1;
    _inventory.SelectedItem!.Id.ShouldBe("stone_axe");

    TickWeapon();

    _light.Visible.ShouldBeFalse();
  }

  /// <summary>
  ///   T8p.6: with no torch anywhere (empty hands) the light stays off.
  /// </summary>
  [Test]
  public void NoTorchLightStaysOff()
  {
    _light.Visible.ShouldBeFalse();

    TickWeapon();

    _light.Visible.ShouldBeFalse();
  }
}
