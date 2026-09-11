// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8p.2 (iter8p-plan Decision 4): harvestable tree on the Interactables
///   physics layer (layer 3, value 4). Interacting hands the player one
///   <c>wood</c> item per harvest; after <see cref="MaxHarvests"/> harvests
///   the tree shrinks away and frees itself. Geometry (collision + visual)
///   is built in code so Game.tscn only carries the script node.
///
///   NOTE: CollisionLayer is the INHERITED <c>CollisionObject3D.CollisionLayer</c>
///   (already exported by the engine) set to 4 in <see cref="_Ready"/> — a
///   re-declared [Export] of the same name would shadow the base property
///   and break the Godot source generator.
/// </summary>
public partial class WoodTree : StaticBody3D, IInteractable
{
  [Export] public string InteractionVerb = "Gather wood";
  [Export] public string ItemId = "wood";
  [Export] public int MaxHarvests = 3;
  [Export] public string VisualModelPath = VegetationModels.Oak;
  [Export] public float VisualScale = VegetationModels.OakScale;

  private int _remaining;

  /// <summary>Harvests left before the tree disappears (0 after <c>_Ready</c> setup).</summary>
  public int RemainingHarvests => _remaining;

  public override void _Ready()
  {
    // Layer 3 = Interactables (value 4). The tree collides for the
    // interaction ray only; it blocks nothing else.
    CollisionLayer = 4;
    CollisionMask = 0;

    _remaining = MaxHarvests;

    var shape = new CollisionShape3D
    {
      Name = "CollisionShape3D",
      Position = new Vector3(0, 1.4f, 0),
      Shape = new BoxShape3D { Size = new Vector3(1.2f, 2.8f, 1.2f) }
    };
    AddChild(shape);

    var visual = new MeshInstance3D
    {
      Name = "Visual",
      Position = new Vector3(0, 1f, 0),
      Mesh = new CylinderMesh
      {
        Height = 2f,
        TopRadius = 0.25f,
        BottomRadius = 0.35f,
        Material = new StandardMaterial3D
        {
          AlbedoColor = new Color("6B4F2A")
        }
      }
    };
    AddChild(visual);

    if (VegetationModels.TryMount(this, VisualModelPath, VisualScale))
      VegetationModels.HidePlaceholders(this, "Visual");
  }

  public string GetInteractionPrompt() =>
    $"[E] {InteractionVerb} ({_remaining} left)";

  public bool CanInteract() => _remaining > 0;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    if (!CanInteract())
      return;

    var inventory = player.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    // A full inventory leaves the harvest available (AddItem returns the
    // unplaced remainder — nothing is consumed unless everything fit).
    if (inventory.AddItem(GD.Load<ItemData>($"res://assets/items/{ItemId}.tres"), 1) != 0)
      return;

    _remaining--;
    if (_remaining <= 0)
      ShrinkAndFree();
  }

  /// <summary>Tweens the tree to zero scale, then frees it.</summary>
  private void ShrinkAndFree()
  {
    var tween = CreateTween();
    tween.TweenProperty(this, "scale", Vector3.Zero, 0.25);
    tween.TweenCallback(Callable.From(QueueFree));
  }
}
