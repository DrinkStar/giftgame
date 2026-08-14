// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Static crop registry (Iter5 plan: "FarmingData（静态懒加载注册表）").
///   Loads exactly the six shipped CropData resources from
///   res://assets/crops/*.tres on first use and exposes them by crop id or by
///   seed item id. Deliberately NOT an autoload and NOT driven by _Ready: a
///   static lazy initializer keeps the registry testable and free of scene
///   wiring — no project.godot changes required.
/// </summary>
public static class FarmingData
{
  /// <summary>The six shipped crops (Iter5 plan Decision 3), file stem = id.</summary>
  private static readonly string[] CropIds =
  {
    "potato",
    "carrot",
    "berry",
    "mushroom",
    "corn",
    "wheat"
  };

  /// <summary>Lazy registry; initializes on first <see cref="Get"/> call.</summary>
  private static readonly Lazy<Dictionary<string, CropData>> Registry =
    new(Initialize);

  /// <summary>All registered crops keyed by id (read-only view).</summary>
  public static IReadOnlyDictionary<string, CropData> Crops => Registry.Value;

  /// <summary>Returns the crop with the given id, or null when unknown.</summary>
  public static CropData? Get(string cropId) =>
    Registry.Value.TryGetValue(cropId, out var crop) ? crop : null;

  /// <summary>
  ///   Returns the crop whose <see cref="CropData.SeedItemId"/> matches
  ///   <paramref name="seedId"/> (linear scan; six crops), or null when the
  ///   seed does not belong to any crop.
  /// </summary>
  public static CropData? GetBySeedId(string seedId)
  {
    foreach (var crop in Registry.Value.Values)
    {
      if (crop.SeedItemId == seedId)
        return crop;
    }

    return null;
  }

  /// <summary>
  ///   Loads the six CropData resources from res://assets/crops/*.tres.
  ///   Unloadable or id-less entries are skipped (never thrown) so a bad
  ///   asset degrades to "unknown crop" instead of crashing the game.
  /// </summary>
  private static Dictionary<string, CropData> Initialize()
  {
    var registry = new Dictionary<string, CropData>();

    foreach (var id in CropIds)
    {
      var crop = GD.Load<CropData>($"res://assets/crops/{id}.tres");
      if (crop != null && !string.IsNullOrEmpty(crop.Id))
        registry[crop.Id] = crop;
    }

    return registry;
  }
}
