// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Data-only crop definition serialized in assets/crops/*.tres (Iter5 plan
///   Decision 3). One resource per crop; the file name equals <see cref="Id"/>.
/// </summary>
[GlobalClass]
public partial class CropData : Resource
{
  /// <summary>Unique crop id, equals the .tres file stem (e.g. "potato").</summary>
  [Export]
  public string Id { get; set; } = "";

  /// <summary>Human-readable name shown in interaction prompts.</summary>
  [Export]
  public string DisplayName { get; set; } = "";

  /// <summary>Item id of the seed consumed when planting this crop.</summary>
  [Export]
  public string SeedItemId { get; set; } = "";

  /// <summary>Item id of the produce granted when harvesting this crop.</summary>
  [Export]
  public string ProduceItemId { get; set; } = "";

  /// <summary>Real seconds from planting to ready-to-harvest.</summary>
  [Export]
  public float GrowthSeconds { get; set; }

  /// <summary>Inclusive lower bound of the harvest yield.</summary>
  [Export]
  public int MinYield { get; set; } = 1;

  /// <summary>Inclusive upper bound of the harvest yield.</summary>
  [Export]
  public int MaxYield { get; set; } = 1;
}
