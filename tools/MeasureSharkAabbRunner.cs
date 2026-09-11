// One-shot: print Gobkit shark_king mesh AABB after Godot import.
// Usage (from game/): godot --headless --path . --script res://tools/MeasureSharkAabb.cs
// Godot 4.7 C# scripts as main: use a Node scene instead — see MeasureSharkAabbRunner.
namespace SeaAnomaly.Tools;

using Godot;

public partial class MeasureSharkAabbRunner : Node
{
  public override void _Ready()
  {
    var packed = GD.Load<PackedScene>("res://assets/models/enemies/shark_king/Shark.glb");
    if (packed == null)
    {
      GD.Print("MEASURE_FAIL load");
      GetTree().Quit(1);
      return;
    }

    var root = packed.Instantiate<Node3D>();
    AddChild(root);

    var aabb = new Aabb();
    var first = true;
    void Walk(Node n)
    {
      if (n is MeshInstance3D mi && mi.Mesh != null)
      {
        var local = mi.GetAabb();
        var xf = mi.GlobalTransform;
        // Corner-expand into world
        for (var i = 0; i < 8; i++)
        {
          var c = local.GetEndpoint(i);
          var w = xf * c;
          if (first)
          {
            aabb = new Aabb(w, Vector3.Zero);
            first = false;
          }
          else
            aabb = aabb.Expand(w);
        }
        GD.Print($"MESH {mi.GetPath()} local={local.Size} pos={mi.GlobalPosition}");
      }
      foreach (var child in n.GetChildren())
        Walk(child);
    }

    Walk(root);
    if (first)
      GD.Print("MEASURE_FAIL no mesh");
    else
      GD.Print($"MEASURE_AABB pos={aabb.Position} size={aabb.Size} end={aabb.End}");

    GetTree().Quit(0);
  }
}
