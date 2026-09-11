// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   GuideService narration tests (iter7-plan Decision 5): event → GuideLine
///   text mapping (including the PeriodChanged Night case), and the boss
///   double-fire dedup — EnemyDied + BossDefeated in the same tick narrate
///   only the boss line.
/// </summary>
public class GuideServiceTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private GuideService _guide = default!;

  public GuideServiceTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked nodes from earlier test classes. GoDotTest's fixture
    // cleanup does not free nodes deterministically between classes
    // (GC/frame-timing dependent), so a leftover Game scene keeps its
    // GuideService/QuestService subscribed to the static bus and pollutes
    // exact-count assertions below (extra lines per event).
    SweepLeakedNodes(TestScene.GetTree().Root);

    _guide = new GuideService { Name = "GuideService" };
    _fixture.AddToRoot(_guide, autoRemoveFromRoot: true);

    // Static lock can leak from MainMenuUI / CraftUI tests.
    GameEvents.RaiseGameplayInputLockChanged(false);
  }

  /// <summary>
  ///   Removes and frees leftover nodes from earlier test classes (see
  ///   <see cref="Setup"/>). Everything except the test runner scene is test
  ///   garbage; freeing it synchronously runs _ExitTree so its static-bus
  ///   subscriptions die before this case raises events.
  /// </summary>
  private void SweepLeakedNodes(Node root)
  {
    var current = TestScene.GetTree().CurrentScene;
    foreach (var child in root.GetChildren())
    {
      if (ReferenceEquals(child, TestScene) || ReferenceEquals(child, current))
        continue;

      if (child.Name == "Main")
        continue;

      root.RemoveChild(child);
      child.Free();
    }
  }

  [Cleanup]
  public void Cleanup()
  {
    // Detach synchronously BEFORE Fixture.Cleanup: _ExitTree unsubscribes the
    // static GameEvents bus immediately. GoDotTest runs the next test in the
    // same frame — queued frees would leak this guide's subscriptions into it.
    if (_guide.IsInsideTree())
      _guide.GetParent()!.RemoveChild(_guide);

    _fixture.Cleanup();
    GameEvents.RaiseGameplayInputLockChanged(false);
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_guide == null)
      return;

    _guide.Dispose();
    _guide = null!;
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///   EnemyDied narrates "野兽逼近了…" after the 0.5s boss-dedup window.
  /// </summary>
  [Test]
  public void EnemyDiedEmitsBeastLineAfterWindow()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseEnemyDied("wolf");

      // Deferred by the dedup window — nothing narrates yet.
      lines.ShouldBeEmpty();

      _guide._Process(0.6);

      lines.Count.ShouldBe(1);
      lines[0].ShouldBe("野兽逼近了…");
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   PeriodChanged → Night narrates the night warning; Day stays silent.
  /// </summary>
  [Test]
  public void PeriodChangedNightEmitsNightWarning()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaisePeriodChanged(DayPeriod.Day);
      lines.ShouldBeEmpty();

      GameEvents.RaisePeriodChanged(DayPeriod.Night);

      lines.Count.ShouldBe(1);
      lines[0].ShouldBe("夜晚危险，小心野兽。");
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   Boss double-fire dedup: EnemyBase.Die raises EnemyDied then
  ///   BossDefeated in the same tick — only the boss line narrates, even
  ///   after the dedup window has fully passed.
  /// </summary>
  [Test]
  public void BossDoubleFireDedupedToSingleBossLine()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseEnemyDied("shark_king");
      GameEvents.RaiseBossDefeated("shark_king");

      _guide._Process(1.0); // far past the dedup window

      lines.Count.ShouldBe(1);
      lines[0].ShouldBe("海域之主的时代…结束了。");
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   quest_boss 是主线终章：完成后播 EpilogueLine（沙盒结局），
  ///   不调 RaiseGameOver；其他 quest 仍播 NewGoalLine。
  /// </summary>
  [Test]
  public void QuestCompletedBossEmitsEpilogueLine()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseQuestCompleted("quest_boss");
      lines.Count.ShouldBe(1);
      lines[0].ShouldBe(StoryGuideCopy.EpilogueLine);

      GameEvents.RaiseQuestCompleted("quest_wood");
      lines.Count.ShouldBe(2);
      lines[1].ShouldBe("新的目标出现了。");
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   The remaining Decision 5 mappings: CropHarvested, PlayerDied and
  ///   QuestCompleted narrate their lines immediately and in order.
  /// </summary>
  [Test]
  public void SurvivalAndQuestLinesMapToGuideLine()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseCropHarvested("potato");
      GameEvents.RaisePlayerDied();
      GameEvents.RaiseQuestCompleted("quest_wood");

      lines.Count.ShouldBe(3);
      lines[0].ShouldBe("收成不错。");
      lines[1].ShouldBe("回到岸边，活下去。");
      lines[2].ShouldBe("新的目标出现了。");
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   Main-story QuestStarted lines stay quiet until TutorialCompleted
  ///   (开始游戏 / 新手教程 both raise it; chapter-1 cards must not overlap).
  /// </summary>
  [Test]
  public void QuestStartedWaitsForTutorialCompletedThenSpeaks()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseQuestStarted("quest_radio");
      lines.ShouldBeEmpty();

      GameEvents.RaiseTutorialCompleted();

      lines.Count.ShouldBe(1);
      lines[0].ShouldBe(StoryGuideCopy.QuestStartLines["quest_radio"]);
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   Unknown quest ids fail closed; known ids speak after the story is ready.
  ///   Input lock queues the line until the menu / modal releases.
  /// </summary>
  [Test]
  public void QuestStartedUnknownSilentAndLockQueuesLine()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseTutorialCompleted();
      GameEvents.RaiseQuestStarted("quest_not_a_real_id");
      lines.ShouldBeEmpty();

      GameEvents.RaiseGameplayInputLockChanged(true);
      GameEvents.RaiseQuestStarted("quest_wood");
      lines.ShouldBeEmpty();

      GameEvents.RaiseGameplayInputLockChanged(false);
      lines.Count.ShouldBe(1);
      lines[0].ShouldBe(StoryGuideCopy.QuestStartLines["quest_wood"]);
    }
    finally
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      GameEvents.GuideLine -= onLine;
    }
  }

  /// <summary>
  ///   Chapter-1 (and later) tutorial cards mute story quest lines until
  ///   TutorialCompleted; shark_king story point narrates, radio does not
  ///   (it completes a quest the same tick).
  /// </summary>
  [Test]
  public void TutorialCardMutesQuestLineAndSharkKingStoryPointSpeaks()
  {
    var lines = new List<string>();
    Action<string> onLine = text => lines.Add(text);
    GameEvents.GuideLine += onLine;
    try
    {
      GameEvents.RaiseTutorialCompleted();
      GameEvents.RaiseTutorialStepChanged(1, 6);
      GameEvents.RaiseQuestStarted("quest_campfire");
      lines.ShouldBeEmpty();

      GameEvents.RaiseTutorialCompleted();
      lines.Count.ShouldBe(1);
      lines[0].ShouldBe(StoryGuideCopy.QuestStartLines["quest_campfire"]);

      GameEvents.RaiseStoryPointReached("radio");
      GameEvents.RaiseStoryPointReached("ruin");
      lines.Count.ShouldBe(1);

      GameEvents.RaiseStoryPointReached("shark_king");
      lines.Count.ShouldBe(2);
      lines[1].ShouldBe(StoryGuideCopy.StoryPointLines["shark_king"]);
    }
    finally
    {
      GameEvents.GuideLine -= onLine;
    }
  }

  [Test]
  public void FormatObjectiveIncludesCountOnlyWhenNeedExceedsOne()
  {
    StoryGuideCopy.FormatObjective("收集 5 个木头", 2, 5)
      .ShouldBe("当前目标：收集 5 个木头（2/5）");
    StoryGuideCopy.FormatObjective("抵达遗迹", 0, 1)
      .ShouldBe("当前目标：抵达遗迹");
  }
}
