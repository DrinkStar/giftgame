// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using dotnetquestsystem;
using Godot;

/// <summary>
///   Bridges <see cref="GameEvents"/> to the vendored DotnetQuestSystem
///   library (src/quest/lib, namespace <c>dotnetquestsystem</c> — plan
///   Decision 2: the NuGet package targets net9.0 and cannot be referenced
///   from this net8.0 project, so the api/ sources are vendored verbatim).
///
///   The library has no progress counting and its <c>ItemReward&lt;string&gt;</c>
///   carries an item Id only (no amount), so this service owns both
///   (plan Decisions 3+4):
///   - <see cref="_progress"/> counts condition events per quest;
///   - <see cref="RewardAmounts"/> is the item-Id → entries table
///     consulted when a quest completes (no Contains matching, review-frozen;
///     each quest may grant one or more item stacks);
///   - the library <c>ItemReward&lt;string&gt;</c> stores the primary reward
///     item Id for documentation parity only.
///
///   Three chapters are defined in code (plan Decision 6): chapter 1
///   (radio → wood ×5 → campfire → cooked meat), chapter 2 (the anomaly:
///   harvest ×3 → ruin → mutant), chapter 3 (shark king). Quests run
///   strictly in order inside a chapter; the next chapter starts after the
///   last quest of the current one. The node sits AFTER the Player subtree
///   in Game.tscn so the starting inventory's <see cref="GameEvents.ItemAdded"/>
///   (raised by the Player subtree _Ready) cannot count toward quest_wood.
///
  ///   T8.5.9: quest_radio ("联系引导者", completed by the radio story point)
  ///   opens chapter 1; the shark_king story point is a chapter-3 beat
  ///   (GuideService narrates; quest_boss stays BossDefeated-driven).
///
///   PlayerDied is intentionally NOT subscribed (frozen contract: death is
///   never a quest failure — T7.0 respawn).
/// </summary>
public partial class QuestService : Node
{
  /// <summary>Path to the player CharacterBody3D whose InventorySystem receives rewards.</summary>
  [Export] public NodePath PlayerPath { get; set; } = new();

  /// <summary>
  ///   Condition targets per quest Id (plan Decision 4 table).
  /// </summary>
  private static readonly Dictionary<string, int> Targets = new()
  {
    ["quest_radio"] = 1,
    ["quest_wood"] = 5,
    ["quest_campfire"] = 1,
    ["quest_cooked_meat"] = 1,
    ["quest_harvest"] = 3,
    ["quest_ruin"] = 1,
    ["quest_mutant"] = 1,
    ["quest_boss"] = 1
  };

  /// <summary>
  ///   Reward stacks per quest Id (plan Decision 3 — the library's ItemReward
  ///   carries only the primary Id, never amounts). Most quests grant one
  ///   stack; quest_mutant grants a pre-boss kit of several stacks.
  /// </summary>
  private static readonly Dictionary<string, (string ItemId, int Amount)[]>
    RewardAmounts = new()
    {
      // T8.5.9: chapter-1 opener — wood ×5 for contacting the guide.
      ["quest_radio"] = new[] { ("wood", 5) },
      ["quest_wood"] = new[] { ("wood", 3) },
      ["quest_campfire"] = new[] { ("berries", 5) },
      // Chapter 2 opener: wheat seeds so Main farmland is never soft-locked.
      ["quest_cooked_meat"] = new[] { ("wheat_seed", 5) },
      ["quest_harvest"] = new[] { ("cooked_meat", 1) },
      ["quest_ruin"] = new[] { ("stone", 5) },
      // Chapter 3 prep kit before sailing to the shark king.
      ["quest_mutant"] = new[]
      {
        ("arrow", 20),
        ("cooked_meat", 3),
        ("cloth_armor", 1)
      },
      ["quest_boss"] = new[] { ("berries", 10) }
    };

  /// <summary>Three chapters, each a strictly ordered quest list (Decision 6).</summary>
  private List<Quest>[] _chapters = System.Array.Empty<List<Quest>>();

  private readonly Dictionary<string, int> _progress = new();

  private int _chapterIdx;
  private int _questIdx;
  private Quest? _currentQuest;
  private InventorySystem? _inventory;

  /// <summary>Active quest id, or null when the chapter chain is finished / not started.</summary>
  public string? CurrentQuestId => _currentQuest?.Name;

  /// <summary>Chinese objective of the active quest, or null when none.</summary>
  public string? CurrentQuestObjective => _currentQuest?.Objective;

  /// <summary>
  ///   Fail-closed snapshot for the HUD tracker. Returns false when no quest
  ///   is current (missing service, finished chain).
  /// </summary>
  public bool TryGetCurrentTracker(
    out string questId, out string objective, out int done, out int need
  )
  {
    if (_currentQuest == null)
    {
      questId = "";
      objective = "";
      done = 0;
      need = 0;
      return false;
    }

    questId = _currentQuest.Name;
    objective = _currentQuest.Objective;
    _progress.TryGetValue(questId, out done);
    need = Targets.TryGetValue(questId, out var target) ? target : 1;
    return true;
  }

  public override void _Ready()
  {
    BuildChapters();
    _inventory = ResolveInventory();

    GameEvents.EnemyDied += OnEnemyDied;
    GameEvents.BossDefeated += OnBossDefeated;
    GameEvents.ItemAdded += OnItemAdded;
    GameEvents.CropHarvested += OnCropHarvested;
    GameEvents.CraftingCompleted += OnCraftingCompleted;
    GameEvents.BuildingPlaced += OnBuildingPlaced;
    GameEvents.StoryPointReached += OnStoryPointReached;

    // PlayerDied is deliberately NOT subscribed — death must never fail a
    // quest (iter6-code-review frozen contract, T7.0 respawn).

    if (_chapters.Length > 0 && _chapters[0].Count > 0)
      StartQuest(_chapters[0][0]);
  }

  public override void _ExitTree()
  {
    GameEvents.EnemyDied -= OnEnemyDied;
    GameEvents.BossDefeated -= OnBossDefeated;
    GameEvents.ItemAdded -= OnItemAdded;
    GameEvents.CropHarvested -= OnCropHarvested;
    GameEvents.CraftingCompleted -= OnCraftingCompleted;
    GameEvents.BuildingPlaced -= OnBuildingPlaced;
    GameEvents.StoryPointReached -= OnStoryPointReached;
  }

  /// <summary>
  ///   Defines the three chapters via QuestController.CreateQuest. Assumes a
  ///   cleared database (tests clear QuestManager.instance.questDatabase in
  ///   Setup — Decision 8 singleton-pollution guard).
  /// </summary>
  private void BuildChapters()
  {
    var controller = QuestManager.instance.questController;
    _chapters = new List<Quest>[]
    {
      new()
      {
        // T8.5.9: chapter 1 opens with the radio quest — the story
        // interactable at the radio tower completes it before any wood
        // gathering starts (quest_radio MUST precede quest_wood).
        controller.CreateQuest(
          "quest_radio", "联系引导者", "抵达无线电塔并阅读日志", new ItemReward<string>("wood")
        ),
        controller.CreateQuest(
          "quest_wood", "收集木材", "收集 5 个木头", new ItemReward<string>("wood")
        ),
        controller.CreateQuest(
          "quest_campfire", "生火取暖", "建造一个篝火", new ItemReward<string>("berries")
        ),
        controller.CreateQuest(
          "quest_cooked_meat", "烹饪熟食", "烤制熟肉", new ItemReward<string>("wheat_seed")
        )
      },
      new()
      {
        controller.CreateQuest(
          "quest_harvest", "农耕", "收获 3 次作物", new ItemReward<string>("cooked_meat")
        ),
        controller.CreateQuest(
          "quest_ruin", "探索遗迹", "抵达遗迹", new ItemReward<string>("stone")
        ),
        controller.CreateQuest(
          "quest_mutant", "狩猎异化者", "击败一个异化者", new ItemReward<string>("arrow")
        )
      },
      new()
      {
        controller.CreateQuest(
          "quest_boss", "海域之主", "击败鲨鱼王", new ItemReward<string>("berries")
        )
      }
    };
  }

  private InventorySystem? ResolveInventory()
  {
    if (PlayerPath.IsEmpty)
      return null;

    var player = GetNodeOrNull<CharacterBody3D>(PlayerPath);
    return player?.GetNodeOrNull<InventorySystem>("InventorySystem");
  }

  private bool IsCurrent(string questId) =>
    _currentQuest != null && _currentQuest.Name == questId;

  private void StartQuest(Quest quest)
  {
    _currentQuest = quest;
    QuestManager.instance.StartQuest(quest);
    _progress[quest.Name] = 0;
    GameEvents.RaiseQuestStarted(quest.Name);
    var need = Targets.TryGetValue(quest.Name, out var target) ? target : 1;
    GameEvents.RaiseQuestProgress(quest.Name, 0, need);
  }

  private void CompleteCurrentQuest()
  {
    if (_currentQuest == null)
      return;

    var quest = _currentQuest;

    // Re-entrancy guard: detach the quest from the active slot BEFORE
    // FinishQuest/granting. The reward's own ItemAdded (quest_wood grants
    // wood, and InventorySystem.AddItem re-raises ItemAdded) would otherwise
    // re-enter HandleProgress while this quest is still "current" and
    // fast-forward the whole chapter chain past the end of the array.
    _currentQuest = null;

    QuestManager.instance.FinishQuest(quest);
    GrantReward(quest.Name);
    GameEvents.RaiseQuestCompleted(quest.Name);
    _progress.Remove(quest.Name);

    _questIdx++;
    if (_questIdx >= _chapters[_chapterIdx].Count)
    {
      _chapterIdx++;
      _questIdx = 0;
      if (_chapterIdx >= _chapters.Length)
      {
        // Every chapter done; no further quest to start.
        return;
      }
    }

    StartQuest(_chapters[_chapterIdx][_questIdx]);
  }

  /// <summary>
  ///   Grants each completion reward stack by item Id + amount (no Contains
  ///   matching; each .tres must exist under assets/items/). Missing entries
  ///   are skipped with a warning so a bad row does not block the rest.
  /// </summary>
  private void GrantReward(string questId)
  {
    if (!RewardAmounts.TryGetValue(questId, out var entries))
    {
      GD.PushWarning($"QuestService: no reward table entry for '{questId}'.");
      return;
    }

    if (_inventory == null)
      return;

    foreach (var (itemId, amount) in entries)
    {
      var item = GD.Load<ItemData>($"res://assets/items/{itemId}.tres");
      if (item == null)
      {
        GD.PushWarning(
          $"QuestService: reward item '{itemId}' missing at res://assets/items/{itemId}.tres."
        );
        continue;
      }

      _inventory.AddItem(item, amount);
    }
  }

  private void HandleProgress(string questId, int target, int increment = 1)
  {
    if (!IsCurrent(questId))
      return;

    _progress.TryGetValue(questId, out var done);
    done += increment;
    _progress[questId] = done;
    GameEvents.RaiseQuestProgress(questId, done, target);

    if (done >= target)
      CompleteCurrentQuest();
  }

  private void OnItemAdded(string itemId, int amount)
  {
    if (itemId == "wood")
      HandleProgress("quest_wood", Targets["quest_wood"], amount);
  }

  private void OnBuildingPlaced(string buildableName)
  {
    if (buildableName == "campfire")
      HandleProgress("quest_campfire", Targets["quest_campfire"]);
  }

  private void OnCraftingCompleted(string recipeId)
  {
    if (recipeId == "cooked_meat")
      HandleProgress("quest_cooked_meat", Targets["quest_cooked_meat"]);
  }

  private void OnCropHarvested(string cropId) =>
    HandleProgress("quest_harvest", Targets["quest_harvest"]);

  /// <summary>
  ///   T8.5.9: story points drive quests — "ruin" advances quest_ruin
  ///   (Iter7), "radio" completes the new chapter-1 opener quest_radio.
  ///   "shark_king" is narrated by GuideService; quest_boss stays
  ///   BossDefeated-driven so the existing chapter flow is untouched.
  /// </summary>
  private void OnStoryPointReached(string storyPointId)
  {
    switch (storyPointId)
    {
      case "ruin":
        HandleProgress("quest_ruin", Targets["quest_ruin"]);
        break;
      case "radio":
        HandleProgress("quest_radio", Targets["quest_radio"]);
        break;
    }
  }

  private void OnEnemyDied(string enemyId)
  {
    if (enemyId == "mutant")
      HandleProgress("quest_mutant", Targets["quest_mutant"]);
  }

  private void OnBossDefeated(string enemyId)
  {
    if (enemyId == "shark_king")
      HandleProgress("quest_boss", Targets["quest_boss"]);
  }
}
