// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   T9.3 — the armor-outfit visuals: raising
///   <see cref="GameEvents.ArmorEquipped"/> swaps the player model's
///   MeshInstance3D MaterialOverride per armor tier (cloth/leather/iron),
///   an unknown armor id leaves the look untouched, and a missing model path
///   is a silent no-op.
/// </summary>
public class EquipmentVisualTest : TestClass
{
  private Fixture _fixture = default!;
  private EquipmentVisual _visual = default!;
  private Node3D _model = default!;
  private MeshInstance3D _mesh = default!;

  public EquipmentVisualTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _visual = new EquipmentVisual
    {
      Name = "EquipmentVisual",
      PlayerModelPath = new NodePath("./PlayerModel") // the model is a child
    };
    _model = new Node3D { Name = "PlayerModel" };
    _mesh = new MeshInstance3D { Name = "Body", Mesh = new BoxMesh() };
    _model.AddChild(_mesh);

    _visual.AddChild(_model);
    await _fixture.AddToRoot(_visual, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup() => _fixture.Cleanup();

  [Test]
  public void EquippingIronArmorAppliesMetallicOverride()
  {
    GameEvents.RaiseArmorEquipped("iron_armor");

    _mesh.MaterialOverride.ShouldNotBeNull();
    _mesh.MaterialOverride.ShouldBeOfType<StandardMaterial3D>();
    var mat = (StandardMaterial3D)_mesh.MaterialOverride;
    mat.Metallic.ShouldBeGreaterThan(0.5f);
  }

  [Test]
  public void EquippingClothArmorAppliesMatteOverride()
  {
    GameEvents.RaiseArmorEquipped("cloth_armor");

    _mesh.MaterialOverride.ShouldNotBeNull();
    var mat = (StandardMaterial3D)_mesh.MaterialOverride;
    mat.Roughness.ShouldBeGreaterThan(0.8f);
  }

  [Test]
  public void UnknownArmorIdLeavesModelUntouched()
  {
    GameEvents.RaiseArmorEquipped("backpack");

    _mesh.MaterialOverride.ShouldBeNull();
  }
}
