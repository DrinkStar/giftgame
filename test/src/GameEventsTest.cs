// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the static <see cref="GameEvents"/> bus: delivery to
///   subscribers, multiple subscribers, and unsubscribe hygiene. Handlers are
///   always removed (try/finally) to keep the static bus clean between tests
///   (plan Decision 13).
/// </summary>
public class GameEventsTest : TestClass
{
  private static readonly int[] ExpectedHotbarCalls = { 2, 2 };

  public GameEventsTest(Node testScene) : base(testScene) { }

  [Test]
  public void SubscribeAndRaiseDeliversPayload()
  {
    var received = -1f;
    Action<float> handler = hour => received = hour;
    GameEvents.TimeChanged += handler;
    try
    {
      GameEvents.RaiseTimeChanged(3.5f);
      received.ShouldBe(3.5f);
    }
    finally
    {
      GameEvents.TimeChanged -= handler;
    }
  }

  [Test]
  public void MultipleSubscribersAllReceiveTheEvent()
  {
    var calls = new List<int>();
    Action<int> first = slot => calls.Add(slot);
    Action<int> second = slot => calls.Add(slot);
    GameEvents.HotbarSelectionChanged += first;
    GameEvents.HotbarSelectionChanged += second;
    try
    {
      GameEvents.RaiseHotbarSelectionChanged(2);
      calls.ShouldBe(ExpectedHotbarCalls);
    }
    finally
    {
      GameEvents.HotbarSelectionChanged -= first;
      GameEvents.HotbarSelectionChanged -= second;
    }
  }

  [Test]
  public void UnsubscribedHandlerStopsReceivingEvents()
  {
    var count = 0;
    Action handler = () => count++;
    GameEvents.GameStarted += handler;
    GameEvents.RaiseGameStarted();
    GameEvents.GameStarted -= handler;
    GameEvents.RaiseGameStarted();
    count.ShouldBe(1);
  }

  [Test]
  public void RaisingEventsWithoutSubscribersIsSafe()
  {
    Should.NotThrow(() => GameEvents.RaisePlayerDied());
    Should.NotThrow(() => GameEvents.RaiseTimeChanged(12f));
    Should.NotThrow(() => GameEvents.RaiseItemAdded("wood", 1));
  }
}
