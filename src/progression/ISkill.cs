// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

/// <summary>
///   R1 (Iter8p): a single activatable skill. Interface reservation only —
///   no skill hotkeys or real effects this iteration; the shipped host is
///   ProgressionService's embedded NullSkillHost (nothing can activate).
/// </summary>
public interface ISkill
{
  /// <summary>True when the skill may activate in <paramref name="context"/>.</summary>
  bool CanActivate(SkillContext context);

  /// <summary>Runs the skill effect for <paramref name="context"/>.</summary>
  void Activate(SkillContext context);
}
