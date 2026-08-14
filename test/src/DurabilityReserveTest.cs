// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System.IO;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   R2 (Iter8p) reserve contract: MaxDurability defaults to 0 (invincible)
///   and placed instances mark it as -1, while legacy save files without the
///   DurabilityRemaining key deserialize to -1 instead of breaking.
/// </summary>
public class DurabilityReserveTest : TestClass
{
  private const string TEST_FOLDER = "user://durability_test";

  private string _tempDirectory = default!;

  public DurabilityReserveTest(Node testScene)
    : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _tempDirectory = ProjectSettings.GlobalizePath(TEST_FOLDER);
    if (Directory.Exists(_tempDirectory))
    {
      Directory.Delete(_tempDirectory, recursive: true);
    }

    Directory.CreateDirectory(_tempDirectory);
  }

  [Cleanup]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDirectory))
    {
      Directory.Delete(_tempDirectory, recursive: true);
    }
  }

  [Test]
  public void MaxDurabilityDefaultsToZeroAndInstancesTrackIt()
  {
    // Fresh resource: 0 = invincible by default.
    var plain = new BuildableResource();
    plain.MaxDurability.ShouldBe(0);
    plain.Dispose();

    // Shipped .tres files don't set it either → 0.
    var campfire = GD.Load<BuildableResource>("res://assets/buildables/campfire.tres");
    campfire.ShouldNotBeNull();
    campfire!.MaxDurability.ShouldBe(0);

    // 0 MaxDurability → the instance marks itself -1 (invincible).
    var invincible = BuildableInstance.Create(campfire, 1u);
    invincible.CurrentDurability.ShouldBe(-1);
    invincible.Dispose();

    // Positive MaxDurability → the instance starts at exactly that value.
    var durable = new BuildableResource
    {
      Name = "durable",
      Object3DModel = campfire.Object3DModel,
      MaxDurability = 42
    };
    var instance = BuildableInstance.Create(durable, 1u);
    instance.CurrentDurability.ShouldBe(42);
    instance.Dispose();
    durable.Dispose();
  }

  [Test]
  public void LegacySaveWithoutDurabilityKeyDeserializesToMinusOne()
  {
    // A pre-R2 save file: no DurabilityRemaining key anywhere.
    const string legacyJson =
      """
      {
        "Grids": [
          {
            "Index": 0,
            "Objects": [
              {
                "Name": "campfire",
                "ResourcePath": "res://assets/buildables/campfire.tres",
                "RotationDegreesY": 0.0,
                "PositionX": 3.5,
                "PositionZ": -2.25
              }
            ]
          }
        ],
        "FreeObjects": []
      }
      """;

    var path = $"{TEST_FOLDER}/savegame_legacy.json";
    File.WriteAllText(ProjectSettings.GlobalizePath(path), legacyJson);

    var loaded = JsonSaveSystem.Load<SaveFile>(path);
    loaded.ShouldNotBeNull();
    loaded!.Grids.Count.ShouldBe(1);
    loaded.Grids[0].Objects.Count.ShouldBe(1);
    loaded.Grids[0].Objects[0].DurabilityRemaining.ShouldBe(-1);

    // Fresh DTOs start at -1 too, so the default stays consistent.
    new SaveGridObject().DurabilityRemaining.ShouldBe(-1);

    // And a round-tripped save carries the explicit value when present.
    var withDurability = new SaveFile
    {
      Grids =
      [
        new SaveGrid
        {
          Index = 0,
          Objects = [new SaveGridObject { DurabilityRemaining = 37 }]
        }
      ]
    };
    var written = JsonSaveSystem.Save(withDurability, TEST_FOLDER);
    JsonSaveSystem.Load<SaveFile>(written)!.Grids[0].Objects[0]
      .DurabilityRemaining.ShouldBe(37);
  }
}
