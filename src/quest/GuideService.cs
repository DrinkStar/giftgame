// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   The narrator ("guide") — a voice without a body (plan Decision 5: no
///   audio assets this iteration, so narration travels as
///   <see cref="GameEvents.GuideLine"/> text consumed by the HUD subtitle).
///
///   Line mapping (plan Decision 5):
///   - EnemyDied → "野兽逼近了…"
///   - BossDefeated → "海域之主的时代…结束了。"
///   - CropHarvested → "收成不错。"
///   - PlayerDied → "回到岸边，活下去。"
///   - QuestCompleted → "新的目标出现了。"
///   - PeriodChanged(Night) → "夜晚危险，小心野兽。"
///     (PeriodChanged, NOT DayChanged — the latter is a midnight day-number
///     rollover event.)
///
///   Main-story through-line (non-intrusive):
///   - QuestStarted → <see cref="StoryGuideCopy.QuestStartLines"/> (queued
///     until TutorialCompleted and the gameplay lock is down, so 开始游戏
///     and 新手教程 both see the line after the menu dismisses, and chapter-1
///     tutorial cards are not overlapped).
///   - StoryPointReached → <see cref="StoryGuideCopy.StoryPointLines"/>
///     (shark_king only; radio/ruin complete a quest the same tick).
///   Never calls GetTree().Paused. Missing copy for an id is fail-closed.
///
///   Boss double-fire dedup: EnemyBase.Die raises EnemyDied and then
///   BossDefeated in the same tick. The beast line is therefore deferred by
///   the 0.5s window and cancelled when the boss line lands first, so a boss
///   kill narrates exactly once. The last-boss-line timestamp also guards the
///   reverse order.
/// </summary>
public partial class GuideService : Node
{
  private const string EnemyLine = "野兽逼近了…";
  private const string BossLine = "海域之主的时代…结束了。";
  private const string HarvestLine = "收成不错。";
  private const string DeathLine = "回到岸边，活下去。";
  private const string NewGoalLine = "新的目标出现了。";
  private const string NightLine = "夜晚危险，小心野兽。";

  /// <summary>Boss-dedup window in seconds (plan Decision 5).</summary>
  private const double DedupWindowSeconds = 0.5;

  /// <summary>Accumulated process time; deterministic in tests (no wall clock).</summary>
  private double _time;

  private double _lastBossLineAt = double.NegativeInfinity;
  private bool _enemyLinePending;
  private double _enemyLineAt;

  /// <summary>
  ///   Set by TutorialCompleted (开始游戏 skips chapter 1 with this event;
  ///   新手教程 raises it when the 6-step card finishes). Story quest lines
  ///   stay quiet until then so they do not fire under the main menu.
  /// </summary>
  private bool _storyReady;

  /// <summary>True while a tutorial step card is on screen (TutorialStepChanged).</summary>
  private bool _tutorialOverlayQuiet;

  private string? _pendingQuestId;
  private string? _spokenQuestId;

  public override void _Ready()
  {
    GameEvents.EnemyDied += OnEnemyDied;
    GameEvents.BossDefeated += OnBossDefeated;
    GameEvents.CropHarvested += OnCropHarvested;
    GameEvents.PlayerDied += OnPlayerDied;
    GameEvents.QuestCompleted += OnQuestCompleted;
    GameEvents.PeriodChanged += OnPeriodChanged;
    GameEvents.QuestStarted += OnQuestStarted;
    GameEvents.StoryPointReached += OnStoryPointReached;
    GameEvents.TutorialCompleted += OnTutorialCompleted;
    GameEvents.TutorialStepChanged += OnTutorialStepChanged;
    GameEvents.GameplayInputLockChanged += OnGameplayInputLockChanged;

    // QuestService sits earlier in Game.tscn and already started quest_radio
    // before this node subscribed — pull the current id fail-closed.
    var quests = GetParent()?.GetNodeOrNull<QuestService>("QuestService");
    if (quests?.CurrentQuestId is { Length: > 0 } currentId)
      _pendingQuestId = currentId;
  }

  public override void _ExitTree()
  {
    GameEvents.EnemyDied -= OnEnemyDied;
    GameEvents.BossDefeated -= OnBossDefeated;
    GameEvents.CropHarvested -= OnCropHarvested;
    GameEvents.PlayerDied -= OnPlayerDied;
    GameEvents.QuestCompleted -= OnQuestCompleted;
    GameEvents.PeriodChanged -= OnPeriodChanged;
    GameEvents.QuestStarted -= OnQuestStarted;
    GameEvents.StoryPointReached -= OnStoryPointReached;
    GameEvents.TutorialCompleted -= OnTutorialCompleted;
    GameEvents.TutorialStepChanged -= OnTutorialStepChanged;
    GameEvents.GameplayInputLockChanged -= OnGameplayInputLockChanged;
  }

  public override void _Process(double delta)
  {
    _time += delta;

    if (_enemyLinePending && _time - _enemyLineAt >= DedupWindowSeconds)
    {
      _enemyLinePending = false;
      GameEvents.RaiseGuideLine(EnemyLine);
    }
  }

  private void OnEnemyDied(string enemyId)
  {
    // If a boss line just fired, dedup the beast line entirely (defensive:
    // covers any BossDefeated-before-EnemyDied ordering).
    if (_time - _lastBossLineAt <= DedupWindowSeconds)
      return;

    if (_enemyLinePending)
      return;

    _enemyLinePending = true;
    _enemyLineAt = _time;
  }

  private void OnBossDefeated(string enemyId)
  {
    // Same-tick EnemyDied + BossDefeated → only the boss line narrates.
    _enemyLinePending = false;
    _lastBossLineAt = _time;
    GameEvents.RaiseGuideLine(BossLine);
  }

  private void OnCropHarvested(string cropId) =>
    GameEvents.RaiseGuideLine(HarvestLine);

  private void OnPlayerDied() => GameEvents.RaiseGuideLine(DeathLine);

  private void OnQuestCompleted(string questId)
  {
    // quest_boss 是主线终章：杀鲨王后播尾声台词，HUD 切沙盒自由探索，
    // 不调 RaiseGameOver（沙盒结局）。OnBossDefeated 已播 BossLine
    // 「海域之主的时代…结束了。」；EpilogueLine 接在后面连播
    // （GuideLine 是 subtitle 队列，连播可接受）。
    if (questId == "quest_boss")
      GameEvents.RaiseGuideLine(StoryGuideCopy.EpilogueLine);
    else
      GameEvents.RaiseGuideLine(NewGoalLine);
  }

  private void OnPeriodChanged(DayPeriod period)
  {
    if (period == DayPeriod.Night)
      GameEvents.RaiseGuideLine(NightLine);
  }

  private void OnQuestStarted(string questId)
  {
    if (string.IsNullOrEmpty(questId))
      return;

    _pendingQuestId = questId;
    TrySpeakPendingQuest();
  }

  private void OnStoryPointReached(string storyPointId)
  {
    if (!CanSpeakStory())
      return;

    if (!StoryGuideCopy.StoryPointLines.TryGetValue(storyPointId, out var line))
      return;

    GameEvents.RaiseGuideLine(line);
  }

  private void OnTutorialCompleted()
  {
    _storyReady = true;
    _tutorialOverlayQuiet = false;
    TrySpeakPendingQuest();
  }

  private void OnTutorialStepChanged(int _currentStep, int _totalSteps) =>
    _tutorialOverlayQuiet = true;

  private void OnGameplayInputLockChanged(bool locked)
  {
    if (!locked)
      TrySpeakPendingQuest();
  }

  private void TrySpeakPendingQuest()
  {
    if (_pendingQuestId == null || _pendingQuestId == _spokenQuestId)
      return;

    if (!CanSpeakStory())
      return;

    if (!StoryGuideCopy.QuestStartLines.TryGetValue(_pendingQuestId, out var line))
      return;

    _spokenQuestId = _pendingQuestId;
    GameEvents.RaiseGuideLine(line);
  }

  /// <summary>
  ///   Story through-line stays quiet under the main menu (input lock),
  ///   before TutorialCompleted, and while a tutorial step card is visible.
  /// </summary>
  private bool CanSpeakStory() =>
    _storyReady && !_tutorialOverlayQuiet && !GameEvents.GameplayInputLocked;
}
