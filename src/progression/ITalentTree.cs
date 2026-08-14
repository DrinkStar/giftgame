// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

using System.Collections.Generic;

/// <summary>
///   R1 (Iter8p): the talent unlock ledger. Interface reservation only — no
///   UI, XP or real talent nodes this iteration; the shipped implementation
///   is <c>ProgressionService</c>'s embedded NullTalentTree (nothing
///   unlockable, no multipliers).
/// </summary>
public interface ITalentTree
{
  /// <summary>True when the talent has been unlocked.</summary>
  bool IsUnlocked(string talentId);

  /// <summary>Attempts to unlock a talent; false when it stays locked (no-op).</summary>
  bool Unlock(string talentId);

  /// <summary>Every talent id the tree knows about.</summary>
  IEnumerable<string> AllIds { get; }
}
