// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
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
}
