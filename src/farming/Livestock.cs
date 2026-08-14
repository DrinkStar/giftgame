// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   A taming-pen animal (Iter5 plan Decisions 6/8): a StaticBody3D on
///   collision layer 3 (Interactables) with its own CollisionShape3D, so the
///   player's interaction ray (mask 29) hits it through the standard ancestor
///   walk. Implements <see cref="IInteractable"/>: Wild animals are tamed by
///   feeding near a placed "taming_pen", Tamed animals start producing on
///   another feeding, and Ready animals are collected for one produce item.
///   The state machine itself is pure in <see cref="LivestockLogic"/>.
/// </summary>
public partial class Livestock : StaticBody3D, IInteractable
{
  /// <summary>Type id carried by the farming events (e.g. "chicken", "sheep").</summary>
  [Export]
  public string LivestockType = "chicken";

  /// <summary>Produce item granted on collection (file stem in assets/items).</summary>
  [Export]
  public string ProduceItemId = "egg";

  /// <summary>Seconds of Producing before the produce becomes Ready.</summary>
  [Export]
  public float ProduceIntervalSeconds = 30f;

  /// <summary>Items accepted as feed; holding any single one suffices.</summary>
  [Export]
  public string[] FeedItemIds = ["wheat_seed", "corn_seed"];

  /// <summary>
  ///   The building system whose grids are scanned for a nearby taming_pen.
  ///   Null (unwired scene or test) fails closed: taming is refused.
  /// </summary>
  [Export]
  public BuildingSystem? BuildingSystem;

  /// <summary>Pen distance gate in meters (same range as StationLinker).</summary>
  private const float PenRange = 5f;

  private LivestockLogic.State _state = LivestockLogic.State.Wild;
  private float _produceElapsed;

  /// <summary>
  ///   Decision 6: Producing is the only timed state. A Ready animal stays
  ///   Ready until collected — <see cref="LivestockLogic.Tick"/> leaves it
  ///   untouched.
  /// </summary>
  public override void _Process(double delta)
  {
    if (_state != LivestockLogic.State.Producing)
      return;

    _produceElapsed += (float)delta;
    _state = LivestockLogic.Tick(_state, _produceElapsed, ProduceIntervalSeconds);
  }

  public string GetInteractionPrompt()
  {
    return _state switch
    {
      LivestockLogic.State.Wild => "[E] Tame (needs feed + pen)",
      LivestockLogic.State.Tamed => "[E] Feed (needs feed)",
      LivestockLogic.State.Producing => "[E] Producing…",
      _ => $"[E] Collect {ProduceItemId}"
    };
  }

  /// <summary>
  ///   Decision 8 contract: no player context is available here, so this is
  ///   state-based and always true — the prompt must stay visible in every
  ///   state and the real validation (pen proximity, feed, inventory space)
  ///   happens inside <see cref="Interact"/>, which no-ops when a requirement
  ///   is missing.
  /// </summary>
  public bool CanInteract() => true;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    var inventory = player.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    switch (_state)
    {
      case LivestockLogic.State.Wild:
        Tame(inventory);
        break;

      case LivestockLogic.State.Tamed:
        Feed(inventory);
        break;

      case LivestockLogic.State.Ready:
        Collect(inventory);
        break;

      // Producing: no-op; the prompt already reports the state.
    }
  }

  /// <summary>
  ///   Decision 6: taming requires BOTH a placed pen within range and feed.
  ///   The pen gate fails closed when BuildingSystem is unwired.
  /// </summary>
  private void Tame(InventorySystem inventory)
  {
    if (!IsPenNearby())
      return;

    if (!LivestockLogic.CanTame(_state, HasFeed(inventory)))
      return;

    if (!ConsumeFeed(inventory))
      return;

    _state = LivestockLogic.State.Tamed;
    GameEvents.RaiseLivestockTamed(LivestockType);
  }

  /// <summary>
  ///   A feeding moves a Tamed animal into Producing and resets its timer.
  /// </summary>
  private void Feed(InventorySystem inventory)
  {
    if (!LivestockLogic.StartProducing(_state, HasFeed(inventory)))
      return;

    if (!ConsumeFeed(inventory))
      return;

    _state = LivestockLogic.State.Producing;
    _produceElapsed = 0f;
  }

  /// <summary>
  ///   Decision 6: grants one produce item, but only when it actually fits —
  ///   AddItem places what it can and returns the remainder, and on any
  ///   remainder the placed portion is rolled back out so nothing is
  ///   silently lost. The animal stays Ready and keeps its produce.
  /// </summary>
  private void Collect(InventorySystem inventory)
  {
    var produce = GD.Load<ItemData>($"res://assets/items/{ProduceItemId}.tres");
    if (produce == null)
      return;

    var remaining = inventory.AddItem(produce, LivestockLogic.ProduceAmount);
    if (remaining > 0)
    {
      inventory.RemoveItem(ProduceItemId, LivestockLogic.ProduceAmount - remaining);
      return;
    }

    _state = LivestockLogic.Collect(_state);
    GameEvents.RaiseLivestockProduced(LivestockType);
  }

  /// <summary>True when the inventory holds at least one accepted feed item.</summary>
  private bool HasFeed(InventorySystem inventory)
  {
    foreach (var feedId in FeedItemIds)
    {
      if (inventory.HasItem(feedId, 1))
        return true;
    }

    return false;
  }

  /// <summary>Consumes one accepted feed item; true when one was taken.</summary>
  private bool ConsumeFeed(InventorySystem inventory)
  {
    foreach (var feedId in FeedItemIds)
    {
      if (inventory.RemoveItem(feedId, 1))
        return true;
    }

    return false;
  }

  /// <summary>
  ///   Decision 6: scans every grid cell for a placed ground object named
  ///   "taming_pen" within <see cref="PenRange"/> of this animal — the same
  ///   proximity scan as StationLinker.cs. The distance gate runs first: one
  ///   cheap check beats a name comparison for far-away buildings.
  /// </summary>
  private bool IsPenNearby()
  {
    if (BuildingSystem == null)
      return false;

    foreach (var grid in BuildingSystem.Grids)
    {
      if (grid == null)
        continue;

      foreach (var cell in grid.GridCells)
      {
        var groundObject = cell?.GroundObject;
        if (groundObject == null || !IsInstanceValid(groundObject))
          continue;

        if (groundObject.GlobalPosition.DistanceTo(GlobalPosition) >= PenRange)
          continue;

        if (groundObject.BuildableResource.Name == "taming_pen")
          return true;
      }
    }

    return false;
  }
}
