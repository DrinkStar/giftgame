// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T9.2 — the held-weapon visual: selecting a displayable weapon/tool on
///   the hotbar shows its model (WeaponVisual mounts the first MeshInstance3D
///   of the item's scene), a non-weapon selection hides it, and unknown items
///   are silent no-ops.
/// </summary>
public class WeaponVisualTest : TestClass
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private InventorySystem _inventory = default!;
  private WeaponVisual _visual = default!;

  public WeaponVisualTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);
    _visual = new WeaponVisual { Name = "WeaponVisual" };
    _player.AddChild(_visual);
    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup() => _fixture.Cleanup();

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  [Test]
  public void SelectingSpearShowsItsModel()
  {
    _inventory.AddItem(LoadItem("wooden_spear"), 1);

    _visual.DisplayedItemId.ShouldBe("wooden_spear");
    var mesh = _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh");
    mesh.ShouldNotBeNull();
    mesh!.Visible.ShouldBeTrue();
    mesh.Mesh.ShouldNotBeNull();
  }

  [Test]
  public void SelectingPickaxeShowsQuaterniusPickaxeMesh()
  {
    _inventory.AddItem(LoadItem("pickaxe"), 1);

    _visual.DisplayedItemId.ShouldBe("pickaxe");
    var mesh = _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh");
    mesh.ShouldNotBeNull();
    mesh!.Visible.ShouldBeTrue();
    mesh.Mesh.ShouldNotBeNull();
  }

  [Test]
  public void SelectingFishingRodShowsQuaterniusRodMesh()
  {
    _inventory.AddItem(LoadItem("fishing_rod"), 1);

    _visual.DisplayedItemId.ShouldBe("fishing_rod");
    var mesh = _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh");
    mesh.ShouldNotBeNull();
    mesh!.Visible.ShouldBeTrue();
    mesh.Mesh.ShouldNotBeNull();
  }

  [Test]
  public void SelectingPlainResourceHidesWeapon()
  {
    _inventory.AddItem(LoadItem("wood"), 5);

    _visual.DisplayedItemId.ShouldBe("");
    _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh")!.Visible.ShouldBeFalse();
  }

  [Test]
  public void EmptySelectionHidesWeapon()
  {
    // No items: the selected slot is empty.
    _visual.DisplayedItemId.ShouldBe("");
    _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh")!.Visible.ShouldBeFalse();
  }

  [Test]
  public async Task SecondarySlotActiveShowsSecondaryWeaponMesh()
  {
    var weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem"
    };
    _player.AddChild(weapon);
    await TestScene.ProcessFrame(1);

    var axe = LoadItem("stone_axe");
    var spear = LoadItem("wooden_spear");
    _inventory.AddItem(axe, 1);
    _inventory.SecondaryItem = spear;
    _inventory.SelectedHotbarSlot = 0;
    GameEvents.RaiseHotbarSelectionChanged(0);
    _visual.DisplayedItemId.ShouldBe("stone_axe");

    weapon.SecondarySlotActive = true;
    _visual.DisplayedItemId.ShouldBe("wooden_spear");
    _visual.GetNodeOrNull<MeshInstance3D>("WeaponMesh")!.Visible.ShouldBeTrue();

    weapon.SecondarySlotActive = false;
    _visual.DisplayedItemId.ShouldBe("stone_axe");
  }

  [Test]
  public async Task SyncYawToPlayerModel_OrbitsHandOffsetWithMeshYaw()
  {
    var model = new Node3D { Name = "PlayerModel" };
    _player.AddChild(model);

    _player.RemoveChild(_visual);
    _visual.Free();
    _visual = new WeaponVisual
    {
      Name = "WeaponVisual",
      Position = new Vector3(0.35f, 1.15f, -0.55f)
    };
    _player.AddChild(_visual);
    await TestScene.ProcessFrame(1);

    var rot = model.Rotation;
    rot.Y = Mathf.Pi * 0.5f;
    model.Rotation = rot;
    _visual.SyncYawToPlayerModel();

    var expectedPos = new Vector3(0.35f, 1.15f, -0.55f)
      .Rotated(Vector3.Up, Mathf.Pi * 0.5f);
    _visual.Position.DistanceTo(expectedPos).ShouldBeLessThan(0.001f);
    Mathf.Abs(Mathf.AngleDifference(_visual.Rotation.Y, Mathf.Pi * 0.5f))
      .ShouldBeLessThan(0.001f);
  }
}
