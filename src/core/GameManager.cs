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

  /// <summary>HUD reference (added in W3, per the plan's W1 note).</summary>
  [Export] public HUD? HUD { get; set; }

  /// <summary>
  ///   BuildingSystem reference (W3, FIX(iter4-plan): plan Decision 8/15). The auto-load below
  ///   runs deferred so the building grids are guaranteed initialized before
  ///   the most recent save is restored.
  /// </summary>
  [Export] public BuildingSystem? BuildingSystem { get; set; }

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
  /// </summary>
  private void AutoLoadBuildings() => BuildingSystem?.LoadMostRecent();

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
  ///   <see cref="PlayerStats.Revive"/>. No inventory drop, no GameOver.
  /// </summary>
  private void RespawnPlayer()
  {
    if (Player == null)
      return;

    Player.GlobalPosition = PlayerSpawnPoint?.GlobalPosition ?? Player.GlobalPosition;
    Player.GetNodeOrNull<PlayerStats>("PlayerStats")?.Revive();
  }

  public override void _Input(InputEvent @event)
  {
    if (@event.IsActionPressed("ui_cancel"))
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
