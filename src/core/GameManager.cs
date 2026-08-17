// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Scene wiring hub (plan Decision 1): connects the day/night and weather
///   services, the player subsystems and the HUD; handles pause (ui_cancel)
///   and restart. All other communication flows through the static
///   <see cref="GameEvents"/> bus.
///
///   Plan note: this file was specified for W1 but W1's commit did not
///   include it; it is created during W3 per the plan's W1 specification,
///   with the HUD member added as planned for W3.
///
///   SUBSCRIBES GameEvents.PlayerDied; unsubscribes in _ExitTree (Decision 13).
/// </summary>
public partial class GameManager : Node
{
  [Export] public DayNightService? DayNightService { get; set; }
  [Export] public WeatherService? WeatherService { get; set; }

  /// <summary>Spawn marker the player respawns at (T7.0; wired in Game.tscn).</summary>
  [Export] public Node3D? PlayerSpawnPoint { get; set; }

  [Export] public PlayerController? Player { get; set; }

  /// <summary>
  ///   FIX(iter8p-plan): T8p.4 respawn slot — set by the bed. Null until a
  ///   bed is slept in; RespawnPlayer prefers it over the world spawn point.
  /// </summary>
  private Vector3? _respawnSlot;

  /// <summary>
  ///   Reads the current bed respawn slot (public getter for testability —
  ///   BedTest asserts the slot through it). Null = never slept in a bed.
  /// </summary>
  public Vector3? RespawnSlot => _respawnSlot;

  /// <summary>
  ///   FIX(iter8p-plan): T8p.4 — moves the respawn slot (the bed sets it to
  ///   its own position on Interact). Called on the main thread from
  ///   <see cref="BedInteract"/>, so no deferred/thread-safety concern.
  /// </summary>
  public void SetRespawnPoint(Vector3 position) => _respawnSlot = position;

  /// <summary>HUD reference (added in W3, per the plan's W1 note).</summary>
  [Export] public HUD? HUD { get; set; }

  /// <summary>
  ///   BuildingSystem reference (W3, FIX(iter4-plan): plan Decision 8/15). The auto-load below
  ///   runs deferred so the building grids are guaranteed initialized before
  ///   the most recent save is restored.
  /// </summary>
  [Export] public BuildingSystem? BuildingSystem { get; set; }

  /// <summary>
  ///   FIX(iter8-plan): T8.1 — the unified save service. When wired, it owns
  ///   the startup auto-read (buildings + everything else) and F5/F9; the
  ///   <see cref="BuildingSystem"/> fallback below only covers scenes wired
  ///   without a SaveService (tests).
  /// </summary>
  [Export] public SaveService? SaveService { get; set; }

  public bool IsPaused { get; private set; }

  public override void _Ready()
  {
    // Pausing stops _Input for inherited-process nodes; the manager keeps
    // receiving input so the same key can unpause (upstream misses this).
    ProcessMode = ProcessModeEnum.Always;

    // Scene wiring: crafting needs the player's inventory.
    var inventory = Player?.GetNodeOrNull<InventorySystem>("InventorySystem");
    var crafting = Player?.GetNodeOrNull<CraftingSystem>("CraftingSystem");
    if (inventory != null && crafting != null)
      crafting.Initialize(inventory);

    // HUD.Initialize guards against double-init in case HUD._Ready ran
    // first (tree order: GameManager._Ready precedes HUD._Ready).
    if (Player != null)
      HUD?.Initialize(Player, DayNightService, WeatherService);

    GameEvents.PlayerDied += OnPlayerDied;
    GameEvents.RaiseGameStarted();

    // W3 auto-load (FIX(iter4-plan): plan Decision 8/15): deferred so it runs AFTER every
    // node's _Ready — in particular BuildingSystem._Ready, which spawns the
    // grids that LoadMostRecent reads. BuildingSystem is also placed BEFORE
    // GameManager in Game.tscn (FIX(iter4-plan): Decision 15), so its _Ready precedes ours
    // even without the defer; the deferred call makes the ordering explicit
    // and keeps working if the scene tree is ever reordered.
    CallDeferred(nameof(AutoLoadBuildings));
  }

  /// <summary>
  ///   W3 auto-load hook: loads the most recent building save when a
  ///   BuildingSystem is wired. A missing save file or an unwired library is
  ///   handled inside LoadMostRecent (returns false), so there is no
  ///   exception path here.
  ///
  ///   FIX(iter8-plan): T8.1 — when a SaveService is wired it owns the startup
  ///   auto-read (via its own deferred call, which covers buildings plus all
  ///   the other systems); this method then only acts as the fallback for
  ///   scenes/tests wired without a SaveService.
  ///
  ///   FIX(release): PUBLIC — Godot's source generator only registers public
  ///   methods for CallDeferred(nameof(...)) lookup.
  /// </summary>
  public void AutoLoadBuildings()
  {
    if (SaveService != null)
      return;

    BuildingSystem?.LoadMostRecent();
  }

  public override void _ExitTree() => GameEvents.PlayerDied -= OnPlayerDied;

  private void OnPlayerDied()
  {
    // FIX(iter7-plan): T7.0 respawn contract — PlayerDied is the character
    // death hook, NOT game over. GameOver stays reserved for the true ending
    // (post-shark-king epilogue) and must never be raised from here. Respawn
    // is deferred to avoid re-entering from inside the death event handler.
    GD.Print("Player died - respawning at spawn point.");
    CallDeferred(nameof(RespawnPlayer));
  }

  /// <summary>
  ///   FIX(iter7-plan): T7.0 — teleports the player to the spawn marker (falling
  ///   back to the current position when no marker is wired) and revives it via
  ///   <see cref="PlayerStats.Revive"/>.
  ///   FIX(iter8p-plan): T8p.3 death drop — the death position is captured
  ///   BEFORE the teleport so the loot lands where the player actually died.
  ///
  ///   FIX(release): PUBLIC — Godot's source generator only registers public
  ///   methods for CallDeferred(nameof(...)) lookup.
  /// </summary>
  public void RespawnPlayer()
  {
    if (Player == null)
      return;

    // Capture the death point first: the drop must land at the death spot,
    // not at the respawn marker (iter8p-plan Decision 5).
    var deathPos = Player.GlobalPosition;

    DropDeathLoot(deathPos);

    // T8p.4 (iter8p-plan Decision 6): prefer the bed respawn slot, fall
    // back to the world spawn marker, then to the current position — the
    // default fallback keeps the pre-bed behavior (and existing tests).
    Player.GlobalPosition =
      _respawnSlot ?? PlayerSpawnPoint?.GlobalPosition ?? Player.GlobalPosition;
    Player.GetNodeOrNull<PlayerStats>("PlayerStats")?.Revive();
  }

  /// <summary>
  ///   FIX(iter8p-plan): T8p.3 death drop — tools/equipment stay. Randomly
  ///   drops 1-3 non-tool stacks: for each iteration a random eligible stack
  ///   (hotbar + grid, tools and empty slots skipped) loses half its amount
  ///   (minimum 1) as ground loot at the death position with a small random
  ///   XZ offset. No eligible stack means that iteration is skipped; an
  ///   empty inventory drops nothing. Kill drops are untouched (EnemyBase).
  /// </summary>
  private void DropDeathLoot(Vector3 deathPos)
  {
    var inventory = Player?.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    var iterations = GD.RandRange(1, 3);
    for (var i = 0; i < iterations; i++)
    {
      var candidates = new System.Collections.Generic.List<(string Id, int Amount)>();

      for (var slotIndex = 0; slotIndex < inventory.HotbarSize; slotIndex++)
        CollectCandidate(inventory.GetHotbarSlot(slotIndex), candidates);

      for (var y = 0; y < inventory.InventoryHeight; y++)
      {
        for (var x = 0; x < inventory.InventoryWidth; x++)
          CollectCandidate(inventory.GetInventorySlot(x, y), candidates);
      }

      if (candidates.Count == 0)
        continue; // Nothing droppable — this iteration is skipped.

      var (id, amount) = candidates[GD.RandRange(0, candidates.Count - 1)];
      var dropAmount = Mathf.Max(1, amount / 2);

      inventory.RemoveItem(id, dropAmount);

      var offset = new Vector3(
        (float)GD.RandRange(-1.5, 1.5), 0f, (float)GD.RandRange(-1.5, 1.5)
      );
      GroundLoot.Spawn(id, dropAmount, deathPos + offset);
    }
  }

  private static void CollectCandidate(
    InventorySlot slot, System.Collections.Generic.List<(string Id, int Amount)> candidates
  )
  {
    if (slot.IsEmpty || slot.Item == null)
      return;

    if (slot.Item.Type == ItemType.Tool)
      return;

    candidates.Add((slot.Item.Id, slot.Amount));
  }

  public override void _Input(InputEvent @event)
  {
    // FIX(iter8-plan): T8.4 — a modal overlay (CraftUI) owns Esc while it is
    // open; the pause key must not fire underneath it.
    if (@event.IsActionPressed("ui_cancel") && !GameEvents.GameplayInputLocked)
      TogglePause();
  }

  public void TogglePause()
  {
    IsPaused = !IsPaused;
    GetTree().Paused = IsPaused;
    Input.MouseMode = IsPaused
      ? Input.MouseModeEnum.Visible
      : Input.MouseModeEnum.Captured;

    if (IsPaused)
      GameEvents.RaiseGamePaused();
    else
      GameEvents.RaiseGameResumed();
  }

  public void RestartGame()
  {
    GetTree().Paused = false;
    GetTree().ReloadCurrentScene();
  }
}
