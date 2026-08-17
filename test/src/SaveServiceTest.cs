// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System;
using System.IO;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   FIX(iter8-plan): T8.1/T8.2 — locks the unified SaveService contract: the
///   full snapshot (stats/inventory/day-night/weather/farms/livestock) round
///   trips through a real user:// folder, missing/corrupt saves return false
///   instead of throwing, missing DTO fields deserialize to safe defaults, and
///   farm/livestock state survives a save→load cycle via the scene groups.
///   The folder is injected so tests sweep an isolated temp directory.
/// </summary>
public class SaveServiceTest : TestClass, IDisposable
{
  private const string TEST_FOLDER = "user://saves_test";

  private Fixture _fixture = default!;
  private SaveService _service = default!;
  private PlayerStats _stats = default!;
  private InventorySystem _inventory = default!;
  private DayNightService _dayNight = default!;
  private WeatherService _weather = default!;
  private string _tempDirectory = default!;

  public SaveServiceTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _tempDirectory = ProjectSettings.GlobalizePath(TEST_FOLDER);
    if (Directory.Exists(_tempDirectory))
    {
      Directory.Delete(_tempDirectory, recursive: true);
    }

    Directory.CreateDirectory(_tempDirectory);

    _fixture = new Fixture(TestScene.GetTree());

    _stats = new PlayerStats { Name = "PlayerStats" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _dayNight = new DayNightService { Name = "DayNightService" };
    _weather = new WeatherService { Name = "WeatherService" };

    _service = new SaveService
    {
      Name = "SaveService",
      SaveFolder = TEST_FOLDER,
      PlayerStats = _stats,
      Inventory = _inventory,
      DayNight = _dayNight,
      Weather = _weather,
      BuildingSystem = null
    };

    await _fixture.AddToRoot(_stats, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(_inventory, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(_dayNight, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(_weather, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(_service, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    if (_service == null)
      return;

    _service.Dispose();
    _service = null!;
    GC.SuppressFinalize(this);
  }

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  /// <summary>The full snapshot round trips through a real save file.</summary>
  [Test]
  public void FullSnapshotRoundTripsThroughJson()
  {
    _stats.Health = 42f;
    _stats.Hunger = 37f;
    _stats.Thirst = 28f;
    _stats.Stamina = 15f;
    _inventory.AddItem(LoadItem("wood"), 5);
    _inventory.AddItem(LoadItem("berries"), 3);
    _inventory.SecondaryItem = LoadItem("wooden_spear");
    _dayNight.RestoreTime(9.5f, 3);
    _weather.SetWeather(WeatherType.Rain);

    var path = _service.SaveGame();

    // JsonSaveSystem.Save returns the user://-style path; probe the real file.
    File.Exists(ProjectSettings.GlobalizePath(path)).ShouldBeTrue();

    // Fresh instances (no tree wiring to the old ones) receive the load.
    var stats2 = new PlayerStats();
    var inventory2 = new InventorySystem();
    var dayNight2 = new DayNightService();
    var weather2 = new WeatherService();
    var service2 = new SaveService
    {
      SaveFolder = TEST_FOLDER,
      PlayerStats = stats2,
      Inventory = inventory2,
      DayNight = dayNight2,
      Weather = weather2
    };
    _fixture.AddToRoot(stats2, autoRemoveFromRoot: true);
    _fixture.AddToRoot(inventory2, autoRemoveFromRoot: true);
    _fixture.AddToRoot(dayNight2, autoRemoveFromRoot: true);
    _fixture.AddToRoot(weather2, autoRemoveFromRoot: true);

    service2.TryLoadMostRecent().ShouldBeTrue();

    stats2.Health.ShouldBe(42f);
    stats2.Hunger.ShouldBe(37f);
    stats2.Thirst.ShouldBe(28f);
    stats2.Stamina.ShouldBe(15f);
    inventory2.GetItemCount("wood").ShouldBe(5);
    inventory2.GetItemCount("berries").ShouldBe(3);
    inventory2.SecondaryItem?.Id.ShouldBe("wooden_spear");
    dayNight2.CurrentHour.ShouldBe(9.5f);
    dayNight2.CurrentDay.ShouldBe(3);
    weather2.CurrentWeather.ShouldBe(WeatherType.Rain);
  }

  /// <summary>An empty save folder reports no save instead of throwing.</summary>
  [Test]
  public void MissingSaveReturnsFalse()
  {
    _service.TryLoadMostRecent().ShouldBeFalse();
  }

  /// <summary>A corrupt save file returns false instead of throwing.</summary>
  [Test]
  public void CorruptSaveReturnsFalse()
  {
    var corruptPath = Path.Combine(_tempDirectory, "savegame_corrupt.json");
    File.WriteAllText(corruptPath, "{ not valid json !!!");
    _service.TryLoadMostRecent().ShouldBeFalse();
  }

  /// <summary>
  ///   Missing DTO fields deserialize to safe defaults (old/partial saves must
  ///   not crash) — UnlockedTalentIds stays empty and is never read.
  /// </summary>
  [Test]
  public void MissingFieldsDeserializeToDefaults()
  {
    var path = Path.Combine(_tempDirectory, "savegame_minimal.json");
    File.WriteAllText(path, """{"Version":1}""");

    var data = JsonSaveSystem.Load<GameSaveData>(path);

    data.ShouldNotBeNull();
    data!.UnlockedTalentIds.ShouldBeEmpty();
    data.Player.Health.ShouldBe(0f);
    data.DayNight.Day.ShouldBe(0);
    data.Farms.ShouldBeEmpty();
    data.Livestock.ShouldBeEmpty();
    data.Buildings.ShouldBeNull();
  }

  /// <summary>Inventory slot positions survive a save→load cycle.</summary>
  [Test]
  public void InventorySlotAlignmentSurvivesRoundTrip()
  {
    _inventory.AddItem(LoadItem("wood"), 7);
    _inventory.AddItem(LoadItem("stone"), 2);
    // Both land in the hotbar first (upstream stacking order); verify the
    // exact slot contents survive.
    var hotbar0Before = _inventory.GetHotbarSlot(0).Item?.Id;

    var snapshot = _service.Snapshot();

    var inventory2 = new InventorySystem();
    var service2 = new SaveService { Inventory = inventory2 };
    _fixture.AddToRoot(inventory2, autoRemoveFromRoot: true);
    service2.LoadGame(snapshot);

    inventory2.GetHotbarSlot(0).Item?.Id.ShouldBe(hotbar0Before);
    inventory2.GetHotbarSlot(0).Amount.ShouldBe(7);
    _inventory.GetItemCount("wood").ShouldBe(7);
    inventory2.GetItemCount("stone").ShouldBe(2);
  }

  /// <summary>Farm plot and livestock state survive a save→load cycle via groups.</summary>
  [Test]
  public void FarmAndLivestockStateSurvivesRoundTrip()
  {
    var plot = new FarmPlot { Name = "FarmPlot" };
    var chicken = new Livestock { Name = "Chicken", LivestockType = "chicken" };
    _fixture.AddToRoot(plot, autoRemoveFromRoot: true);
    _fixture.AddToRoot(chicken, autoRemoveFromRoot: true);

    // Simulate a planted/growing plot and a producing animal (public API).
    plot.ApplySaveState(
      new FarmSaveData { CropId = "potato", ElapsedSeconds = 12.5f, Ready = true }
    );
    chicken.ApplySaveState(
      new LivestockSaveData { Type = "chicken", State = LivestockLogic.State.Producing, ProduceElapsedSeconds = 4f }
    );

    var snapshot = _service.Snapshot();

    snapshot.Farms.Count.ShouldBe(1);
    snapshot.Farms[0].CropId.ShouldBe("potato");
    snapshot.Farms[0].ElapsedSeconds.ShouldBe(12.5f);
    snapshot.Farms[0].Ready.ShouldBeTrue();
    snapshot.Livestock.Count.ShouldBe(1);
    snapshot.Livestock[0].State.ShouldBe(LivestockLogic.State.Producing);
    snapshot.Livestock[0].ProduceElapsedSeconds.ShouldBe(4f);

    // Reset the same instances, then prove LoadGame re-attaches the saved
    // state by position matching (the instances stay at the origin).
    plot.ApplySaveState(
      new FarmSaveData { CropId = "", ElapsedSeconds = 0f, Ready = false }
    );
    chicken.ApplySaveState(
      new LivestockSaveData { Type = "chicken", State = LivestockLogic.State.Wild, ProduceElapsedSeconds = 0f }
    );

    var service2 = new SaveService();
    _fixture.AddToRoot(service2, autoRemoveFromRoot: true);
    service2.LoadGame(snapshot);

    plot.HasCrop.ShouldBeTrue();
    plot.IsReady.ShouldBeTrue();
    plot.GetSaveState().CropId.ShouldBe("potato");
    plot.GetSaveState().ElapsedSeconds.ShouldBe(12.5f);
    chicken.GetSaveState().State.ShouldBe(LivestockLogic.State.Producing);
    chicken.GetSaveState().ProduceElapsedSeconds.ShouldBe(4f);
  }

  /// <summary>
  ///   FIX(code-review): a backpack-expanded inventory (10→12) must restore
  ///   slot-aligned after a fresh (default-width) load — the save carries
  ///   GridWidth and the restore widens the grid first.
  /// </summary>
  [Test]
  public void ExpandedInventoryRestoresAlignedAfterFreshLoad()
  {
    _inventory.ExpandInventory(12);
    // Deterministic direct placement: stone at grid col 0, berries at the
    // 12th column (the misalignment trigger), wood on the second row.
    _inventory.GetInventorySlot(0, 0).Item = LoadItem("stone");
    _inventory.GetInventorySlot(0, 0).Amount = 1;
    _inventory.GetInventorySlot(11, 0).Item = LoadItem("berries");
    _inventory.GetInventorySlot(11, 0).Amount = 1;
    _inventory.GetInventorySlot(0, 1).Item = LoadItem("wood");
    _inventory.GetInventorySlot(0, 1).Amount = 3;

    var snapshot = _service.Snapshot();
    snapshot.Inventory.GridWidth.ShouldBe(12);

    // A fresh, default-width inventory receives the load.
    var fresh = new InventorySystem { Name = "FreshInventory" };
    var service2 = new SaveService { Inventory = fresh };
    _fixture.AddToRoot(fresh, autoRemoveFromRoot: true);
    service2.LoadGame(snapshot);

    fresh.InventoryWidth.ShouldBe(12); // widened to match the save
    fresh.GetInventorySlot(0, 0).Item?.Id.ShouldBe("stone");
    fresh.GetInventorySlot(11, 0).Item?.Id.ShouldBe("berries");
    fresh.GetInventorySlot(0, 1).Item?.Id.ShouldBe("wood");
  }

  /// <summary>F5/F9 raise the GameSaved/GameLoaded events (raise-only).</summary>
  [Test]
  public void SaveAndLoadRaiseEvents()
  {
    var savedRaised = false;
    var loadedRaised = false;

    void OnSaved(bool overwrite) => savedRaised = overwrite;
    void OnLoaded(string fileName) => loadedRaised = !string.IsNullOrEmpty(fileName);

    GameEvents.GameSaved += OnSaved;
    GameEvents.GameLoaded += OnLoaded;
    try
    {
      _service.SaveGame();
      savedRaised.ShouldBeFalse(); // First save: no prior file → overwrite=false.

      _service.SaveGame();
      savedRaised.ShouldBeTrue(); // Second save overwrites.

      _service.TryLoadMostRecent().ShouldBeTrue();
      loadedRaised.ShouldBeTrue();
    }
    finally
    {
      GameEvents.GameSaved -= OnSaved;
      GameEvents.GameLoaded -= OnLoaded;
    }
  }
}
