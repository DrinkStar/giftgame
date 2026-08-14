// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

using Godot;

/// <summary>
///   R1 (Iter8p): data definition of one activatable skill. Reservation only —
///   no skill hotkeys or effects this iteration; the resource type exists so
///   .tres skill definitions can be authored later without touching code.
/// </summary>
[GlobalClass]
public partial class SkillData : Resource
{
  /// <summary>Stable skill id (e.g. "power_slash").</summary>
  [Export] public string Id = "";

  /// <summary>Stamina paid on activation (multiplied by the stamina_cost stat).</summary>
  [Export] public float StaminaCost;

  /// <summary>Seconds the skill stays on cooldown after activation.</summary>
  [Export] public float CooldownSeconds;

  /// <summary>Effect implementation id resolved by the future skill host.</summary>
  [Export] public string EffectId = "";
}
