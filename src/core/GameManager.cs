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

  /// <summary>Optional spawn marker (no spawn marker node in Game.tscn).</summary>
  [Export] public Node3D? PlayerSpawnPoint { get; set; }

  [Export] public PlayerController? Player { get; set; }

  /// <summary>HUD reference (added in W3, per the plan's W1 note).</summary>
  [Export] public HUD? HUD { get; set; }

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
  }

  public override void _ExitTree() => GameEvents.PlayerDied -= OnPlayerDied;

  private void OnPlayerDied()
  {
    GD.Print("Game Over - Player died!");
    GameEvents.RaiseGameOver();
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
