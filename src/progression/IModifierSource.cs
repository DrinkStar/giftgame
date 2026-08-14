// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

/// <summary>
///   R1 (Iter8p): any object that scales a named stat by a multiplier.
///   The reserved statId set is: melee_damage / throw_damage / ranged_damage /
///   stamina_cost / hunger_rate / thirst_rate / move_speed / max_health.
///   A real talent tree lands in a later iteration; until then every
///   consumer multiplies by 1 (no-op).
/// </summary>
public interface IModifierSource
{
  /// <summary>Multiplier applied to <paramref name="statId"/> (1 = unchanged).</summary>
  float GetMultiplier(string statId);
}
