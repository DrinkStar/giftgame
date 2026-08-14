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

  public override void _Ready()
  {
    GameEvents.EnemyDied += OnEnemyDied;
    GameEvents.BossDefeated += OnBossDefeated;
    GameEvents.CropHarvested += OnCropHarvested;
    GameEvents.PlayerDied += OnPlayerDied;
    GameEvents.QuestCompleted += OnQuestCompleted;
    GameEvents.PeriodChanged += OnPeriodChanged;
  }

  public override void _ExitTree()
  {
    GameEvents.EnemyDied -= OnEnemyDied;
    GameEvents.BossDefeated -= OnBossDefeated;
    GameEvents.CropHarvested -= OnCropHarvested;
    GameEvents.PlayerDied -= OnPlayerDied;
    GameEvents.QuestCompleted -= OnQuestCompleted;
    GameEvents.PeriodChanged -= OnPeriodChanged;
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

  private void OnQuestCompleted(string questId) =>
    GameEvents.RaiseGuideLine(NewGoalLine);

  private void OnPeriodChanged(DayPeriod period)
  {
    if (period == DayPeriod.Night)
      GameEvents.RaiseGuideLine(NightLine);
  }
}
