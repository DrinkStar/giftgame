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

  /// <summary>
  ///   The second weapon slot contents changed, carrying the newly equipped
  ///   item (null = unequipped). Raised by
  ///   <see cref="Inventory.InventorySystem.SecondaryItem"/> (Iter6.1 todo 5).
  ///   Raise-only: the event exists for future UI/audio subscribers.
  /// </summary>
  public static event Action<ItemData?>? SecondarySlotChanged;

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

  #region Building

  /// <summary>Slot (library index, mouse button) clicked in the building menu.</summary>
  public static event Action<int, int>? BuildingSlotClicked;

  public static event Action<bool>? BuildModeChanged;

  public static event Action<bool>? DemolitionModeChanged;

  /// <summary>Active grid level index changed.</summary>
  public static event Action<int>? BuildingLevelChanged;

  /// <summary>
  ///   Raised by the F5 handler after a real save: BuildingSaveSystem.Save
  ///   has run and the event carries <c>true</c> (overwrite flag). No
  ///   subscribers yet — the event exists for future UI/feedback.
  /// </summary>
  public static event Action<bool>? BuildingSaved;

  /// <summary>
  ///   Raised by the F9 handler only when a save was actually loaded, carrying
  ///   the real save filename (empty only if the loaded file name is unknown).
  ///   No subscribers yet — the event exists for future UI/feedback.
  /// </summary>
  public static event Action<string>? BuildingLoaded;

  /// <summary>A mouse tile began overlapping another body (red collision feedback).</summary>
  public static event Action? MouseTileBodyEntered;

  /// <summary>A mouse tile stopped overlapping other bodies (red collision feedback).</summary>
  public static event Action? MouseTileBodyExited;

  #endregion Building

  #region Farming

  /// <summary>
  ///   A crop was planted on a farm plot, carrying the crop id (e.g.
  ///   "potato"). Raise-only this iteration (Iter5 plan Decision 9): the
  ///   events exist so future audio/UI/stats systems can subscribe without
  ///   touching the farming nodes.
  /// </summary>
  public static event Action<string>? CropPlanted;

  /// <summary>
  ///   A ready crop was harvested, carrying the crop id. Raise-only this
  ///   iteration (Iter5 plan Decision 9).
  /// </summary>
  public static event Action<string>? CropHarvested;

  /// <summary>
  ///   A wild animal was tamed, carrying its livestock type id. Raise-only
  ///   this iteration (Iter5 plan Decision 9); first raised by W2.
  /// </summary>
  public static event Action<string>? LivestockTamed;

  /// <summary>
  ///   A tamed animal produced its produce (egg/wool/milk), carrying the
  ///   livestock type id. Raise-only this iteration (Iter5 plan Decision 9);
  ///   first raised by W2.
  /// </summary>
  public static event Action<string>? LivestockProduced;

  #endregion Farming

  #region Combat

  /// <summary>
  ///   An enemy died, carrying its data id (e.g. "crab"). Raised by
  ///   <see cref="Combat.EnemyBase.Die"/> exactly once per enemy. Raise-only
  ///   this iteration (Iter6 plan Decision 9): the event exists so future
  ///   audio/UI/quest systems can subscribe without touching the enemy nodes.
  /// </summary>
  public static event Action<string>? EnemyDied;

  /// <summary>
  ///   A boss enemy died, carrying its data id (e.g. "shark_king"). Raised in
  ///   addition to <see cref="EnemyDied"/> for enemies flagged
  ///   <c>Boss = true</c>. Raise-only this iteration (Iter6 plan Decision 9).
  /// </summary>
  public static event Action<string>? BossDefeated;

  /// <summary>
  ///   A boss transitioned between phases, carrying the boss id and the new
  ///   phase (1/2/3). Raised exactly once per transition by
  ///   <see cref="Combat.BossPhaseController"/> (Iter6.1 todo 4). Raise-only:
  ///   the event exists so future audio/UI systems can subscribe.
  /// </summary>
  public static event Action<string, int>? BossPhaseChanged;

  /// <summary>
  ///   The active weapon slot toggled, carrying the primary (hotbar) item and
  ///   the secondary slot item AFTER the switch (Iter6.1 todo 5). Raised by
  ///   <see cref="Combat.WeaponSystem.SecondarySlotActive"/>. Raise-only: the
  ///   event exists for future UI/audio subscribers.
  /// </summary>
  public static event Action<ItemData?, ItemData?>? WeaponSlotChanged;

  #endregion Combat

  #region Progression (Iter8p R1)

  /// <summary>
  ///   A talent was unlocked, carrying its talent id. R1 reserve — raise-only:
  ///   the event exists so future UI/audio/save systems can subscribe; first
  ///   raised once a real talent tree replaces the null tree.
  /// </summary>
  public static event Action<string>? TalentUnlocked;

  /// <summary>
  ///   A skill activated, carrying its skill id. R1 reserve — raise-only:
  ///   the event exists so future UI/audio/save systems can subscribe; first
  ///   raised once a real skill host replaces the null host.
  /// </summary>
  public static event Action<string>? SkillActivated;

  #endregion Progression (Iter8p R1)

  #region Quests (Iter7)

  /// <summary>
  ///   A quest became the active one, carrying its id. Raised by
  ///   <see cref="Quest.QuestService"/> when it starts a quest. Raise-only.
  /// </summary>
  public static event Action<string>? QuestStarted;

  /// <summary>
  ///   Progress of the active quest changed, carrying (questId, done, need).
  ///   Raised by <see cref="Quest.QuestService"/>. Raise-only.
  /// </summary>
  public static event Action<string, int, int>? QuestProgress;

  /// <summary>
  ///   A quest was completed, carrying its id. Raised by
  ///   <see cref="Quest.QuestService"/>. Raise-only.
  /// </summary>
  public static event Action<string>? QuestCompleted;

  /// <summary>
  ///   The player reached a story point trigger, carrying the story point id.
  ///   Raised by <see cref="Quest.StoryPointTrigger"/> exactly once per
  ///   trigger. Raise-only.
  /// </summary>
  public static event Action<string>? StoryPointReached;

  /// <summary>
  ///   The guide has a line of narration to show, carrying the text. Raised by
  ///   <see cref="Quest.GuideService"/>; consumed by the HUD subtitle. Raise-only.
  /// </summary>
  public static event Action<string>? GuideLine;

  /// <summary>
  ///   A buildable object was placed in the world, carrying its library name
  ///   (e.g. "campfire"). Raised by
  ///   <see cref="Building.BuildingSystem.TryToPlaceObject"/>. Raise-only.
  /// </summary>
  public static event Action<string>? BuildingPlaced;

  #endregion Quests (Iter7)

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

  public static void RaiseSecondarySlotChanged(ItemData? item) =>
    SecondarySlotChanged?.Invoke(item);

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

  public static void RaiseBuildingSlotClicked(int libraryIndex, int button) =>
    BuildingSlotClicked?.Invoke(libraryIndex, button);

  public static void RaiseBuildModeChanged(bool enabled) =>
    BuildModeChanged?.Invoke(enabled);

  public static void RaiseDemolitionModeChanged(bool enabled) =>
    DemolitionModeChanged?.Invoke(enabled);

  public static void RaiseBuildingLevelChanged(int levelIndex) =>
    BuildingLevelChanged?.Invoke(levelIndex);

  public static void RaiseBuildingSaved(bool overwrite) =>
    BuildingSaved?.Invoke(overwrite);

  public static void RaiseBuildingLoaded(string fileName) =>
    BuildingLoaded?.Invoke(fileName);

  public static void RaiseMouseTileBodyEntered() => MouseTileBodyEntered?.Invoke();

  public static void RaiseMouseTileBodyExited() => MouseTileBodyExited?.Invoke();

  public static void RaiseCropPlanted(string cropId) => CropPlanted?.Invoke(cropId);

  public static void RaiseCropHarvested(string cropId) => CropHarvested?.Invoke(cropId);

  public static void RaiseLivestockTamed(string livestockId) =>
    LivestockTamed?.Invoke(livestockId);

  public static void RaiseLivestockProduced(string livestockId) =>
    LivestockProduced?.Invoke(livestockId);

  public static void RaiseEnemyDied(string enemyId) => EnemyDied?.Invoke(enemyId);

  public static void RaiseBossDefeated(string enemyId) =>
    BossDefeated?.Invoke(enemyId);

  public static void RaiseBossPhaseChanged(string bossId, int phase) =>
    BossPhaseChanged?.Invoke(bossId, phase);

  public static void RaiseWeaponSlotChanged(ItemData? primary, ItemData? secondary) =>
    WeaponSlotChanged?.Invoke(primary, secondary);

  public static void RaiseTalentUnlocked(string talentId) =>
    TalentUnlocked?.Invoke(talentId);

  public static void RaiseSkillActivated(string skillId) =>
    SkillActivated?.Invoke(skillId);

  public static void RaiseQuestStarted(string questId) =>
    QuestStarted?.Invoke(questId);

  public static void RaiseQuestProgress(string questId, int done, int need) =>
    QuestProgress?.Invoke(questId, done, need);

  public static void RaiseQuestCompleted(string questId) =>
    QuestCompleted?.Invoke(questId);

  public static void RaiseStoryPointReached(string storyPointId) =>
    StoryPointReached?.Invoke(storyPointId);

  public static void RaiseGuideLine(string text) => GuideLine?.Invoke(text);

  public static void RaiseBuildingPlaced(string buildableName) =>
    BuildingPlaced?.Invoke(buildableName);

  #endregion Raise helpers
}
