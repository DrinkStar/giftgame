// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;

/// <summary>
///   FIX(iter8-plan): T8.1 — unified game save document. One JSON file covers
///   the player stats, inventory, day/night, weather, buildings, farm plots,
///   livestock and storage boxes, replacing the previous building-only F5/F9
///   save. All sections have default values so a missing field (older save,
///   partial write) deserializes to a safe default instead of crashing — the
///   same contract JsonSaveSystem already locks for the building DTOs.
/// </summary>
public sealed class GameSaveData
{
  /// <summary>Save format version; bump when breaking DTO changes land.</summary>
  public int Version { get; set; } = 1;

  /// <summary>
  ///   R1 (Iter8p) reserve: unlocked talent ids. Kept EMPTY and never read by
  ///   any tree in the first release — the field exists so a future talent
  ///   system has a compatible slot without a schema migration.
  /// </summary>
  public List<string> UnlockedTalentIds { get; set; } = new();

  public PlayerSaveData Player { get; set; } = new();

  public InventorySaveData Inventory { get; set; } = new();

  public DayNightSaveData DayNight { get; set; } = new();

  public WeatherSaveData Weather { get; set; } = new();

  /// <summary>
  ///   Embedded building snapshot (the existing building DTO — reuse, never
  ///   re-serialized). Null when no building system is wired at save time.
  /// </summary>
  public SaveFile? Buildings { get; set; }

  public List<FarmSaveData> Farms { get; set; } = new();

  public List<LivestockSaveData> Livestock { get; set; } = new();

  /// <summary>
  ///   T8.5.8: storage box contents (one entry per placed box, keyed by world
  ///   position). Empty when no box is placed or the save predates Iter8.5.
  /// </summary>
  public List<StorageBoxSaveData> StorageBoxes { get; set; } = new();
}

/// <summary>Player survival stats (the four Iter3 bars).</summary>
public sealed class PlayerSaveData
{
  public float Health { get; set; }
  public float Hunger { get; set; }
  public float Thirst { get; set; }
  public float Stamina { get; set; }
}

/// <summary>Inventory contents: hotbar, grid and the secondary weapon slot.</summary>
public sealed class InventorySaveData
{
  /// <summary>Hotbar slots in index order; empty slots are omitted.</summary>
  public List<SlotSaveData> Hotbar { get; set; } = new();

  /// <summary>Grid slots in row-major order; empty slots are omitted.</summary>
  public List<SlotSaveData> Grid { get; set; } = new();

  /// <summary>Secondary weapon item id, or null when empty.</summary>
  public string? SecondaryItemId { get; set; }
}

/// <summary>A single inventory slot (item resolved by id on load).</summary>
public sealed class SlotSaveData
{
  public string? ItemId { get; set; }
  public int Amount { get; set; }
}

/// <summary>Day/night clock state.</summary>
public sealed class DayNightSaveData
{
  public float Hour { get; set; }
  public int Day { get; set; }
}

/// <summary>Current weather.</summary>
public sealed class WeatherSaveData
{
  public WeatherType Weather { get; set; } = WeatherType.Clear;
}

/// <summary>
///   Farm plot state, keyed by world position so a load can re-attach the
///   state to the freshly rebuilt plot instance (buildings restore first).
/// </summary>
public sealed class FarmSaveData
{
  public float PositionX { get; set; }
  public float PositionZ { get; set; }
  public string CropId { get; set; } = "";
  public float ElapsedSeconds { get; set; }
  public bool Ready { get; set; }
}

/// <summary>
///   Livestock state, keyed by world position (chicken/sheep are top-level
///   named nodes, but position matching keeps the restore order-agnostic).
/// </summary>
public sealed class LivestockSaveData
{
  public float PositionX { get; set; }
  public float PositionZ { get; set; }
  public string Type { get; set; } = "";
  public LivestockLogic.State State { get; set; } = LivestockLogic.State.Wild;
  public float ProduceElapsedSeconds { get; set; }
}

/// <summary>
///   T8.5.8: storage box contents, keyed by world position so a load can
///   re-attach the state to the freshly rebuilt box instance (buildings
///   restore first — the same contract as <see cref="FarmSaveData"/>).
///   Slots is positional (index-aligned with the box's 20 slots; empty
///   slots carry a null ItemId).
/// </summary>
public sealed class StorageBoxSaveData
{
  public float PositionX { get; set; }
  public float PositionZ { get; set; }
  public List<SlotSaveData> Slots { get; set; } = new();
}
