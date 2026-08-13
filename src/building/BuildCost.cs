// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

/// <summary>
///   Pure cost-check helper for buildable placement (plan Decision 2).
///   <c>CostItemId == ""</c> means the buildable is free — TryConsume returns
///   <c>true</c> without touching the inventory. Otherwise the full
///   <c>CostAmount</c> must be present and removable in one atomic
///   <see cref="InventorySystem.RemoveItem"/> call, so a failed payment never
///   leaves the inventory half-deducted.
/// </summary>
public static class BuildCost
{
  /// <summary>
  ///   Attempts to consume the material cost of <paramref name="resource"/>
  ///   from <paramref name="inventory"/>.
  /// </summary>
  /// <returns>
  ///   <c>true</c> when the buildable is free or the full cost was deducted;
  ///   <c>false</c> when the inventory lacks the items (nothing is deducted).
  /// </returns>
  public static bool TryConsume(InventorySystem inventory, BuildableResource resource)
  {
    if (string.IsNullOrEmpty(resource.CostItemId))
    {
      return true;
    }

    if (!inventory.HasItem(resource.CostItemId, resource.CostAmount))
    {
      return false;
    }

    return inventory.RemoveItem(resource.CostItemId, resource.CostAmount);
  }
}
