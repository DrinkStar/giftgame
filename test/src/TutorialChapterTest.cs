// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   Iter8.5 (T8.5.10/T8.5.11) forced-tutorial tests, split into two classes
///   in one file (test/src/TutorialChapterTest.cs):
///   - <see cref="TutorialFlowParameterizedTest"/>: pure state-machine logic
///     for the parameterized constructor (2-step chapter2, 1-step chapter3)
///     and the fail-fast step-count validation. No nodes.
///   - <see cref="TutorialChapterTest"/>: TutorialUI assembled with a fake
///     player (mirroring TutorialStateTest's TutorialUITest pattern), driven
///     through the REAL chapter triggers (QuestStarted("quest_harvest") for
///     chapter2, StoryPointReached("shark_king") for chapter3) and the real
///     step events (CropPlanted / StoryPointReached("ruin") / sail distance).
///
///   Chapter gating: chapter2/3 triggers only fire after chapter1 completed,
///   so each sequence test first finishes chapter1 via <see cref="SkipTutorial"/>
///   (the same public debug/test shortcut TutorialStateTest exercises).
///
///   Static-event discipline: every GameEvents subscription is released in a
///   finally block (or by synchronous node removal in Cleanup, which runs
///   _ExitTree's unsubscribes); Input.MouseMode is restored in Cleanup.
/// </summary>
public class TutorialFlowParameterizedTest : TestClass
{
  public TutorialFlowParameterizedTest(Node testScene) : base(testScene) { }

  /// <summary>
  ///   T8.5.10: a 2-step flow (chapter2: plant → read log) constructs
  ///   legally, reports StepCount 2, and completes after two forced
  ///   advances (CurrentStep 3).
  /// </summary>
  [Test]
  public void TwoStepFlowIsLegal()
  {
    var flow = new TutorialFlow(
      2,
      new TutorialFlow.Step("plant", null),
      new TutorialFlow.Step("read_log", null)
    );

    flow.StepCount.ShouldBe(2);
    flow.Steps.Count.ShouldBe(2);
    flow.IsComplete.ShouldBeFalse();

    flow.Start();
    flow.CurrentStep.ShouldBe(1);

    flow.CompleteCurrentStep();
    flow.CurrentStep.ShouldBe(2);
    flow.IsComplete.ShouldBeFalse();

    flow.CompleteCurrentStep();
    flow.IsComplete.ShouldBeTrue();
    flow.CurrentStep.ShouldBe(3);
  }

  /// <summary>
  ///   T8.5.11: a 1-step flow (chapter3: sail) constructs legally, reports
  ///   StepCount 1, and completes after a single forced advance.
  /// </summary>
  [Test]
  public void OneStepFlowIsLegal()
  {
    var flow = new TutorialFlow(1, new TutorialFlow.Step("sail", null));

    flow.StepCount.ShouldBe(1);
    flow.Steps.Count.ShouldBe(1);

    flow.Start();
    flow.CompleteCurrentStep();

    flow.IsComplete.ShouldBeTrue();
    flow.CurrentStep.ShouldBe(2);
  }

  /// <summary>
  ///   Fail-fast validation: the step count must match the constructor's
  ///   totalSteps exactly — fewer or more steps both throw.
  /// </summary>
  [Test]
  public void StepCountMismatchThrows()
  {
    Should.Throw<ArgumentException>(
      () => new TutorialFlow(2, new TutorialFlow.Step("only", null))
    );

    Should.Throw<ArgumentException>(
      () => new TutorialFlow(
        1,
        new TutorialFlow.Step("a", null),
        new TutorialFlow.Step("b", null)
      )
    );
  }

  /// <summary>
  ///   The 6-step params constructor (TutorialStateTest's contract) is
  ///   unchanged: it delegates to the parameterized constructor with
  ///   <see cref="TutorialFlow.TotalSteps"/>.
  /// </summary>
  [Test]
  public void DefaultSixStepConstructorStillWorks()
  {
    var flow = new TutorialFlow(
      new TutorialFlow.Step("move", null),
      new TutorialFlow.Step("gather", null),
      new TutorialFlow.Step("drink", null),
      new TutorialFlow.Step("campfire", null),
      new TutorialFlow.Step("bed", null),
      new TutorialFlow.Step("combat", null)
    );

    flow.StepCount.ShouldBe(TutorialFlow.TotalSteps);
    flow.StepCount.ShouldBe(6);
    flow.Steps.Count.ShouldBe(6);
  }
}

/// <summary>
///   Scene-level tests: assemble a TutorialUI with a fake player and drive
///   the real chapter triggers + step events (no real input, no scene file).
/// </summary>
public class TutorialChapterTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private TutorialUI _ui = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;

  public TutorialChapterTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Gravity off so the bare player never drifts while the tutorial runs
    // (there is no ground in this fixture); movement is teleported only.
    _player = new PlayerController { Name = "Player", Gravity = 0f };
    _stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(_stats);
    _player.Stats = _stats;
    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);

    _ui = new TutorialUI
    {
      Name = "TutorialUI",
      Player = _player,
      Stats = _stats
    };
    await _fixture.AddToRoot(_ui, autoRemoveFromRoot: true);

    // _Ready CallDeferred'd StartTutorial (chapter1); let the deferred call
    // run so the tutorial is active before the first assertion.
    await TestScene.ProcessFrame(2);
  }

  [Cleanup]
  public void Cleanup()
  {
    // The tutorial changes Input.MouseMode per step — always restore it.
    Input.MouseMode = Input.MouseModeEnum.Captured;

    // Detach synchronously BEFORE Fixture.Cleanup so _ExitTree unsubscribes
    // the static GameEvents bus immediately (GuideServiceTest precedent).
    if (_ui != null && _ui.IsInsideTree() && _ui.GetParent() != null)
      _ui.GetParent()!.RemoveChild(_ui);

    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_ui == null && _player == null)
      return;

    _ui?.Dispose();
    _ui = null!;
    _player?.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///   Completes chapter1 via the public debug shortcut (raises
  ///   TutorialCompleted once, sets the internal chapter1-done gate the
  ///   chapter2/3 triggers require).
  /// </summary>
  private void FinishChapter1() => _ui.SkipTutorial();

  /// <summary>
  ///   T8.5.10 chapter2 sequence: after chapter1 completed,
  ///   QuestStarted("quest_harvest") activates the 2-step farming tutorial —
  ///   step1 completes on any CropPlanted, step2 on StoryPointReached("ruin")
  ///   — and finishing raises TutorialCompleted exactly once (chapter2's).
  /// </summary>
  [Test]
  public void ChapterTwoStartsOnHarvestQuestAndCompletesOnCropAndLog()
  {
    FinishChapter1();
    _ui.IsTutorialActive.ShouldBeFalse();

    var completed = 0;
    Action onDone = () => completed++;
    GameEvents.TutorialCompleted += onDone;
    try
    {
      // Trigger: the harvest quest starts chapter2's farming tutorial.
      GameEvents.RaiseQuestStarted("quest_harvest");
      _ui.IsTutorialActive.ShouldBeTrue();
      _ui.CurrentStep.ShouldBe(1);

      var title = _ui.GetNodeOrNull<Label>("StepCard/VBox/Title");
      title.ShouldNotBeNull();
      title!.Text.ShouldBe("种一格农田");

      // Step1: any CropPlanted completes the plant step.
      GameEvents.RaiseCropPlanted("potato");
      _ui.CurrentStep.ShouldBe(2);

      // Step2: reading the ruin log completes chapter2.
      GameEvents.RaiseStoryPointReached("ruin");
      _ui.IsTutorialActive.ShouldBeFalse();
      _ui.CurrentStep.ShouldBe(3); // 2 steps + 1
      completed.ShouldBe(1);
    }
    finally
    {
      GameEvents.TutorialCompleted -= onDone;
    }
  }

  /// <summary>
  ///   The chapter2 trigger is gated on chapter1: while the chapter-1
  ///   tutorial is still running, QuestStarted("quest_harvest") does NOT
  ///   hijack it (CropPlanted must not advance the active chapter1 flow).
  /// </summary>
  [Test]
  public void ChapterTwoDoesNotStartBeforeChapterOneCompletes()
  {
    // Setup left chapter1 active at step 1.
    _ui.IsTutorialActive.ShouldBeTrue();
    _ui.CurrentStep.ShouldBe(1);

    GameEvents.RaiseQuestStarted("quest_harvest");

    // Still chapter1's flow: a crop-plant event must NOT advance it.
    _ui.IsTutorialActive.ShouldBeTrue();
    _ui.CurrentStep.ShouldBe(1);
    GameEvents.RaiseCropPlanted("potato");
    _ui.CurrentStep.ShouldBe(1);
  }

  /// <summary>
  ///   T8.5.11 chapter3 sequence: after chapter1 completed,
  ///   StoryPointReached("shark_king") activates the 1-step raft tutorial;
  ///   the sail step completes once the player is more than 15 m from the
  ///   world origin (no Raft reference involved).
  /// </summary>
  [Test]
  public void ChapterThreeStartsOnSharkKingStoryPointAndCompletesOnSail()
  {
    FinishChapter1();
    _ui.IsTutorialActive.ShouldBeFalse();

    var completed = 0;
    Action onDone = () => completed++;
    GameEvents.TutorialCompleted += onDone;
    try
    {
      // Trigger: reaching the shark-king story point starts the raft tutorial.
      GameEvents.RaiseStoryPointReached("shark_king");
      _ui.IsTutorialActive.ShouldBeTrue();
      _ui.CurrentStep.ShouldBe(1);

      var title = _ui.GetNodeOrNull<Label>("StepCard/VBox/Title");
      title.ShouldNotBeNull();
      title!.Text.ShouldBe("登上木筏驶向远海");

      // Still near the origin (player spawns at 0,0,0): not complete yet.
      _ui._Process(0.1);
      _ui.IsTutorialActive.ShouldBeTrue();
      _ui.CurrentStep.ShouldBe(1);

      // Sail > 15 m from the origin: step completes, tutorial finishes.
      _player.GlobalPosition = new Vector3(20f, 0f, 0f);
      _ui._Process(0.1);
      _ui.IsTutorialActive.ShouldBeFalse();
      _ui.CurrentStep.ShouldBe(2); // 1 step + 1
      completed.ShouldBe(1);
    }
    finally
    {
      GameEvents.TutorialCompleted -= onDone;
    }
  }

  /// <summary>
  ///   The chapter3 trigger is also gated on chapter1: the shark-king story
  ///   point is ignored while the chapter-1 tutorial is still running.
  /// </summary>
  [Test]
  public void ChapterThreeDoesNotStartBeforeChapterOneCompletes()
  {
    _ui.IsTutorialActive.ShouldBeTrue();

    GameEvents.RaiseStoryPointReached("shark_king");

    _ui.IsTutorialActive.ShouldBeTrue();
    _ui.CurrentStep.ShouldBe(1); // still chapter1 step1
  }

  /// <summary>
  ///   Chapter1 regression: the parameterless StartTutorial() (deferred from
  ///   _Ready) still runs the default 6-step sequence — active on step 1,
  ///   the card shows the move copy, and the first advance reports
  ///   (current=2, total=6) through TutorialStepChanged.
  /// </summary>
  [Test]
  public void DefaultStartTutorialRunsSixStepChapterOne()
  {
    _ui.IsTutorialActive.ShouldBeTrue();
    _ui.CurrentStep.ShouldBe(1);

    var title = _ui.GetNodeOrNull<Label>("StepCard/VBox/Title");
    title.ShouldNotBeNull();
    title!.Text.ShouldBe("移动");

    var steps = new List<(int Current, int Total)>();
    Action<int, int> onStep = (current, total) => steps.Add((current, total));
    GameEvents.TutorialStepChanged += onStep;
    try
    {
      // Step1 (move): displacement beyond 2 m from the step-start position.
      _player.GlobalPosition = new Vector3(3f, 0f, 0f);
      _ui._Process(0.1);

      _ui.CurrentStep.ShouldBe(2);
      steps.Count.ShouldBe(1);
      steps[0].Current.ShouldBe(2);
      steps[0].Total.ShouldBe(TutorialFlow.TotalSteps);
      steps[0].Total.ShouldBe(6);
    }
    finally
    {
      GameEvents.TutorialStepChanged -= onStep;
    }
  }
}
