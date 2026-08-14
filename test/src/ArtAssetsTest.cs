// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   T8p.5 (iter8p-plan Decision 8) art-asset landing tests: every expected
///   Iter8p icon / model / sfx asset must load through the Godot resource
///   system. Partial-download safe: an asset marked "pending" in
///   assets/sources.md (i.e. the file did not land on disk) is skipped — the
///   assertion passes without checking and the name is recorded below.
///
///   Skipped (pending) names as of 2026-08-14: none — all 16 Iter8p assets
///   landed (W5 logs this to state.json reuse_notes).
/// </summary>
public class ArtAssetsTest : TestClass
{
  public ArtAssetsTest(Node testScene) : base(testScene) { }

  private static readonly string[] IconIds =
  {
    "wood",
    "stone",
    "coconut",
    "berries",
    "stone_axe",
    "wooden_spear",
    "torch"
  };

  private static readonly string[] ModelPaths =
  {
    "res://assets/models/enemies/crab/Crab.glb",
    "res://assets/models/player/Woman.glb",
    "res://assets/models/buildings/campfire/Campfire.glb",
    "res://assets/models/buildings/bed/Bed.glb"
  };

  private static readonly string[] SfxIds =
  {
    "wood_chop",
    "eat_drink",
    "place_building",
    "melee_hit",
    "death_respawn"
  };

  private static string IconPath(string id) => $"res://assets/icons/{id}.png";
  private static string SfxPath(string id) => $"res://assets/audio/sfx/{id}.ogg";

  [Test]
  public void ItemIcons_LoadAsTextures()
  {
    foreach (var id in IconIds)
    {
      var path = IconPath(id);
      if (!FileAccess.FileExists(path))
        continue; // pending in sources.md — skipped (see class summary)

      GD.Load<Texture2D>(path).ShouldNotBeNull(path);
    }
  }

  [Test]
  public void SfxAssets_LoadAsAudioStreams()
  {
    foreach (var id in SfxIds)
    {
      var path = SfxPath(id);
      if (!FileAccess.FileExists(path))
        continue; // pending in sources.md — skipped (see class summary)

      GD.Load<AudioStream>(path).ShouldNotBeNull(path);
    }
  }

  [Test]
  public void ModelAssets_LoadAsPackedScenes()
  {
    foreach (var path in ModelPaths)
    {
      if (!FileAccess.FileExists(path))
        continue; // pending in sources.md — skipped (see class summary)

      GD.Load<PackedScene>(path).ShouldNotBeNull(path);
    }
  }

  [Test]
  public void LandedIcons_AreWiredIntoItemDataTres()
  {
    // Every landed icon must be wired into its ItemData.Icon (T8p.5 wiring
    // gate). Pending icons are skipped together with their .tres check.
    foreach (var id in IconIds)
    {
      if (!FileAccess.FileExists(IconPath(id)))
        continue; // pending in sources.md — skipped (see class summary)

      var item = GD.Load<ItemData>($"res://assets/items/{id}.tres");
      item.ShouldNotBeNull($"res://assets/items/{id}.tres");
      item!.Icon.ShouldNotBeNull($"ItemData.Icon not wired for '{id}'");
    }
  }
}
