// Original — tutorial-island driftwood storage (procedural WoodenChest.glb)
namespace SeaAnomaly;

using Godot;

/// <summary>
///   World-placed storage chest: a <see cref="StorageBox"/> with its own
///   Interactables-layer hitbox and the stylized wooden GLB. PlayerInteraction
///   rays hit the child <c>Hitbox</c> and walk up to this node (same ancestor
///   walk as buildable storage). Opens the product <see cref="StorageUI"/>
///   without pausing day-night.
///
///   Geometry is built in <see cref="_Ready"/> when the packed scene lacks
///   nodes, so IslandBuilder can also construct this class directly. The GLB
///   parents lid meshes under <c>LidPivot</c>; opening tweens that node and
///   fails closed when the pivot is missing.
/// </summary>
public partial class WoodenChest : StorageBox
{
    public const string ScenePath = "res://scenes/world/wooden_chest.tscn";
    public const string ModelPath = VegetationModels.WoodenChest;
    public const string LidPivotName = "LidPivot";
    public const float OpenAngleDegrees = -80f;
    public const float OpenDuration = 0.32f;
    public const float CloseDuration = 0.24f;

    private Node3D? _lidPivot;
    private Tween? _lidTween;

    public override void _Ready()
    {
        base._Ready();
        EnsureHitbox();
        EnsureVisual();
        _lidPivot = FindLidPivot(this);
    }

    public override void Interact(PlayerController player)
    {
        var ui = FindStorageUi();
        if (ui == null)
        {
            GD.PushWarning("StorageBox: no StorageUI under the current scene; cannot open.");
            return;
        }

        if (GameEvents.GameplayInputLocked && !ui.IsOpen)
            return;

        PlayLid(open: true);
        ui.Open(this, player.GetNodeOrNull<InventorySystem>("InventorySystem"));
    }

    public override void OnStorageClosed()
    {
        PlayLid(open: false);
    }

    private void EnsureHitbox()
    {
        var hitbox = GetNodeOrNull<StaticBody3D>("Hitbox");
        if (hitbox == null)
        {
            hitbox = new StaticBody3D { Name = "Hitbox" };
            hitbox.AddChild(new CollisionShape3D
            {
                Name = "CollisionShape3D",
                Position = new Vector3(0f, 0.36f, 0f),
                Shape = new BoxShape3D { Size = new Vector3(0.95f, 0.72f, 0.58f) }
            });
            AddChild(hitbox);
        }

        // Layer 3 = Interactables (value 4). Interaction ray only.
        hitbox.CollisionLayer = 4;
        hitbox.CollisionMask = 0;
    }

    private void EnsureVisual()
    {
        if (GetNodeOrNull<Node3D>("Model") != null)
            return;

        if (GetNodeOrNull<MeshInstance3D>("Placeholder") == null)
        {
            AddChild(new MeshInstance3D
            {
                Name = "Placeholder",
                Position = new Vector3(0f, 0.255f, 0f),
                Mesh = new BoxMesh
                {
                    Size = new Vector3(0.88f, 0.4f, 0.52f),
                    Material = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.58f, 0.34f, 0.16f),
                        Roughness = 0.88f
                    }
                }
            });
        }

        if (VegetationModels.TryMount(this, ModelPath, VegetationModels.WoodenChestScale))
            VegetationModels.HidePlaceholders(this, "Placeholder");
    }

    private void PlayLid(bool open)
    {
        if (_lidPivot == null || !IsInsideTree())
            return;

        _lidTween?.Kill();
        var target = open
            ? new Vector3(Mathf.DegToRad(OpenAngleDegrees), 0f, 0f)
            : Vector3.Zero;
        _lidTween = CreateTween();
        _lidTween.SetEase(open ? Tween.EaseType.Out : Tween.EaseType.In);
        _lidTween.SetTrans(Tween.TransitionType.Cubic);
        _lidTween.TweenProperty(_lidPivot, "rotation", target, open ? OpenDuration : CloseDuration);
    }

    internal static Node3D? FindLidPivot(Node root)
    {
        if (root is Node3D named && named.Name == LidPivotName)
            return named;

        foreach (var child in root.GetChildren())
        {
            var found = FindLidPivot(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
