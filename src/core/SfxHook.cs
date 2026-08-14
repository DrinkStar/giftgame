// Original (Iter8p)
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   T8p.5 (iter8p-plan Decision 8): static GameEvents → AudioManager adapter.
///   Event map:
///   - ItemAdded  (itemId ∈ {wood, coconut, stone, berries}) → "wood_chop"   (gathering)
///   - BuildingPlaced                                            → "place_building"
///   - MeleeHit                                                  → "melee_hit"
///   - PlayerDied                                                → "death_respawn"
///   Audio is optional: the AudioManager is resolved null-safely from the
///   current scene root on every call (never cached — the scene can change),
///   so a missing manager degrades to a silent no-op. Subscriptions are
///   symmetric (_Ready/_ExitTree) to keep the static bus clean.
///   The eat_drink stream is downloaded but deliberately unhooked: there is
///   no Eat/Drink GameEvent yet — a later iteration raises one (T8p.1 use
///   item already consumes food/drink, so the hook lands with that event).
/// </summary>
public partial class SfxHook : Node
{
  /// <summary>Item ids whose pickup counts as "gathering" (chop sfx).</summary>
  private static readonly string[] GatherSfxItemIds =
  {
    "wood",
    "coconut",
    "stone",
    "berries"
  };

  public override void _Ready()
  {
    GameEvents.ItemAdded += OnItemAdded;
    GameEvents.BuildingPlaced += OnBuildingPlaced;
    GameEvents.MeleeHit += OnMeleeHit;
    GameEvents.PlayerDied += OnPlayerDied;
  }

  public override void _ExitTree()
  {
    GameEvents.ItemAdded -= OnItemAdded;
    GameEvents.BuildingPlaced -= OnBuildingPlaced;
    GameEvents.MeleeHit -= OnMeleeHit;
    GameEvents.PlayerDied -= OnPlayerDied;
  }

  /// <summary>
  ///   Resolves the AudioManager child of the current scene root. Null when
  ///   the scene does not host one (tests, other scenes) — audio is optional.
  /// </summary>
  private AudioManager? ResolveAudioManager() =>
    GetTree().CurrentScene?.GetNodeOrNull<AudioManager>("AudioManager");

  private void OnItemAdded(string itemId, int amount)
  {
    if (Array.IndexOf(GatherSfxItemIds, itemId) >= 0)
      ResolveAudioManager()?.PlaySfx("wood_chop");
  }

  private void OnBuildingPlaced(string buildableName) =>
    ResolveAudioManager()?.PlaySfx("place_building");

  private void OnMeleeHit(string weaponId) =>
    ResolveAudioManager()?.PlaySfx("melee_hit");

  private void OnPlayerDied() =>
    ResolveAudioManager()?.PlaySfx("death_respawn");
}
