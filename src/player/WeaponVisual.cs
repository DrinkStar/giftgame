// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   T9.2 — shows the currently selected weapon/tool's model in the player's
///   hand position (a fixed offset node under the Player, not bone-anchored —
///   the Woman.glb skeleton is not driven). Refreshes on hotbar selection and
///   inventory changes; a non-weapon selection hides the mesh. Unknown item
///   ids and unwired parents are silent no-ops (fail-closed).
/// </summary>
public partial class WeaponVisual : Node3D
{
  /// <summary>Item id → model scene path for displayable weapons/tools.</summary>
  private static readonly Dictionary<string, string> ModelPaths = new()
  {
    ["wooden_spear"] = "res://assets/models/weapons/spear/Spear.fbx",
    ["iron_spear"] = "res://assets/models/weapons/iron_spear.fbx",
    ["wooden_bow"] = "res://assets/models/weapons/bow/Bow_Wooden.glb",
    ["iron_bow"] = "res://assets/models/weapons/iron_bow.fbx",
    ["pickaxe"] = "res://assets/models/weapons/pickaxe.fbx",
    ["sickle"] = "res://assets/models/weapons/sickle.fbx",
    ["fishing_rod"] = "res://assets/models/weapons/fishing_rod.fbx",
    ["stone_axe"] = "res://assets/models/weapons/pickaxe.fbx" // axe stand-in
  };

  private MeshInstance3D? _mesh;
  private string? _currentModelPath;

  public override void _Ready()
  {
    _mesh = new MeshInstance3D { Name = "WeaponMesh", Visible = false };
    AddChild(_mesh);
    GameEvents.HotbarSelectionChanged += OnSelectionChanged;
    GameEvents.InventoryChanged += OnInventoryChanged;
    Refresh();
  }

  public override void _ExitTree()
  {
    GameEvents.HotbarSelectionChanged -= OnSelectionChanged;
    GameEvents.InventoryChanged -= OnInventoryChanged;
  }

  /// <summary>Public seam for tests — the displayed item id, or "" when hidden.</summary>
  public string DisplayedItemId { get; private set; } = "";

  private void OnSelectionChanged(int slot) => Refresh();

  private void OnInventoryChanged() => Refresh();

  private void Refresh()
  {
    var player = GetParentOrNull<PlayerController>();
    var selectedId = player?.GetNodeOrNull<InventorySystem>("InventorySystem")?.SelectedItem?.Id ?? "";

    if (string.IsNullOrEmpty(selectedId) || !ModelPaths.TryGetValue(selectedId, out var path))
    {
      DisplayedItemId = "";
      _mesh!.Visible = false;
      return;
    }

    if (path != _currentModelPath)
    {
      // A glTF/FBX scene root is a Node3D; copy the first mesh surface.
      var packed = GD.Load<PackedScene>(path);
      var instance = packed?.Instantiate<Node3D>();
      if (instance != null)
      {
        var sourceMesh = FindFirstMesh(instance);
        instance.QueueFree();

        // FIX(code-review P2-01): a failed load must HIDE the weapon, never
        // show the previous weapon's stale mesh — before this, a missing/
        // broken model path kept the old _mesh.Mesh visible while the item id
        // changed (visually wrong equipped item).
        if (sourceMesh == null)
        {
          GD.PushWarning($"WeaponVisual: no mesh in model '{path}'; hiding weapon.");
          _currentModelPath = path; // remember the failure so we retry on item change
          _mesh!.Visible = false;
          DisplayedItemId = "";
          return;
        }

        _mesh!.Mesh = sourceMesh.Mesh;
      }
      else
      {
        GD.PushWarning($"WeaponVisual: model '{path}' failed to load; hiding weapon.");
        _currentModelPath = path;
        _mesh!.Visible = false;
        DisplayedItemId = "";
        return;
      }

      _currentModelPath = path;
    }

    DisplayedItemId = selectedId;
    _mesh!.Visible = true;
  }

  /// <summary>Depth-first search for the first MeshInstance3D in a scene.</summary>
  private static MeshInstance3D? FindFirstMesh(Node root)
  {
    if (root is MeshInstance3D mesh)
      return mesh;

    foreach (var child in root.GetChildren())
    {
      var found = FindFirstMesh(child);
      if (found != null)
        return found;
    }

    return null;
  }
}
