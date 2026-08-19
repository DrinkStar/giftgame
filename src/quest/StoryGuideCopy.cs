// Original — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;

/// <summary>
///   Main-story copy for the remote guide: short in-world lines (HUD subtitle)
///   plus the persistent "当前目标" tracker. Not a tutorial overlay; never
///   pauses the tree. Unknown ids fail closed (no line).
/// </summary>
public static class StoryGuideCopy
{
  public const string ObjectivePrefix = "当前目标：";

  public const string SandboxObjective = "自由探索这片海域。";

  /// <summary>Character-voiced one-liners when a main-story quest becomes current.</summary>
  public static readonly IReadOnlyDictionary<string, string> QuestStartLines =
    new Dictionary<string, string>
    {
      ["quest_radio"] = "醒了？岸边那座塔在响。去看看。",
      ["quest_wood"] = "先活下来。附近有树，砍五根木头。",
      ["quest_campfire"] = "夜里会冷。生一堆火。",
      ["quest_cooked_meat"] = "生的吃不得。用火把肉烤熟。",
      ["quest_harvest"] = "地里能养活人。收三茬，肚子才稳。",
      ["quest_ruin"] = "林子里有旧石头。去读那些刻痕。",
      ["quest_mutant"] = "那些……不是野兽。小心，但别逃。",
      ["quest_boss"] = "风暴在等你。造木筏，出海。"
    };

  /// <summary>
  ///   Flavor for walk-in story points that do not already narrate via a
  ///   readable log. radio/ruin complete a quest in the same tick — skip those
  ///   so the next quest's start line is the one the player sees.
  /// </summary>
  public static readonly IReadOnlyDictionary<string, string> StoryPointLines =
    new Dictionary<string, string>
    {
      ["shark_king"] = "风暴里有呼吸。海域之主现身了。"
    };

  public static string FormatObjective(string objective, int done, int need)
  {
    if (need > 1)
      return $"{ObjectivePrefix}{objective}（{done}/{need}）";

    return $"{ObjectivePrefix}{objective}";
  }
}
