// Original (Iter8) — no upstream port
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
/// </summary>
public sealed class TutorialFlow
{
  /// <summary>The fixed number of forced tutorial steps (T8.3).</summary>
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
  ///   Creates the flow with exactly <see cref="TotalSteps"/> steps.
  /// </summary>
  /// <exception cref="ArgumentException">
  ///   Thrown when the step count differs from <see cref="TotalSteps"/> —
  ///   fail-fast instead of silently mis-sequencing the forced tutorial.
  /// </exception>
  public TutorialFlow(params Step[] steps)
  {
    if (steps.Length != TotalSteps)
    {
      throw new ArgumentException(
        $"TutorialFlow requires exactly {TotalSteps} steps, got {steps.Length}.",
        nameof(steps)
      );
    }

    _steps = steps;
  }

  /// <summary>
  ///   Current step, 1-based: 0 = not started, 1..6 = active step, 7 =
  ///   complete (see <see cref="IsComplete"/>).
  /// </summary>
  public int CurrentStep { get; private set; }

  /// <summary>True once all six steps are done.</summary>
  public bool IsComplete => CurrentStep > TotalSteps;

  /// <summary>The injected step definitions, in order.</summary>
  public IReadOnlyList<Step> Steps => _steps;

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
    if (!_started || IsComplete || CurrentStep < 1 || CurrentStep > TotalSteps)
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
