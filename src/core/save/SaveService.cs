// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   FIX(iter8-plan): T8.1 — unified save/load total control. F5 (quick_save),
///   F9 (quick_load) and the startup auto-read ALL go through this single
///   service; nothing else writes or reads a save file anymore. Internally it
///   reuses the existing JsonSaveSystem (generic JSON + user:// folder) and the
///   existing BuildingSaveSystem snapshot DTOs — building serialization is
///   reused, never rewritten.
///
///   Wiring: node in Game.tscn with [Export] references to the systems it
///   snapshots. All exports are nullable and fail closed — an unwired system
///   is simply skipped on save and left untouched on load. The save folder is
///   injectable so tests write to an isolated user:// folder.
/// </summary>
public partial class SaveService : Node
{
  /// <summary>Where the unified save document lives (injectable for tests).</summary>
  [Export] public string SaveFolder = "user://saves";

  [Export] public PlayerStats? PlayerStats;
  [Export] public InventorySystem? Inventory;
  [Export] public DayNightService? DayNight;
  [Export] public WeatherService? Weather;
  [Export] public BuildingSystem? BuildingSystem;

  /// <summary>Position-matching tolerance (meters) for farm plot / livestock restore.</summary>
  private const float PositionTolerance = 1.0f;

  private string? _currentSaveFile;

  public override void _Ready()
  {
    // F5/F9 must work even while the tree is paused (same as the pause key).
    ProcessMode = ProcessModeEnum.Always;

    // Startup auto-read. Deferred so every system's _Ready (in particular
    // BuildingSystem._Ready, which spawns the grids) has run first — the same
    // ordering the old GameManager auto-load relied on.
    CallDeferred(nameof(AutoLoad));
  }

  public override void _Input(InputEvent @event)
  {
    // FIX(iter8-plan): T8.1 — F5/F9 moved here from BuildingSystem._Input.
    // The actions are registered at runtime by BuildingInput.RegisterInputActions.
    if (@event.IsActionPressed("quick_save"))
    {
      SaveGame();
    }
    else if (@event.IsActionPressed("quick_load"))
    {
      TryLoadMostRecent();
    }
  }

  private void AutoLoad()
  {
    // Skip the startup auto-read under GoDotTest: test scenes must start from
    // a clean state, never from whatever is in the real user:// save folder.
    if (Chickensoft.GodotNodeInterfaces.RuntimeContext.IsTesting)
    {
      return;
    }

    if (TryLoadMostRecent())
    {
      GD.Print($"SaveService: auto-loaded save '{_currentSaveFile}'.");
    }
  }

  /// <summary>Builds the current full game snapshot without writing it.</summary>
  public GameSaveData Snapshot()
  {
    var data = new GameSaveData();

    if (PlayerStats != null)
    {
      data.Player = new PlayerSaveData
      {
        Health = PlayerStats.Health,
        Hunger = PlayerStats.Hunger,
        Thirst = PlayerStats.Thirst,
        Stamina = PlayerStats.Stamina
      };
    }

    if (Inventory != null)
    {
      data.Inventory = SnapshotInventory();
    }

    if (DayNight != null)
    {
      data.DayNight = new DayNightSaveData
      {
        Hour = DayNight.CurrentHour,
        Day = DayNight.CurrentDay
      };
    }

    if (Weather != null)
    {
      data.Weather = new WeatherSaveData { Weather = Weather.CurrentWeather };
    }

    if (BuildingSystem != null)
    {
      data.Buildings = BuildingSystem.BuildSaveSnapshot();
    }

    // Farm plots are group-registered in FarmPlot._Ready; livestock likewise.
    // GetTree() can be null when the service is exercised off-tree (unit
    // tests) — the groups are simply empty then.
    var tree = GetTree();
    if (tree != null)
    {
      foreach (var node in tree.GetNodesInGroup("farm_plots"))
      {
        if (node is FarmPlot plot)
        {
          data.Farms.Add(plot.GetSaveState());
        }
      }

      foreach (var node in tree.GetNodesInGroup("livestock"))
      {
        if (node is Livestock animal)
        {
          data.Livestock.Add(animal.GetSaveState());
        }
      }
    }

    return data;
  }

  /// <summary>Saves the current game to the save folder and raises GameSaved.</summary>
  /// <returns>The full user:// path of the written save file.</returns>
  public string SaveGame()
  {
    var overwrite = _currentSaveFile != null;
    var fileName = overwrite && _currentSaveFile != null
      ? StripFolder(_currentSaveFile)
      : null;
    var path = JsonSaveSystem.Save(Snapshot(), SaveFolder, fileName);
    _currentSaveFile = path;
    GameEvents.RaiseGameSaved(overwrite);
    return path;
  }

  /// <summary>
  ///   Loads the most recent unified save (startup auto-read and F9). Returns
  ///   false when no save exists or the file is missing/corrupt — never
  ///   throws (JsonSaveSystem contract). Raises GameLoaded only on a real load.
  /// </summary>
  public bool TryLoadMostRecent()
  {
    var files = JsonSaveSystem.GetSaveFilesInfo(SaveFolder);
    if (files.Count == 0)
    {
      return false;
    }

    var data = JsonSaveSystem.Load<GameSaveData>(files[0].FullName);
    if (data == null)
    {
      return false;
    }

    var loaded = LoadGame(data);
    if (loaded)
    {
      _currentSaveFile = files[0].FullName;
      GameEvents.RaiseGameLoaded(files[0].FullName);
    }

    return loaded;
  }

  /// <summary>
  ///   Applies a full game snapshot to the wired systems. Restore order
  ///   matters: buildings FIRST (re-creates the farm plot instances), then
  ///   farm plots and livestock matched by world position, then the scalar
  ///   state (inventory / stats / clock / weather).
  /// </summary>
  public bool LoadGame(GameSaveData data)
  {
    if (BuildingSystem != null && data.Buildings != null)
    {
      BuildingSystem.RestoreFromSnapshot(data.Buildings);
    }

    RestoreFarms(data.Farms);
    RestoreLivestock(data.Livestock);

    if (Inventory != null)
    {
      RestoreInventory(data.Inventory);
    }

    if (PlayerStats != null)
    {
      RestorePlayer(data.Player);
    }

    if (DayNight != null)
    {
      DayNight.RestoreTime(data.DayNight.Hour, data.DayNight.Day);
    }

    if (Weather != null)
    {
      Weather.SetWeather(data.Weather.Weather);
    }

    return true;
  }

  #region Inventory

  private InventorySaveData SnapshotInventory()
  {
    var inv = new InventorySaveData();

    // Snapshot EVERY slot (including empties) so the list index stays
    // positionally aligned with the slot index on restore.
    for (var i = 0; i < Inventory!.HotbarSize; i++)
    {
      var slot = Inventory.GetHotbarSlot(i);
      inv.Hotbar.Add(
        new SlotSaveData { ItemId = slot.Item?.Id, Amount = slot.Item == null ? 0 : slot.Amount }
      );
    }

    for (var y = 0; y < Inventory.InventoryHeight; y++)
    {
      for (var x = 0; x < Inventory.InventoryWidth; x++)
      {
        var slot = Inventory.GetInventorySlot(x, y);
        inv.Grid.Add(
          new SlotSaveData { ItemId = slot.Item?.Id, Amount = slot.Item == null ? 0 : slot.Amount }
        );
      }
    }

    inv.SecondaryItemId = Inventory.SecondaryItem?.Id;
    return inv;
  }

  private void RestoreInventory(InventorySaveData data)
  {
    // Clear every slot silently, then write back the saved contents directly
    // (slot fields are writable) so no per-slot events fire mid-restore.
    for (var i = 0; i < Inventory!.HotbarSize; i++)
    {
      var slot = Inventory.GetHotbarSlot(i);
      slot.Item = null;
      slot.Amount = 0;
    }

    for (var y = 0; y < Inventory.InventoryHeight; y++)
    {
      for (var x = 0; x < Inventory.InventoryWidth; x++)
      {
        var slot = Inventory.GetInventorySlot(x, y);
        slot.Item = null;
        slot.Amount = 0;
      }
    }

    for (var i = 0; i < data.Hotbar.Count && i < Inventory.HotbarSize; i++)
    {
      var saved = data.Hotbar[i];
      if (string.IsNullOrEmpty(saved.ItemId) || saved.Amount <= 0)
      {
        continue;
      }

      var item = GD.Load<ItemData>($"res://assets/items/{saved.ItemId}.tres");
      if (item == null)
      {
        continue;
      }

      var slot = Inventory.GetHotbarSlot(i);
      slot.Item = item;
      slot.Amount = saved.Amount;
    }

    var gridIndex = 0;
    for (var y = 0; y < Inventory.InventoryHeight; y++)
    {
      for (var x = 0; x < Inventory.InventoryWidth; x++, gridIndex++)
      {
        if (gridIndex >= data.Grid.Count)
        {
          break;
        }

        var saved = data.Grid[gridIndex];
        if (string.IsNullOrEmpty(saved.ItemId) || saved.Amount <= 0)
        {
          continue;
        }

        var item = GD.Load<ItemData>($"res://assets/items/{saved.ItemId}.tres");
        if (item == null)
        {
          continue;
        }

        var slot = Inventory.GetInventorySlot(x, y);
        slot.Item = item;
        slot.Amount = saved.Amount;
      }
    }

    if (!string.IsNullOrEmpty(data.SecondaryItemId))
    {
      var item = GD.Load<ItemData>($"res://assets/items/{data.SecondaryItemId}.tres");
      if (item != null)
      {
        Inventory.SecondaryItem = item;
      }
    }

    // One consolidated refresh signal for the HUD.
    GameEvents.RaiseInventoryChanged();
  }

  #endregion Inventory

  #region Player / clock / weather

  private void RestorePlayer(PlayerSaveData data)
  {
    // Health <= 0 means an invalid (dead) save — skip it so the Health setter
    // cannot re-raise PlayerDied mid-load; everything else restores verbatim.
    if (data.Health > 0)
    {
      PlayerStats!.Health = data.Health;
    }

    PlayerStats!.Hunger = data.Hunger;
    PlayerStats.Thirst = data.Thirst;
    PlayerStats.Stamina = data.Stamina;
  }

  #endregion Player / clock / weather

  #region Farm plots & livestock

  private void RestoreFarms(List<FarmSaveData> farms)
  {
    var tree = GetTree();
    if (tree == null)
    {
      return;
    }

    foreach (var farm in farms)
    {
      var plot = FindNearest<FarmPlot>(
        tree.GetNodesInGroup("farm_plots"),
        new Vector3(farm.PositionX, 0, farm.PositionZ)
      );
      plot?.ApplySaveState(farm);
    }
  }

  private void RestoreLivestock(List<LivestockSaveData> livestock)
  {
    var tree = GetTree();
    if (tree == null)
    {
      return;
    }

    foreach (var entry in livestock)
    {
      var animal = FindNearest<Livestock>(
        tree.GetNodesInGroup("livestock"),
        new Vector3(entry.PositionX, 0, entry.PositionZ)
      );
      if (animal != null && animal.LivestockType == entry.Type)
      {
        animal.ApplySaveState(entry);
      }
    }
  }

  /// <summary>Finds the nearest matching node within the position tolerance.</summary>
  private static T? FindNearest<T>(Godot.Collections.Array<Node> nodes, Vector3 position)
    where T : Node3D
  {
    T? best = null;
    var bestDistance = PositionTolerance;
    foreach (var node in nodes)
    {
      if (node is not T typed || !IsInstanceValid(typed))
      {
        continue;
      }

      var distance = typed.GlobalPosition.DistanceTo(position);
      if (distance <= bestDistance)
      {
        bestDistance = distance;
        best = typed;
      }
    }

    return best;
  }

  #endregion Farm plots & livestock

  /// <summary>Strips the folder from a user:// path, leaving the file name.</summary>
  private static string StripFolder(string path)
  {
    var slash = path.LastIndexOf('/');
    return slash >= 0 ? path[(slash + 1)..] : path;
  }
}
