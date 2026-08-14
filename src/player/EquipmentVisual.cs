// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T9.3 — armor outfits. Subscribes <see cref="GameEvents.ArmorEquipped"/>
///   and swaps the player model's material per equipped armor tier (material
///   replacement, no separate models): cloth = light fabric, leather = brown
///   hide, iron = dark metal; unequipping is not modelled (the last equipped
///   armor stays until another is equipped — matching the current gameplay
///   where armor stacks overwrite). A missing model path or unknown item id
///   is a silent no-op (fail-closed).
/// </summary>
public partial class EquipmentVisual : Node
{
  /// <summary>Player model node whose MeshInstance3D children get the outfit.</summary>
  [Export] public NodePath PlayerModelPath = new NodePath("../PlayerModel");

  private static readonly Color ClothTint = new(0.72f, 0.78f, 0.84f);
  private static readonly Color LeatherTint = new(0.45f, 0.30f, 0.17f);
  private static readonly Color IronTint = new(0.58f, 0.62f, 0.68f);

  public override void _Ready()
  {
    GameEvents.ArmorEquipped += OnArmorEquipped;
  }

  public override void _ExitTree()
  {
    GameEvents.ArmorEquipped -= OnArmorEquipped;
  }

  private void OnArmorEquipped(string itemId)
  {
    var model = GetNodeOrNull<Node3D>(PlayerModelPath);
    if (model == null)
      return;

    StandardMaterial3D? outfit = itemId switch
    {
      "cloth_armor" => MakeOutfit(ClothTint, metallic: 0f, roughness: 0.9f),
      "leather_armor" => MakeOutfit(LeatherTint, metallic: 0.3f, roughness: 0.6f),
      "iron_armor" => MakeOutfit(IronTint, metallic: 0.8f, roughness: 0.3f),
      _ => null // unknown armor id — leave the default look.
    };

    // GLB-imported player models expose their surfaces as MeshInstance3D
    // children; MaterialOverride swaps the whole look without touching the
    // mesh or animations.
    foreach (var child in model.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
    {
      if (child is MeshInstance3D meshInstance)
        meshInstance.MaterialOverride = outfit;
    }
  }

  private static StandardMaterial3D MakeOutfit(Color albedo, float metallic, float roughness) =>
    new()
    {
      AlbedoColor = albedo,
      Metallic = metallic,
      Roughness = roughness
    };
}
