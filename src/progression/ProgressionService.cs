// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

using Godot;

/// <summary>
///   R1 (Iter8p): progression hub mounted as a scene node. Consumers inject
///   it via <c>[Export] public ProgressionService? Progression</c> and treat
///   null exactly like the defaults — every multiplier is 1 and no skill can
///   activate. The embedded null implementations keep the whole system a
///   no-op until the real talent tree/skill host land in a later iteration.
/// </summary>
/// <remarks>
///   The talent tree/skill host are plain public properties, NOT [Export]:
///   the Godot C# analyzer rejects exported members of interface type
///   (GD0102). Code/tests inject concrete implementations directly; scene
///   wiring lands when the real tree exists as a concrete class.
/// </remarks>
public partial class ProgressionService : Node
{
  /// <summary>The talent ledger; defaults to the no-op NullTalentTree.</summary>
  public ITalentTree TalentTree { get; set; } = NullTalentTree.Instance;

  /// <summary>The skill dispatcher; defaults to the no-op NullSkillHost.</summary>
  public ISkillHost SkillHost { get; set; } = NullSkillHost.Instance;

  /// <summary>
  ///   Multiplier for <paramref name="statId"/> (1 = unchanged). The talent
  ///   tree is only consulted when it also implements
  ///   <see cref="IModifierSource"/> — the null tree does (returns 1).
  /// </summary>
  public float GetMultiplier(string statId) =>
    TalentTree is IModifierSource source ? source.GetMultiplier(statId) : 1f;
}

/// <summary>
///   R1 null talent tree: nothing is ever unlocked and every multiplier is 1.
/// </summary>
internal sealed class NullTalentTree : ITalentTree, IModifierSource
{
  public static readonly NullTalentTree Instance = new();

  private NullTalentTree() { }

  public bool IsUnlocked(string talentId) => false;

  public bool Unlock(string talentId) => false;

  public System.Collections.Generic.IEnumerable<string> AllIds { get; } =
    System.Array.Empty<string>();

  public float GetMultiplier(string statId) => 1f;
}

/// <summary>R1 null skill host: no skill can ever activate.</summary>
internal sealed class NullSkillHost : ISkillHost
{
  public static readonly NullSkillHost Instance = new();

  private NullSkillHost() { }

  public bool TryActivate(string skillId) => false;
}
