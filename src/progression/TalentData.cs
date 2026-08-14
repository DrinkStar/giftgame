// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

using Godot;

/// <summary>
///   R1 (Iter8p): data definition of one talent node. Reservation only — no
///   talent UI or tree content this iteration; the resource type exists so
///   .tres talent definitions can be authored later without touching code.
/// </summary>
[GlobalClass]
public partial class TalentData : Resource
{
  /// <summary>Stable talent id (e.g. "mighty_blows").</summary>
  [Export] public string Id = "";

  /// <summary>Human-readable talent name shown in the future talent UI.</summary>
  [Export] public string DisplayName = "";

  /// <summary>Talent ids that must be unlocked before this one unlocks.</summary>
  [Export] public string[] Requires = [];

  /// <summary>
  ///   Stat modifiers this talent grants once unlocked. Each entry is a
  ///   Dictionary with two keys — "stat_id" (string, e.g. "melee_damage")
  ///   and "value" (float, e.g. 0.2 for +20%). The same serializable
  ///   shape <see cref="Inventory.InventorySystem.StartingItems"/> uses,
  ///   so .tres files can declare the data inline.
  /// </summary>
  [Export]
  public Godot.Collections.Array<Godot.Collections.Dictionary> StatModifiers = [];
}
