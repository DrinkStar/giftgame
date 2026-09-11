// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8.5.0 — keeps the ocean clipmap mesh centered on a target (the player)
///   in the horizontal plane, so the water appears infinite while sailing.
///   The wave simulation and the CPU-side height sampler are world-space, so
///   moving the mesh does not affect GetWaveHeight results. Attach to the
///   Water node's parent (or the Water node itself) with the target exported.
/// </summary>
public partial class WaterFollow : Node
{
  /// <summary>
  ///   The node the ocean centers on (wired to the Player in Game.tscn).
  ///   Null-safe: without a target the mesh simply stays where it is.
  /// </summary>
  [Export] public Node3D? Target;

  private MeshInstance3D? _water;

  public override void _Ready()
  {
    // Works both as a sibling driver (parent is the scene root) and as a
    // child of the water mesh itself.
    _water = GetParentOrNull<MeshInstance3D>();
    if (_water == null)
    {
      _water = GetParentOrNull<Node>()?.GetNodeOrNull<MeshInstance3D>("Water");
    }
  }

  public override void _Process(double delta)
  {
    if (_water == null || Target == null)
      return;

    var position = Target.GlobalPosition;
    var rest = _water is WaterMesh mesh ? mesh.RestLevel : 0f;
    _water.GlobalPosition = new Vector3(position.X, rest, position.Z);
  }
}
