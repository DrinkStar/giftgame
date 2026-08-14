// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8.3 forced new-player tutorial tests, split into two classes in one
///   file (test/src/TutorialStateTest.cs):
///   - <see cref="TutorialFlowTest"/>: pure state-machine logic, no nodes.
///   - <see cref="TutorialUITest"/>: TutorialUI assembled with a fake player
///     (new PlayerController + PlayerStats children, manual _Ready via the
///     engine), driven through real GameEvents raises — no real input and
///     no scene file, mirroring the FoodUseTest assembly pattern.
///
///   Static-event discipline: every GameEvents subscription is released in a
///   finally block; the bus is raise-only and stateless so nothing needs
///   resetting, but Input.MouseMode (which the tutorial changes per step) is
///   restored to Captured in Cleanup.
/// </summary>
public class TutorialFlowTest : TestClass
{
  public TutorialFlowTest(Node testScene) : base(testScene) { }

  private static TutorialFlow MakeFlow() => new(
    Enumerable.Range(0, TutorialFlow.TotalSteps)
      .Select(i => new TutorialFlow.Step($"step{i + 1}", null))
      .ToArray()
  );

  private static TutorialFlow MakeConditionalFlow(IReadOnlyList<bool> flags) => new(
    Enumerable.Range(0, TutorialFlow.TotalSteps)
      .Select(i => new TutorialFlow.Step($"step{i + 1}", () => flags[i]))
      .ToArray()
  );

  /// <summary>
  ///   T8.3 (a): a fresh flow has not started (CurrentStep 0, not complete).
  /// </summary>
  [Test]
  public void FlowStartsUnstarted()
  {
    var flow = MakeFlow();

    flow.CurrentStep.ShouldBe(0);
    flow.IsComplete.ShouldBeFalse();
    flow.Steps.Count.ShouldBe(TutorialFlow.TotalSteps);
  }

  /// <summary>
  ///   T8.3 (b): Start moves the flow to step 1 without completing it.
  /// </summary>
  [Test]
  public void StartMovesToFirstStep()
  {
    var flow = MakeFlow();

    flow.Start();

    flow.CurrentStep.ShouldBe(1);
    flow.IsComplete.ShouldBeFalse();
  }

  /// <summary>
  ///   T8.3 (b): Start is idempotent — the GameStarted event plus the
  ///   deferred fallback must not skip a step.
  /// </summary>
  [Test]
  public void StartIsIdempotent()
  {
    var flow = MakeFlow();

    flow.Start();
    flow.Start();

    flow.CurrentStep.ShouldBe(1);
  }

  /// <summary>
  ///   T8.3 (c): TryAdvance does not move while the current step's condition
  ///   is unmet.
  /// </summary>
  [Test]
  public void TryAdvanceDoesNotMoveWhenConditionUnmet()
  {
    var flow = MakeConditionalFlow(new bool[TutorialFlow.TotalSteps]);

    flow.Start();
    flow.TryAdvance().ShouldBeFalse();
    flow.CurrentStep.ShouldBe(1);
  }

  /// <summary>
  ///   T8.3 (d): satisfying the six conditions in order advances CurrentStep
  ///   by one each time and completes the flow.
  /// </summary>
  [Test]
  public void SatisfyingConditionsAdvancesThroughAllSteps()
  {
    var flags = new bool[TutorialFlow.TotalSteps];
    var flow = MakeConditionalFlow(flags);

    flow.Start();
    for (var i = 0; i < TutorialFlow.TotalSteps; i++)
    {
      flags[i] = true;
      flow.TryAdvance().ShouldBeTrue();
      flow.CurrentStep.ShouldBe(i + 2);
    }

    flow.IsComplete.ShouldBeTrue();
    flow.CurrentStep.ShouldBe(TutorialFlow.TotalSteps + 1);
  }

  /// <summary>
  ///   T8.3: CompleteCurrentStep forces the advance even when the condition
  ///   is unmet (the external-driver entry the UI uses).
  /// </summary>
  [Test]
  public void CompleteCurrentStepForcesAdvanceRegardlessOfCondition()
  {
    var flow = MakeConditionalFlow(new bool[TutorialFlow.TotalSteps]);

    flow.Start();
    flow.CompleteCurrentStep();

    flow.CurrentStep.ShouldBe(2);
  }

  /// <summary>
  ///   T8.3 (e): out-of-range calls after completion are no-ops — neither
  ///   CompleteCurrentStep nor TryAdvance crashes or moves the flow.
  /// </summary>
  [Test]
  public void CallsAfterCompletionAreNoOps()
  {
    var flow = MakeConditionalFlow(
      Enumerable.Repeat(true, TutorialFlow.TotalSteps).ToArray()
    );

    flow.Start();
    while (!flow.IsComplete)
      flow.TryAdvance().ShouldBeTrue();

    flow.CompleteCurrentStep();
    flow.TryAdvance().ShouldBeFalse();

    flow.CurrentStep.ShouldBe(TutorialFlow.TotalSteps + 1);
    flow.IsComplete.ShouldBeTrue();
  }
}

/// <summary>
///   Scene-level tests: assemble a TutorialUI with a fake player and drive
///   the real GameEvents translation path (no real input, no scene file).
/// </summary>
public class TutorialUITest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private TutorialUI _ui = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;

  public TutorialUITest(Node testScene) : base(testScene) { }

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

    // _Ready CallDeferred'd StartTutorial; let the deferred call run so the
    // tutorial is active before the first assertion.
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
  ///   Drives steps 1-5 (move/gather/drink/campfire/bed) through the real
  ///   event translation path, leaving the flow on step 6 (combat).
  /// </summary>
  private void AdvanceToCombatStep()
  {
    _player.GlobalPosition = new Vector3(3f, 0f, 0f);
    _ui._Process(0.1);
    _ui.CurrentStep.ShouldBe(2);

    // Step 2 (gather): collecting wood or coconut.
    GameEvents.RaiseItemAdded("coconut", 1);
    _ui.CurrentStep.ShouldBe(3);

    // Step 3 (drink): pressing F consumed one berries/coconut (ItemRemoved).
    GameEvents.RaiseItemRemoved("coconut", 1);
    _ui.CurrentStep.ShouldBe(4);

    GameEvents.RaiseBuildingPlaced("campfire");
    _ui.CurrentStep.ShouldBe(5);

    GameEvents.RaiseBuildingPlaced("bed");
    _ui.CurrentStep.ShouldBe(6);
  }

  /// <summary>
  ///   The tutorial starts active on step 1 with the dim visible and the
  ///   card showing the move instructions.
  /// </summary>
  [Test]
  public void TutorialStartsActiveWithDimVisible()
  {
    _ui.IsTutorialActive.ShouldBeTrue();
    _ui.CurrentStep.ShouldBe(1);

    var dim = _ui.GetNodeOrNull<Control>("Dim");
    dim.ShouldNotBeNull();
    dim!.Visible.ShouldBeTrue();
    dim.MouseFilter.ShouldBe(Control.MouseFilterEnum.Stop);

    var title = _ui.GetNodeOrNull<Label>("StepCard/VBox/Title");
    title.ShouldNotBeNull();
    title!.Text.ShouldBe("移动");
  }

  /// <summary>
  ///   Full walkthrough: each event-driven condition advances one step, the
  ///   step-changed events carry the right indices, and completion hides the
  ///   dim, releases the tutorial and raises TutorialCompleted exactly once.
  /// </summary>
  [Test]
  public void FullWalkthroughAdvancesStepsAndCompletes()
  {
    var steps = new List<(int Current, int Total)>();
    Action<int, int> onStep = (current, total) => steps.Add((current, total));
    var completed = 0;
    Action onDone = () => completed++;

    GameEvents.TutorialStepChanged += onStep;
    GameEvents.TutorialCompleted += onDone;
    try
    {
      // Step 1 (move): displacement beyond 2m from the step-start position.
      _player.GlobalPosition = new Vector3(3f, 0f, 0f);
      _ui._Process(0.1);
      _ui.CurrentStep.ShouldBe(2);

      // Step 2 (gather): collecting wood or coconut.
      GameEvents.RaiseItemAdded("wood", 1);
      _ui.CurrentStep.ShouldBe(3);

      // Step 3 (drink): pressing F consumed one berries/coconut (ItemRemoved).
      GameEvents.RaiseItemRemoved("coconut", 1);
      _ui.CurrentStep.ShouldBe(4);
      Input.MouseMode.ShouldBe(Input.MouseModeEnum.Visible);

      // Step 4 (campfire) / step 5 (bed): the matching buildable is placed.
      GameEvents.RaiseBuildingPlaced("campfire");
      _ui.CurrentStep.ShouldBe(5);
      GameEvents.RaiseBuildingPlaced("bed");
      _ui.CurrentStep.ShouldBe(6);

      // Step 6 (combat): the dim must not block the attack click.
      _ui.GetNodeOrNull<Control>("Dim")!.MouseFilter
        .ShouldBe(Control.MouseFilterEnum.Ignore);

      // A melee hit completes the tutorial.
      GameEvents.RaiseMeleeHit("stone_axe");

      _ui.IsTutorialActive.ShouldBeFalse();
      _ui.CurrentStep.ShouldBe(7);
      completed.ShouldBe(1);
      _ui.GetNodeOrNull<Control>("Dim")!.Visible.ShouldBeFalse();
      // NOTE: the tutorial requests Captured on completion (and on every
      // keyboard step), but a headless test window has no focus and Godot
      // does not honor Captured there — the Visible assertion at step 4
      // above already proves the mouse-driven step switch works.
    }
    finally
    {
      GameEvents.TutorialStepChanged -= onStep;
      GameEvents.TutorialCompleted -= onDone;
    }

    // The (1,6) raise happened during Setup before this subscription; the
    // five in-test advances cover steps 2..6.
    steps.Count.ShouldBe(5);
    for (var i = 0; i < steps.Count; i++)
    {
      steps[i].Current.ShouldBe(i + 2);
      steps[i].Total.ShouldBe(TutorialFlow.TotalSteps);
    }
  }

  /// <summary>
  ///   Fail-closed filtering: only wood/coconut advance the gather step.
  /// </summary>
  [Test]
  public void NonGatherableItemsDoNotAdvanceGatherStep()
  {
    _player.GlobalPosition = new Vector3(3f, 0f, 0f);
    _ui._Process(0.1);
    _ui.CurrentStep.ShouldBe(2);

    GameEvents.RaiseItemAdded("stone", 1);

    _ui.CurrentStep.ShouldBe(2);
    _ui.IsTutorialActive.ShouldBeTrue();
  }

  /// <summary>
  ///   The combat step also completes on an enemy death (MeleeHit alternate).
  /// </summary>
  [Test]
  public void EnemyDeathAlsoCompletesCombatStep()
  {
    AdvanceToCombatStep();

    GameEvents.RaiseEnemyDied("crab");

    _ui.IsTutorialActive.ShouldBeFalse();
    _ui.CurrentStep.ShouldBe(7);
  }

  /// <summary>
  ///   SkipTutorial force-completes the remaining steps through the normal
  ///   completion path (single TutorialCompleted, dim hidden).
  /// </summary>
  [Test]
  public void SkipTutorialCompletesImmediately()
  {
    var completed = 0;
    Action onDone = () => completed++;

    GameEvents.TutorialCompleted += onDone;
    try
    {
      _ui.SkipTutorial();
      completed.ShouldBe(1);
    }
    finally
    {
      GameEvents.TutorialCompleted -= onDone;
    }

    _ui.IsTutorialActive.ShouldBeFalse();
    _ui.CurrentStep.ShouldBe(7);
    _ui.GetNodeOrNull<Control>("Dim")!.Visible.ShouldBeFalse();
  }
}
