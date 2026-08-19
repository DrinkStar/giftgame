// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8p.3 (iter8p-plan Decision 5): a generic item lying on the ground.
///   Interacting (E) adds <see cref="Amount"/> of <see cref="ItemId"/> to
///   the player's inventory and frees the node. Lives on the Interactables
///   physics layer (layer 3, value 4).
///
///   <see cref="Spawn"/> is the static factory used by the death drop: it
///   instantiates <c>res://scenes/world/ground_loot.tscn</c> and attaches
///   it to the current scene. Kill drops keep going straight to the
///   inventory (EnemyBase untouched) — only the player death drop spawns
///   ground loot this iteration.
/// </summary>
public partial class GroundLoot : StaticBody3D, IInteractable
{
  [Export] public string ItemId = "";
  [Export] public int Amount = 1;

  public override void _Ready()
  {
    // Layer 3 = Interactables (value 4).
    CollisionLayer = 4;
    CollisionMask = 0;

    // Built in code only when the scene lacks them (direct construction in
    // tests), so scene-instanced and code-constructed loot look the same.
    if (GetNodeOrNull<CollisionShape3D>("CollisionShape3D") == null)
    {
      var shape = new CollisionShape3D
      {
        Name = "CollisionShape3D",
        Shape = new BoxShape3D { Size = new Vector3(0.3f, 0.3f, 0.3f) }
      };
      AddChild(shape);
    }

    if (GetNodeOrNull<MeshInstance3D>("Visual") == null)
    {
      var visual = new MeshInstance3D
      {
        Name = "Visual",
        Mesh = new BoxMesh
        {
          Size = new Vector3(0.22f, 0.22f, 0.22f),
          Material = new StandardMaterial3D
          {
            AlbedoColor = new Color("C8A24B")
          }
        }
      };
      AddChild(visual);
    }
  }

  public string GetInteractionPrompt()
  {
    var item = GD.Load<ItemData>($"res://assets/items/{ItemId}.tres");
    return $"[E] 拾取 {item?.DisplayName ?? ItemId}";
  }

  public bool CanInteract() => true;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    var inventory = player.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    // FIX(code-review): a full inventory must not silently delete the loot —
    // AddItem returns the unplaced remainder; keep the pile on the ground so
    // the player can clear space and pick it up (same contract as WoodTree).
    var remaining = inventory.AddItem(
      GD.Load<ItemData>($"res://assets/items/{ItemId}.tres"), Amount
    );
    if (remaining > 0)
      return;

    QueueFree();
  }

  /// <summary>
  ///   T8p.3 factory: spawns one loot pile of <paramref name="itemId"/> at
  ///   <paramref name="globalPos"/> under the current scene. Returns null
  ///   when the scene or the current scene root is unavailable (the caller
  ///   treats that as a silent skip — the death drop is best-effort).
  /// </summary>
  public static GroundLoot? Spawn(string itemId, int amount, Vector3 globalPos)
  {
    var packed = GD.Load<PackedScene>("res://scenes/world/ground_loot.tscn");
    if (packed == null)
      return null;

    var loot = packed.Instantiate<GroundLoot>();

    var scene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
    if (scene == null)
    {
      // Never added anywhere — free it to avoid leaking the instance.
      loot.Free();
      return null;
    }

    scene.AddChild(loot);
    loot.ItemId = itemId;
    loot.Amount = amount;
    loot.GlobalPosition = globalPos;
    return loot;
  }
}
