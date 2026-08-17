// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>Represents an instance of a buildable object in the grid building system.</summary>
public partial class BuildableInstance : Node3D
{
  /// <summary>Gets the buildable resource associated with this instance.</summary>
  public BuildableResource BuildableResource { get; private set; } = default!;

  /// <summary>Gets the object instance of the buildable resource.</summary>
  public Node3D ObjectInstance { get; private set; } = default!;

  /// <summary>
  ///   R2 (Iter8p): durability left on this instance. -1 marks invincible
  ///   (MaxDurability 0). Reserved only — no damage logic this iteration.
  /// </summary>
  public int CurrentDurability { get; private set; }

  private StaticBody3D _body = default!;
  private CollisionShape3D _collider = default!;
  private MeshInstance3D _demolitionVisual = default!;

  // We have a reference to the cells this object is in. This is an
  // optimization, similar to a Godot parent object, but here we have
  // multiple parents.
  private List<GridCell> _cells = [];

  /// <summary>
  ///   Creates a new instance of the <see cref="BuildableInstance"/> class
  ///   with the specified buildable resource and layer mask.
  /// </summary>
  /// <remarks>
  ///   This will instantiate the 3D object of the buildable resource and
  ///   create a collider for it. Plan Decision 3: the generated collider
  ///   lives on the passed layer (layer 5 "Buildings" for ground objects), so
  ///   demolition raycasts and mouse tile feedback find it. The player does
  ///   NOT collide with it this iteration (walking through buildings is
  ///   accepted).
  /// </remarks>
  /// <param name="resource">The buildable resource.</param>
  /// <param name="layerMask">The layer mask.</param>
  /// <returns>The created <see cref="BuildableInstance"/>.</returns>
  public static BuildableInstance Create(BuildableResource resource, uint layerMask)
  {
    var instance = new BuildableInstance();
    instance.Initialize(resource, layerMask);
    return instance;
  }

  private void Initialize(BuildableResource resource, uint layerMask)
  {
    if (resource.Object3DModel == null)
    {
      GD.PushWarning($"BuildableInstance: '{resource.Name}' has no Object3DModel; using empty placeholder.");
      ObjectInstance = new Node3D { Name = "MissingModel" };
    }
    else
    {
      ObjectInstance = resource.Object3DModel.Instantiate<Node3D>();
    }

    AddChild(ObjectInstance);
    BuildableResource = resource;
    _cells = [];

    // R2 (Iter8p): 0 MaxDurability = invincible, marked -1. No damage/repair
    // logic this iteration — the value is reserved for 8.x durability work.
    CurrentDurability = BuildableResource.MaxDurability > 0
      ? BuildableResource.MaxDurability
      : -1;

    CreateCollider(layerMask);
    CreateDemolishVisual();
  }

  /// <summary>Adds a grid cell to the buildable instance.</summary>
  /// <param name="cell">The grid cell to add.</param>
  public void AddCell(GridCell cell)
  {
    _cells.Add(cell);
  }

  /// <summary>Clears the buildable instance and removes it from the grid cells.</summary>
  public void ClearObject()
  {
    foreach (var cell in _cells)
    {
      if (BuildableResource.SnapBehaviour == SnapBehaviour.Ground)
      {
        cell.ClearGroundObject();
      }
      else
      {
        cell.ClearWallObject(this);
      }
    }

    _cells = [];
    ObjectInstance.QueueFree();
    QueueFree();
  }

  /// <summary>Sets the demolition view of the buildable instance.</summary>
  /// <param name="enabled">A value indicating whether the demolition view is enabled.</param>
  public void SetDemolitionView(bool enabled)
  {
    if (enabled)
    {
      _demolitionVisual.Visible = true;
      ObjectInstance.Visible = false;
    }
    else
    {
      _demolitionVisual.Visible = false;
      ObjectInstance.Visible = true;
    }
  }

  private void CreateCollider(uint layerMask)
  {
    var shape = new BoxShape3D
    {
      Size = GetSize()
    };

    _collider = new CollisionShape3D
    {
      Shape = shape
    };
    _body = new StaticBody3D
    {
      CollisionLayer = layerMask
    };
    _body.AddChild(_collider);

    AddChild(_body);

    // Optionally set floor collider offset based on the thickness of the floor.
    var offset = BuildableResource.SnapBehaviour == SnapBehaviour.Wall
      ? new Vector3(0, (float)BuildableResource.Size.Y / 2, 0)
      : Vector3.Zero;
    _collider.Position = offset;
  }

  private void CreateDemolishVisual()
  {
    var cubeMesh = new BoxMesh
    {
      Size = GetSize()
    };

    var material = new StandardMaterial3D
    {
      AlbedoColor = new Color(Colors.DarkRed, 0.8f),
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };

    _demolitionVisual = new MeshInstance3D
    {
      Mesh = cubeMesh,
      MaterialOverride = material
    };

    AddChild(_demolitionVisual);
    _demolitionVisual.Visible = false;

    // Same offset as the collider.
    var offset = BuildableResource.SnapBehaviour == SnapBehaviour.Wall
      ? new Vector3(0, (float)BuildableResource.Size.Y / 2, 0)
      : Vector3.Zero;
    _demolitionVisual.Position = offset;
  }

  private Vector3 GetSize()
  {
    var size = new Vector3(1, 1, 1);
    switch (BuildableResource.SnapBehaviour)
    {
      case SnapBehaviour.Ground:
        size = new Vector3(BuildableResource.Size.X, 0.2f, BuildableResource.Size.Z);
        break;
      case SnapBehaviour.Wall:
        size = new Vector3(BuildableResource.Size.X, BuildableResource.Size.Y, 0.2f);
        break;
      case SnapBehaviour.Free:
      {
        var meshInstance = FindMeshInstance(ObjectInstance);
        if (meshInstance?.Mesh != null)
        {
          var aabb = meshInstance.Mesh.GetAabb();
          size = aabb.Size;
        }

        break;
      }

      default:
        break;
    }

    return size;
  }

  /// <summary>
  ///   Finds the first MeshInstance3D child node of the specified parent node
  ///   (upstream BSUtils.FindMeshInstance, inlined because the BSUtils
  ///   utility class is out of port scope — only BuildingInput adapts it).
  /// </summary>
  /// <param name="parent">The parent node to search.</param>
  /// <returns>The first MeshInstance3D child node found, or null if none is found.</returns>
  private static MeshInstance3D? FindMeshInstance(Node3D parent)
  {
    foreach (var child in parent.GetChildren(true))
    {
      if (child is MeshInstance3D meshInstance)
      {
        return meshInstance;
      }
    }

    return null;
  }
}
