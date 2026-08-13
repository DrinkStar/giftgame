// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System;

/// <summary>Time of day, used by <see cref="GameEvents.PeriodChanged"/>.</summary>
public enum DayPeriod
{
  Dawn,
  Day,
  Dusk,
  Night
}

/// <summary>
///   The four weather states driven by <see cref="WeatherLogic"/>.
/// </summary>
public enum WeatherType
{
  Clear,
  Cloudy,
  Rain,
  Storm
}

/// <summary>
///   Static C# event bus replacing Godot signal wiring for cross-system
///   communication (plan Decision 1). Upstream SurvivalIsland used per-node
///   [Signal] delegates; we funnel everything through this single hub so
///   services stay decoupled and unit-testable without a scene tree.
///
///   Threading: the game runs on a single-threaded main loop, so these
///   plain C# events need no locking. Tests must unsubscribe their handlers
///   (plan Decision 13) to keep the static bus clean between test cases.
/// </summary>
public static class GameEvents
{
  #region Lifecycle

  public static event Action? GameStarted;
  public static event Action? GamePaused;
  public static event Action? GameResumed;
  public static event Action? GameOver;
  public static event Action? PlayerDied;

  #endregion Lifecycle

  #region Survival stats (newValue, maxValue)

  public static event Action<float, float>? HealthChanged;
  public static event Action<float, float>? HungerChanged;
  public static event Action<float, float>? ThirstChanged;
  public static event Action<float, float>? StaminaChanged;

  #endregion Survival stats

  #region Day/night and weather

  /// <summary>Current hour of day (0-24), emitted every frame.</summary>
  public static event Action<float>? TimeChanged;

  /// <summary>Day number (1-based), emitted on midnight rollover.</summary>
  public static event Action<int>? DayChanged;

  /// <summary>Emitted only when the day period actually changes.</summary>
  public static event Action<DayPeriod>? PeriodChanged;

  public static event Action<WeatherType>? WeatherChanged;

  #endregion Day/night and weather

  #region Inventory

  public static event Action? InventoryChanged;
  public static event Action<int>? HotbarSelectionChanged;
  public static event Action<string, int>? ItemAdded;
  public static event Action<string, int>? ItemRemoved;

  #endregion Inventory

  #region Interaction

  public static event Action<string>? InteractionPromptChanged;
  public static event Action<float>? InteractionProgressChanged;

  #endregion Interaction

  #region Crafting

  public static event Action<string>? CraftingStarted;
  public static event Action<float>? CraftingProgress;
  public static event Action<string>? CraftingCompleted;
  public static event Action<string>? CraftingFailed;

  #endregion Crafting

  #region Raise helpers

  public static void RaiseGameStarted() => GameStarted?.Invoke();
  public static void RaiseGamePaused() => GamePaused?.Invoke();
  public static void RaiseGameResumed() => GameResumed?.Invoke();
  public static void RaiseGameOver() => GameOver?.Invoke();
  public static void RaisePlayerDied() => PlayerDied?.Invoke();

  public static void RaiseHealthChanged(float value, float max) =>
    HealthChanged?.Invoke(value, max);

  public static void RaiseHungerChanged(float value, float max) =>
    HungerChanged?.Invoke(value, max);

  public static void RaiseThirstChanged(float value, float max) =>
    ThirstChanged?.Invoke(value, max);

  public static void RaiseStaminaChanged(float value, float max) =>
    StaminaChanged?.Invoke(value, max);

  public static void RaiseTimeChanged(float hour) => TimeChanged?.Invoke(hour);
  public static void RaiseDayChanged(int day) => DayChanged?.Invoke(day);
  public static void RaisePeriodChanged(DayPeriod period) =>
    PeriodChanged?.Invoke(period);

  public static void RaiseWeatherChanged(WeatherType weather) =>
    WeatherChanged?.Invoke(weather);

  public static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
  public static void RaiseHotbarSelectionChanged(int slot) =>
    HotbarSelectionChanged?.Invoke(slot);

  public static void RaiseItemAdded(string itemId, int amount) =>
    ItemAdded?.Invoke(itemId, amount);

  public static void RaiseItemRemoved(string itemId, int amount) =>
    ItemRemoved?.Invoke(itemId, amount);

  public static void RaiseInteractionPromptChanged(string prompt) =>
    InteractionPromptChanged?.Invoke(prompt);

  public static void RaiseInteractionProgressChanged(float progress) =>
    InteractionProgressChanged?.Invoke(progress);

  public static void RaiseCraftingStarted(string recipeId) =>
    CraftingStarted?.Invoke(recipeId);

  public static void RaiseCraftingProgress(float progress) =>
    CraftingProgress?.Invoke(progress);

  public static void RaiseCraftingCompleted(string recipeId) =>
    CraftingCompleted?.Invoke(recipeId);

  public static void RaiseCraftingFailed(string recipeId) =>
    CraftingFailed?.Invoke(recipeId);

  #endregion Raise helpers
}
