// SeaAnomaly W3 integration (plan Decision 10) — not part of the upstream
// MarkoDM port. Activates the Iter 3 crafting station gates by watching for
// campfire/workbench buildings near the player.
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Polls the building grids for campfire/workbench ground objects within 5
///   meters of the player and mirrors the result into the CraftingSystem
///   proximity flags (IsNearCampfire / IsNearWorkbench), which gate the Iter 3
///   recipes through CraftingRecipe.RequiresCampfire / RequiresWorkbench.
///
///   Plan Decision 10 rationale: buildings can appear and disappear through
///   several paths (placement, demolition, save-game load, grid reset), none
///   of which publish a "stations changed" event, so an event-driven design
///   would need a new event per path. A 0.5-second poll of the grid cells is
///   cheap (one level of 40×40 = 1600 cells) and the latency is imperceptible
///   for a proximity gate. When no station is near (or the wiring is absent),
///   both flags are cleared so recipes fail closed.
/// </summary>
public partial class StationLinker : Node
{
  /// <summary>Buildings closer than this distance (meters) activate the station.</summary>
  private const float StationRange = 5f;

  /// <summary>Poll cadence in seconds (plan Decision 10).</summary>
  private const double PollInterval = 0.5;

  /// <summary>The building system whose grids are scanned for stations.</summary>
  [Export] public BuildingSystem? BuildingSystem { get; set; }

  /// <summary>The player whose position defines "near".</summary>
  [Export] public PlayerController? Player { get; set; }

  /// <summary>The crafting system receiving the proximity flags.</summary>
  [Export] public CraftingSystem? Crafting { get; set; }

  private double _elapsed;

  public override void _PhysicsProcess(double delta)
  {
    _elapsed += delta;
    if (_elapsed < PollInterval)
    {
      return;
    }

    _elapsed = 0.0;
    RefreshProximity();
  }

  /// <summary>
  ///   Scans every grid cell for placed ground objects named "campfire" or
  ///   "workbench" within <see cref="StationRange"/> of the player. Any
  ///   unwired export clears both flags instead of crashing — the linker is
  ///   optional scene wiring and must never take the game down.
  /// </summary>
  private void RefreshProximity()
  {
    // Null guards: when any of the three exports is missing the flags fall
    // back to false, keeping every station-gated recipe unavailable rather
    // than throwing inside a physics tick.
    if (BuildingSystem == null || Player == null || Crafting == null)
    {
      if (Crafting != null)
      {
        Crafting.IsNearCampfire = false;
        Crafting.IsNearWorkbench = false;
      }

      return;
    }

    var nearCampfire = false;
    var nearWorkbench = false;
    var playerPosition = Player.GlobalPosition;

    foreach (var grid in BuildingSystem.Grids)
    {
      if (grid == null)
      {
        continue;
      }

      foreach (var cell in grid.GridCells)
      {
        var groundObject = cell?.GroundObject;
        if (groundObject == null || !IsInstanceValid(groundObject))
        {
          continue;
        }

        // Distance gate first — one cheap distance check beats comparing
        // names for far-away buildings. Multi-cell buildings are visited once
        // per occupied cell; the duplicate work is harmless.
        if (groundObject.GlobalPosition.DistanceTo(playerPosition) >= StationRange)
        {
          continue;
        }

        switch (groundObject.BuildableResource.Name)
        {
          case "campfire":
            nearCampfire = true;
            break;
          case "workbench":
            nearWorkbench = true;
            break;
        }
      }
    }

    Crafting.IsNearCampfire = nearCampfire;
    Crafting.IsNearWorkbench = nearWorkbench;
  }
}
