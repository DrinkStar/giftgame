namespace SeaAnomaly;

using Godot;

/// <summary>
///   Root script of scenes/ocean_test.tscn (Iter2). Wires the floating box to
///   the water mesh's CPU-side wave height sampler and injects two buoyancy
///   cells at 500 kg/m³ (≈ half-submersion — the box body runs with
///   gravity_scale = 0, so each cell's own weight balances buoyancy).
/// </summary>
public partial class OceanTest : Node3D
{
  public override void _Ready()
  {
    var waterMesh = GetNodeOrNull<WaterMesh>("Water");
    var floatBox = GetNodeOrNull<FloatingBody>("FloatBox");
    if (waterMesh == null || floatBox == null)
    {
      GD.PushWarning("OceanTest: Water/FloatBox nodes not found; buoyancy wiring skipped.");
      return;
    }

    floatBox.WaveHeightProvider = waterMesh.GetWaveHeight;
    floatBox.Cells = new[]
    {
      new BuoyantCellData(new Vector3(-0.5f, 0, 0), new Vector3(0.8f, 0.4f, 0.8f), 500f),
      new BuoyantCellData(new Vector3(0.5f, 0, 0), new Vector3(0.8f, 0.4f, 0.8f), 500f),
    };
    floatBox.BodySize = new Vector3(2, 1, 2);
  }
}
