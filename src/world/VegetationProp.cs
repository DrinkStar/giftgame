// Original — decorative island vegetation (non-harvestable)
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Non-interactable grass / shrub / rock instance. Collision stays on the
///   island heightmap; these props are visual-only so they never block the
///   crab, plateau, or harvest ray.
/// </summary>
public partial class VegetationProp : Node3D
{
    [Export] public string ModelPath = "";
    [Export] public float ModelScale = 1f;

    public override void _Ready()
    {
        VegetationModels.TryMount(this, ModelPath, ModelScale);
    }
}
