// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8p.2 (iter8p-plan Decision 4): harvestable coconut palm on the
///   Interactables physics layer (layer 3, value 4). Interacting hands the
///   player one <c>coconut</c> per harvest; after <see cref="MaxHarvests"/>
///   harvests the palm shrinks away and frees itself. Geometry is built in
///   code so Game.tscn only carries the script node.
///
///   NOTE: CollisionLayer is the INHERITED <c>CollisionObject3D.CollisionLayer</c>
///   set to 4 in <see cref="_Ready"/> (see <see cref="WoodTree"/> note).
/// </summary>
public partial class CoconutPalm : StaticBody3D, IInteractable
{
  [Export] public string InteractionVerb = "Gather coconut";
  [Export] public string ItemId = "coconut";
  [Export] public int MaxHarvests = 2;
  [Export] public string VisualModelPath = VegetationModels.Palm;
  [Export] public float VisualScale = VegetationModels.PalmScale;

  private int _remaining;

  /// <summary>Harvests left before the palm disappears.</summary>
  public int RemainingHarvests => _remaining;

  public override void _Ready()
  {
    // Layer 3 = Interactables (value 4).
    CollisionLayer = 4;
    CollisionMask = 0;

    _remaining = MaxHarvests;

    var shape = new CollisionShape3D
    {
      Name = "CollisionShape3D",
      Position = new Vector3(0, 1.2f, 0),
      Shape = new BoxShape3D { Size = new Vector3(0.8f, 2.4f, 0.8f) }
    };
    AddChild(shape);

    var trunk = new MeshInstance3D
    {
      Name = "Trunk",
      Position = new Vector3(0, 1f, 0),
      Mesh = new CylinderMesh
      {
        Height = 2f,
        TopRadius = 0.2f,
        BottomRadius = 0.3f,
        Material = new StandardMaterial3D
        {
          AlbedoColor = new Color("6B4F2A")
        }
      }
    };
    AddChild(trunk);

    // Darker crown — a simple placeholder sphere at the top of the trunk.
    var crown = new MeshInstance3D
    {
      Name = "Crown",
      Position = new Vector3(0, 2.2f, 0),
      Mesh = new SphereMesh
      {
        Radius = 0.6f,
        Height = 1.2f,
        Material = new StandardMaterial3D
        {
          AlbedoColor = new Color("2E4A1F")
        }
      }
    };
    AddChild(crown);

    if (VegetationModels.TryMount(this, VisualModelPath, VisualScale))
      VegetationModels.HidePlaceholders(this, "Trunk", "Crown");
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

    if (inventory.AddItem(GD.Load<ItemData>($"res://assets/items/{ItemId}.tres"), 1) != 0)
      return;

    _remaining--;
    if (_remaining <= 0)
      ShrinkAndFree();
  }

  /// <summary>Tweens the palm to zero scale, then frees it.</summary>
  private void ShrinkAndFree()
  {
    var tween = CreateTween();
    tween.TweenProperty(this, "scale", Vector3.Zero, 0.25);
    tween.TweenCallback(Callable.From(QueueFree));
  }
}
