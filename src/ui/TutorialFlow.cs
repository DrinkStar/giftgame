// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;

/// <summary>
///   Pure-C# sequential state machine for the forced new-player tutorial
///   (T8.3). It only tracks order and counting: a step advances when its
///   completion condition is met (<see cref="TryAdvance"/>) or when driven
///   externally (<see cref="CompleteCurrentStep"/> — the entry TutorialUI
///   uses after translating a GameEvents occurrence into "current step
///   done"). Not a Godot node: no scene-tree dependency, unit-testable on
///   its own.
///
///   Step definitions (id + optional condition) are injected at
///   construction. The default 6-step order is the T8.3 contract:
///   move → gather → drink → campfire → bed → combat.
///
///   Iter8.5 (T8.5.10/T8.5.11): the step count is now a constructor
///   parameter, so chapter 2 (2 steps) and chapter 3 (1 step) forced
///   tutorials use the same state machine. <see cref="TotalSteps"/> stays 6
///   as the DEFAULT tutorial's step count (kept for TutorialStateTest);
///   <see cref="StepCount"/> reports the actual length of any flow.
/// </summary>
public sealed class TutorialFlow
{
  /// <summary>The step count of the default forced tutorial (T8.3).</summary>
  public const int TotalSteps = 6;

  /// <summary>
  ///   One tutorial step: an id plus an optional completion condition. A
  ///   null condition means the step is driven externally (the UI calls
  ///   <see cref="CompleteCurrentStep"/> when its event listeners fire).
  /// </summary>
  public sealed record Step(string Id, Func<bool>? Condition);

  private readonly Step[] _steps;
  private bool _started;

  /// <summary>
  ///   Creates the default flow with exactly <see cref="TotalSteps"/> steps
  ///   (the T8.3 chapter-1 tutorial). Equivalent to
  ///   <c>new TutorialFlow(TotalSteps, ...)</c> — kept so existing 6-step
  ///   constructions (TutorialStateTest) compile unchanged.
  /// </summary>
  /// <exception cref="ArgumentException">
  ///   Thrown when the step count differs from <see cref="TotalSteps"/> —
  ///   fail-fast instead of silently mis-sequencing the forced tutorial.
  /// </exception>
  public TutorialFlow(params Step[] steps) : this(TotalSteps, steps) { }

  /// <summary>
  ///   Iter8.5: creates a flow with exactly <paramref name="totalSteps"/>
  ///   steps. Chapters 2 and 3 pass their own counts (2 and 1).
  /// </summary>
  /// <param name="totalSteps">The number of steps this flow must contain.</param>
  /// <param name="steps">The step definitions, in order.</param>
  /// <exception cref="ArgumentException">
  ///   Thrown when the step count differs from <paramref name="totalSteps"/>
  ///   — fail-fast instead of silently mis-sequencing a forced tutorial.
  /// </exception>
  public TutorialFlow(int totalSteps, params Step[] steps)
  {
    if (steps.Length != totalSteps)
    {
      throw new ArgumentException(
        $"TutorialFlow requires exactly {totalSteps} steps, got {steps.Length}.",
        nameof(steps)
      );
    }

    _steps = steps;
  }

  /// <summary>
  ///   Current step, 1-based: 0 = not started, 1..N = active step, N+1 =
  ///   complete (see <see cref="IsComplete"/>), where N is this flow's own
  ///   step count.
  /// </summary>
  public int CurrentStep { get; private set; }

  /// <summary>True once all of this flow's steps are done.</summary>
  public bool IsComplete => CurrentStep > _steps.Length;

  /// <summary>The injected step definitions, in order.</summary>
  public IReadOnlyList<Step> Steps => _steps;

  /// <summary>
  ///   Iter8.5: the number of steps in THIS flow (the default is
  ///   <see cref="TotalSteps"/>; chapter 2 = 2, chapter 3 = 1).
  /// </summary>
  public int StepCount => _steps.Length;

  /// <summary>
  ///   Begins the tutorial at step 1. A no-op when already started or
  ///   already complete, so repeated starts (the GameStarted event plus the
  ///   deferred fallback) are safe.
  /// </summary>
  public void Start()
  {
    if (_started || IsComplete)
      return;

    _started = true;
    CurrentStep = 1;
  }

  /// <summary>
  ///   Advances to the next step only when the current step's condition (if
  ///   any) is satisfied; steps with a null condition advance immediately.
  ///   Returns false — leaving the step unchanged — when the flow has not
  ///   started, is already complete, or the current condition is unmet.
  /// </summary>
  public bool TryAdvance()
  {
    if (!_started || IsComplete || CurrentStep < 1 || CurrentStep > _steps.Length)
      return false;

    var condition = _steps[CurrentStep - 1].Condition;
    if (condition != null && !condition())
      return false;

    CurrentStep++;
    return true;
  }

  /// <summary>
  ///   Forces the current step to complete (advances by one) regardless of
  ///   its condition. This is the entry the UI uses after its event
  ///   listeners translate a GameEvents occurrence into "current step done".
  ///   A no-op when the flow has not started or is already complete, so
  ///   out-of-range calls never crash.
  /// </summary>
  public void CompleteCurrentStep()
  {
    if (!_started || IsComplete)
      return;

    CurrentStep++;
  }
}
