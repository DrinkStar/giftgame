namespace SeaAnomaly;

using System.IO;
using System.Linq;
using System.Threading;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Locks the building save contract (plan Decision 8 + fixes ③⑤⑥): JSON
///   DTO roundtrips through a real user:// folder, rotation stays in degrees
///   for quarter turns, missing/corrupt files return null instead of
///   throwing, and GetSaveFilesInfo only lists savegame_*.json files (the
///   injectable folder lets the test sweep an isolated temp directory).
/// </summary>
public class BuildingSaveTest : TestClass
{
  private const string TEST_FOLDER = "user://buildings_test";

  private static readonly string[] ExpectedNewestFirst = ["savegame_b.json", "savegame_a.json"];

  private string _tempDirectory = default!;

  public BuildingSaveTest(Node testScene)
    : base(testScene) { }

  [Setup]
  public void Setup()
  {
    // Start every test on a clean temp folder so saves from other tests can
    // never leak into the assertions.
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
  public void SaveFileDtoRoundTripsThroughJson()
  {
    var saveFile = new SaveFile
    {
      Grids =
      [
        new SaveGrid
        {
          Index = 0,
          Objects =
          [
            new SaveGridObject
            {
              Name = "campfire",
              ResourcePath = "res://assets/buildables/campfire.tres",
              RotationDegreesY = 90f,
              PositionX = 3.5f,
              PositionZ = -2.25f
            }
          ]
        }
      ],
      FreeObjects =
      [
        new SaveFreeObject
        {
          Name = "storage_box",
          ResourcePath = "res://assets/buildables/storage_box.tres",
          RotationDegreesY = 45f,
          PositionX = 1f,
          PositionY = 0.5f,
          PositionZ = 4f
        }
      ]
    };

    var path = JsonSaveSystem.Save(saveFile, TEST_FOLDER);
    var loaded = JsonSaveSystem.Load<SaveFile>(path);

    loaded.ShouldNotBeNull();
    loaded!.Grids.Count.ShouldBe(1);
    loaded.Grids[0].Index.ShouldBe(0);
    loaded.Grids[0].Objects.Count.ShouldBe(1);

    var gridObject = loaded.Grids[0].Objects[0];
    gridObject.Name.ShouldBe("campfire");
    gridObject.ResourcePath.ShouldBe("res://assets/buildables/campfire.tres");
    gridObject.RotationDegreesY.ShouldBe(90f);
    gridObject.PositionX.ShouldBe(3.5f);
    gridObject.PositionZ.ShouldBe(-2.25f);

    loaded.FreeObjects.Count.ShouldBe(1);
    var freeObject = loaded.FreeObjects[0];
    freeObject.Name.ShouldBe("storage_box");
    freeObject.ResourcePath.ShouldBe("res://assets/buildables/storage_box.tres");
    freeObject.RotationDegreesY.ShouldBe(45f);
    freeObject.PositionX.ShouldBe(1f);
    freeObject.PositionY.ShouldBe(0.5f);
    freeObject.PositionZ.ShouldBe(4f);

    // The produced document uses PascalCase DTO property names and lands in
    // a savegame_*.json file inside the injected folder.
    path.ShouldContain(TEST_FOLDER);
    Path.GetFileName(path).ShouldMatch("savegame_.*\\.json");
    var raw = File.ReadAllText(ProjectSettings.GlobalizePath(path));
    raw.ShouldContain("\"RotationDegreesY\"");
    raw.ShouldContain("\"ResourcePath\"");
  }

  [Test]
  public void RotationDegreesSurviveQuarterTurns()
  {
    // Fix ⑤ regression: the degrees value must come back byte-identical
    // for every quarter turn the rotation key can produce.
    foreach (var degrees in new[] { 90f, 180f, 270f })
    {
      var saveFile = new SaveFile
      {
        Grids =
        [
          new SaveGrid
          {
            Index = 0,
            Objects =
            [
              new SaveGridObject
              {
                Name = "workbench",
                RotationDegreesY = degrees,
                PositionX = 0f,
                PositionZ = 0f
              }
            ]
          }
        ]
      };

      var path = JsonSaveSystem.Save(saveFile, TEST_FOLDER);
      var loaded = JsonSaveSystem.Load<SaveFile>(path);

      loaded.ShouldNotBeNull();
      loaded!.Grids[0].Objects[0].RotationDegreesY.ShouldBe(degrees);
    }
  }

  [Test]
  public void LoadOfMissingOrCorruptFileReturnsNullWithoutThrowing()
  {
    // Missing file (fix ③ regression): upstream dereferenced the missing
    // save file outside the null guard and crashed with a null reference.
    JsonSaveSystem.Load<SaveFile>($"{TEST_FOLDER}/does_not_exist.json").ShouldBeNull();

    // Empty folder: the most-recent lookup must also come back null.
    JsonSaveSystem.LoadMostRecent<SaveFile>(TEST_FOLDER).ShouldBeNull();

    // Corrupt file: treated as missing, never throws.
    var corruptPath = Path.Combine(_tempDirectory, "savegame_corrupt.json");
    File.WriteAllText(corruptPath, "this is not valid json {");

    JsonSaveSystem.Load<SaveFile>($"{TEST_FOLDER}/savegame_corrupt.json").ShouldBeNull();
    JsonSaveSystem.LoadMostRecent<SaveFile>(TEST_FOLDER).ShouldBeNull();
  }

  [Test]
  public void GetSaveFilesInfoListsOnlySavegameFilesNewestFirst()
  {
    // Fix ⑥ regression: upstream listed every file in the folder. Unrelated
    // files must be invisible to the save listing.
    File.WriteAllText(Path.Combine(_tempDirectory, "junk.txt"), "not a save");
    File.WriteAllText(Path.Combine(_tempDirectory, "savegame_notes.txt"), "not json");
    File.WriteAllText(Path.Combine(_tempDirectory, "other.json"), "{}");

    JsonSaveSystem.Save(new SaveFile(), TEST_FOLDER, "savegame_a.json");
    // A small gap guarantees the second write gets a strictly later file
    // timestamp than the first.
    Thread.Sleep(10);
    JsonSaveSystem.Save(new SaveFile(), TEST_FOLDER, "savegame_b.json");

    var files = JsonSaveSystem.GetSaveFilesInfo(TEST_FOLDER);

    // Only the two real saves, newest first.
    files.Select(f => f.Name).ShouldBe(ExpectedNewestFirst);
  }

  [Test]
  public void LoadMostRecentReturnsNewestSave()
  {
    var older = new SaveFile
    {
      FreeObjects = [new SaveFreeObject { Name = "older" }]
    };
    var newer = new SaveFile
    {
      FreeObjects = [new SaveFreeObject { Name = "newer" }]
    };

    JsonSaveSystem.Save(older, TEST_FOLDER, "savegame_a.json");
    Thread.Sleep(10);
    JsonSaveSystem.Save(newer, TEST_FOLDER, "savegame_b.json");

    var loaded = JsonSaveSystem.LoadMostRecent<SaveFile>(TEST_FOLDER);

    loaded.ShouldNotBeNull();
    loaded!.FreeObjects.Count.ShouldBe(1);
    loaded.FreeObjects[0].Name.ShouldBe("newer");
  }
}
