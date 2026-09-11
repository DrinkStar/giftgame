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

  /// <summary>
  ///   尾声台词：杀鲨王后播，引导者最后一次响起，HUD 切沙盒自由探索。
  ///   不调 RaiseGameOver（沙盒结局）。接在 OnBossDefeated 的 BossLine
  ///   之后连播（GuideLine 是 subtitle 队列，连播可接受）。
  /// </summary>
  public const string EpilogueLine =
    "海域之主沉入深渊，风暴散去。引导者的信号最后一次响起：『海仍在那，活下去。』远方还有岛屿。";

  /// <summary>Character-voiced one-liners when a main-story quest becomes current.</summary>
  public static readonly IReadOnlyDictionary<string, string> QuestStartLines =
    new Dictionary<string, string>
    {
      ["quest_radio"] = "醒了？岸边那座塔在响。去看看。",
      ["quest_wood"] = "先活下来。附近有树，砍五根木头。",
      ["quest_campfire"] = "夜里会冷。生一堆火。",
      ["quest_cooked_meat"] = "生的吃不得。用火把肉烤熟。",
      ["quest_harvest"] =
        "种子在背包里。按 B 造农田，多种几格，收三茬肚子才稳。",
      ["quest_ruin"] =
        "造木筏出海。近环有座旧石岛——登上去，读完三段刻痕。",
      ["quest_mutant"] =
        "刻痕说得对。再往外，那片密林里有异化者。去猎一只，别逃。",
      ["quest_boss"] =
        "箭和干粮已备好。风暴在等你——造木筏，出海。"
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
