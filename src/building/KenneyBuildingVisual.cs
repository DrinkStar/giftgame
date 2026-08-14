// Original (Iter9) — visual-only helper
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T9.1 — extracts a single named mesh from a Kenney GLB kit (godot-refs
///   MarkoDM reuse, CC0 Kenney assets) and mounts it as this node's visual.
///   The upstream .glb imports as a scene whose child MeshInstance3D nodes are
///   named after each kit piece (floor/table/wall/rail/campfire_pit/...);
///   instantiating it and copying just the wanted mesh keeps each building a
///   single lightweight mesh instance. Kenney kits use per-vertex albedo plus
///   an external colormap texture, so the material is rebuilt in code with the
///   colormap when one is provided (the glb's own material path points into
///   godot-refs and does not survive the copy).
/// </summary>
public partial class KenneyBuildingVisual : Node3D
{
  /// <summary>The Kenney GLB scene to take a mesh from.</summary>
  [Export] public PackedScene? Source;

  /// <summary>Name of the kit piece to extract (e.g. "table", "rail").</summary>
  [Export] public string MeshName = "";

  /// <summary>Optional Kenney colormap texture applied as the albedo.</summary>
  [Export] public Texture2D? Colormap;

  /// <summary>Uniform scale applied to the extracted mesh.</summary>
  [Export] public float MeshScale = 1f;

  /// <summary>Positional offset (grid cell alignment tuning).</summary>
  [Export] public Vector3 MeshOffset;

  public override void _Ready()
  {
    if (Source == null || string.IsNullOrEmpty(MeshName))
      return;

    var instance = Source.Instantiate<Node3D>();
    var sourceMesh = FindMesh(instance, MeshName);
    if (sourceMesh?.Mesh != null)
    {
      var copy = new MeshInstance3D
      {
        Mesh = sourceMesh.Mesh,
        Position = MeshOffset,
        Scale = Vector3.One * MeshScale
      };

      if (Colormap != null)
      {
        // Kenney colormap material: per-vertex albedo + the shared colormap
        // texture, cull disabled (kit pieces face both ways).
        copy.MaterialOverride = new StandardMaterial3D
        {
          VertexColorUseAsAlbedo = true,
          CullMode = BaseMaterial3D.CullModeEnum.Disabled,
          AlbedoTexture = Colormap,
          TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear
        };
      }

      AddChild(copy);
    }

    instance.QueueFree();
  }

  /// <summary>Finds the first MeshInstance3D named <paramref name="name"/> in
  /// the instantiated GLB tree (depth-first).</summary>
  private static MeshInstance3D? FindMesh(Node root, string name)
  {
    if (root is MeshInstance3D mesh && mesh.Name == name)
      return mesh;

    foreach (var child in root.GetChildren())
    {
      if (child is Node3D node3D)
      {
        var found = FindMesh(node3D, name);
        if (found != null)
          return found;
      }
    }

    return null;
  }
}
