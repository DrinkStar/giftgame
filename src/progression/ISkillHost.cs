// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

/// <summary>
///   R1 (Iter8p): the actor that owns and dispatches skills. Interface
///   reservation only — the shipped implementation is ProgressionService's
///   embedded NullSkillHost (TryActivate always false).
/// </summary>
public interface ISkillHost
{
  /// <summary>Attempts to activate the skill; false when it did not activate.</summary>
  bool TryActivate(string skillId);
}
