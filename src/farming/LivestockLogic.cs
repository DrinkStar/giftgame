// Original (Iter5) — no upstream port
namespace SeaAnomaly;

/// <summary>
///   Pure static livestock state machine (Iter5 plan Decision 6). No Godot
///   types, no state: every transition is a pure function from the current
///   state plus inputs to the next state, so the whole tame/feed/produce/
///   collect loop is unit-testable without a scene tree. Livestock.cs drives
///   it with real frame time; the pen-proximity gate and inventory mutation
///   stay in the node.
/// </summary>
public static class LivestockLogic
{
  /// <summary>
  ///   Lifecycle of a pen animal:
  ///   - Wild: untamed; a feeding near a placed pen tames it.
  ///   - Tamed: owned; a feeding starts producing.
  ///   - Producing: waiting for the produce interval to elapse.
  ///   - Ready: produce available; collecting returns it to Tamed.
  /// </summary>
  public enum State
  {
    Wild,
    Tamed,
    Producing,
    Ready
  }

  /// <summary>Produce granted on every successful collection.</summary>
  public const int ProduceAmount = 1;

  /// <summary>
  ///   True when the animal is Wild AND the player holds feed — the state
  ///   half of the tame gate (the pen-proximity half lives in Livestock).
  /// </summary>
  public static bool CanTame(State state, bool hasFeed) =>
    state == State.Wild && hasFeed;

  /// <summary>
  ///   True when the animal may enter Producing: a Wild or Tamed animal that
  ///   is fed. A producing or ready animal cannot be restarted until its
  ///   produce is collected (Decision 6: a feeding always requires feed).
  /// </summary>
  public static bool StartProducing(State state, bool hasFeed) =>
    (state is State.Wild or State.Tamed) && hasFeed;

  /// <summary>
  ///   Time transition: Producing becomes Ready once the elapsed time reaches
  ///   (or passes) the produce interval. Every other state — including Ready,
  ///   which must stay collectible — is unchanged.
  /// </summary>
  public static State Tick(State state, float elapsed, float produceInterval) =>
    state == State.Producing && elapsed >= produceInterval ? State.Ready : state;

  /// <summary>
  ///   Collect transition: a Ready animal yields its produce (the amount is
  ///   <see cref="ProduceAmount"/>) and returns to Tamed. Any other state is
  ///   unchanged — there is nothing to collect.
  /// </summary>
  public static State Collect(State state) =>
    state == State.Ready ? State.Tamed : state;
}
