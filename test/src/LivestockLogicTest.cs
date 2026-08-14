// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Locks the pure <see cref="LivestockLogic"/> state machine (Iter5 plan
///   Decision 6): every tame/feed/produce/collect path — the feed gates, the
///   timed transition, and the collect loop — as pure functions with no scene
///   tree.
/// </summary>
public class LivestockLogicTest : TestClass
{
  public LivestockLogicTest(Node testScene)
    : base(testScene) { }

  [Test]
  public void WildWithFeedCanBeTamed()
  {
    LivestockLogic.CanTame(LivestockLogic.State.Wild, true).ShouldBeTrue();

    // Taming is only offered to wild animals.
    LivestockLogic.CanTame(LivestockLogic.State.Tamed, true).ShouldBeFalse();
    LivestockLogic.CanTame(LivestockLogic.State.Producing, true).ShouldBeFalse();
    LivestockLogic.CanTame(LivestockLogic.State.Ready, true).ShouldBeFalse();
  }

  [Test]
  public void WildWithoutFeedCannotBeTamed()
  {
    // The state stays Wild: CanTame is the entire gate and it fails closed.
    LivestockLogic.CanTame(LivestockLogic.State.Wild, false).ShouldBeFalse();
    LivestockLogic.CanTame(LivestockLogic.State.Tamed, false).ShouldBeFalse();
  }

  [Test]
  public void TamedWithFeedStartsProducing()
  {
    LivestockLogic.StartProducing(LivestockLogic.State.Tamed, true).ShouldBeTrue();

    // A wild animal may also be fed straight into producing (Decision 6).
    LivestockLogic.StartProducing(LivestockLogic.State.Wild, true).ShouldBeTrue();

    // Without feed nothing starts, and a producing or ready animal cannot
    // restart until its produce is collected.
    LivestockLogic.StartProducing(LivestockLogic.State.Tamed, false).ShouldBeFalse();
    LivestockLogic.StartProducing(LivestockLogic.State.Producing, true).ShouldBeFalse();
    LivestockLogic.StartProducing(LivestockLogic.State.Ready, true).ShouldBeFalse();
  }

  [Test]
  public void ProducingTicksToReadyOnlyPastTheInterval()
  {
    LivestockLogic.Tick(LivestockLogic.State.Producing, 0f, 30f)
      .ShouldBe(LivestockLogic.State.Producing);
    LivestockLogic.Tick(LivestockLogic.State.Producing, 29.9f, 30f)
      .ShouldBe(LivestockLogic.State.Producing);
    LivestockLogic.Tick(LivestockLogic.State.Producing, 30f, 30f)
      .ShouldBe(LivestockLogic.State.Ready);
    LivestockLogic.Tick(LivestockLogic.State.Producing, 60f, 30f)
      .ShouldBe(LivestockLogic.State.Ready);
  }

  [Test]
  public void TickLeavesNonProducingStatesUntouched()
  {
    LivestockLogic.Tick(LivestockLogic.State.Wild, 999f, 30f)
      .ShouldBe(LivestockLogic.State.Wild);
    LivestockLogic.Tick(LivestockLogic.State.Tamed, 999f, 30f)
      .ShouldBe(LivestockLogic.State.Tamed);

    // Ready stays Ready until collected — it never re-produces on its own.
    LivestockLogic.Tick(LivestockLogic.State.Ready, 999f, 30f)
      .ShouldBe(LivestockLogic.State.Ready);
  }

  [Test]
  public void CollectTurnsReadyBackToTamedWithOneProduce()
  {
    LivestockLogic.ProduceAmount.ShouldBe(1);
    LivestockLogic.Collect(LivestockLogic.State.Ready)
      .ShouldBe(LivestockLogic.State.Tamed);

    // Collecting from a non-ready animal is a no-op.
    LivestockLogic.Collect(LivestockLogic.State.Tamed)
      .ShouldBe(LivestockLogic.State.Tamed);
    LivestockLogic.Collect(LivestockLogic.State.Producing)
      .ShouldBe(LivestockLogic.State.Producing);
    LivestockLogic.Collect(LivestockLogic.State.Wild)
      .ShouldBe(LivestockLogic.State.Wild);
  }

  [Test]
  public void FullProduceLoop()
  {
    // Wild -> (feed) Tamed -> (feed) Producing -> (interval) Ready -> Collect Tamed.
    LivestockLogic.CanTame(LivestockLogic.State.Wild, true).ShouldBeTrue();
    LivestockLogic.StartProducing(LivestockLogic.State.Tamed, true).ShouldBeTrue();
    LivestockLogic.Tick(LivestockLogic.State.Producing, 30f, 30f)
      .ShouldBe(LivestockLogic.State.Ready);
    LivestockLogic.Collect(LivestockLogic.State.Ready)
      .ShouldBe(LivestockLogic.State.Tamed);
  }
}
