// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;
using dotnetquestsystem;

/// <summary>
///   QuestService bridge tests (iter7-plan Decision 8): progress counting,
///   completion → FinishQuest + reward by item Id AND amount, chapter gating,
///   death-is-not-failure, boss completion of chapter 3, and Game.tscn node
///   presence. Setup clears the static quest database (singleton survives
///   between tests) and Cleanup unsubscribes the OnQuestCompleate handler —
///   the singleton-pollution guard.
///
///   Iter8.5 (T8.5.9): chapter 1 now opens with quest_radio, so the tests
///   below complete it first (RaiseStoryPointReached("radio")) before
///   exercising the quest_wood chain.
/// </summary>
public class QuestServiceTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private CharacterBody3D _player = default!;
  private InventorySystem _inventory = default!;
  private QuestService _service = default!;
  private Action<Quest>? _compleateHandler;

  public QuestServiceTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    // Singleton pollution guard (Decision 8): the DotnetQuestSystem database
    // is a process-wide singleton — start every case from a clean slate.
    QuestManager.instance.questDatabase.Quests.Clear();

    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes. GoDotTest's
    // fixture cleanup does not free nodes deterministically between classes
    // (GC/frame-timing dependent), so a leftover Game scene keeps its
    // QuestService/GuideService subscribed to the static bus — and a leaked
    // "Player" would steal the "../Player" node-path resolution below
    // (Godot auto-renames OUR player and rewards land in the wrong
    // inventory). Sweep BEFORE adding our own nodes.
    SweepLeakedNodes(TestScene.GetTree().Root);

    // Plain CharacterBody3D suffices: QuestService only needs the node type
    // plus an InventorySystem child to grant rewards into.
    _player = new CharacterBody3D { Name = "Player" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);

    _service = new QuestService { Name = "QuestService", PlayerPath = "../Player" };

    // Player first: QuestService._Ready subscribes after the player subtree
    // is ready, mirroring the Game.tscn ordering (Decision 6).
    _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
    _fixture.AddToRoot(_service, autoRemoveFromRoot: true);
  }

  /// <summary>
  ///   Removes and frees leftover nodes from earlier test classes. GoDotTest's
  ///   fixture cleanup does not free nodes deterministically between classes
  ///   (GC/frame-timing dependent), so leaked nodes accumulate under the
  ///   scene-tree root: a leftover Game scene keeps its QuestService/
  ///   GuideService subscribed to the static bus, and a leaked node named
  ///   "Player" (of ANY type — some suites use bare Nodes) would steal the
  ///   "../Player" node-path resolution below (Godot auto-renames OUR player
  ///   and rewards land in the wrong inventory, or nowhere at all). Everything
  ///   except the test runner scene is test garbage and gets freed
  ///   synchronously so its subscriptions die before this case raises events.
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
    // Singleton pollution guard: drop any OnQuestCompleate handler this test
    // attached (Decision 8) — the library's static QuestManager never resets.
    if (_compleateHandler != null)
    {
      QuestManager.instance.OnQuestCompleate -= _compleateHandler;
      _compleateHandler = null;
    }

    // Detach synchronously BEFORE Fixture.Cleanup: _ExitTree unsubscribes the
    // static GameEvents bus immediately. GoDotTest runs the next test in the
    // same frame — queued frees would leak this service's subscriptions (and
    // its quest state) into the next test case.
    if (_service.IsInsideTree())
      _service.GetParent()!.RemoveChild(_service);
    if (_player.IsInsideTree())
      _player.GetParent()!.RemoveChild(_player);

    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_service == null)
      return;

    _service.Dispose();
    _service = null!;
    _inventory.Dispose();
    _inventory = null!;
    _player.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  private static Quest? Quest(string name) =>
    QuestManager.instance.questController.GetQuestByName(name);

  /// <summary>
  ///   HUD tracker seam: chapter 1 opens on quest_radio with its Chinese
  ///   objective; completing radio advances the snapshot to quest_wood.
  /// </summary>
  [Test]
  public void CurrentTrackerExposesRadioThenWood()
  {
    _service.TryGetCurrentTracker(out var id, out var objective, out var done, out var need)
      .ShouldBeTrue();
    id.ShouldBe("quest_radio");
    objective.ShouldBe("抵达无线电塔并阅读日志");
    done.ShouldBe(0);
    need.ShouldBe(1);
    _service.CurrentQuestId.ShouldBe("quest_radio");

    GameEvents.RaiseStoryPointReached("radio");

    _service.TryGetCurrentTracker(out id, out objective, out done, out need).ShouldBeTrue();
    id.ShouldBe("quest_wood");
    objective.ShouldBe("收集 5 个木头");
    done.ShouldBe(0);
    need.ShouldBe(5);
  }

  /// <summary>
  ///   (a) The internal progress counter counts condition events and forwards
  ///   (questId, done, need) on every step — the quest stays Current below
  ///   its target. quest_radio (chapter-1 opener) is completed first.
  /// </summary>
  [Test]
  public void WoodProgressCountsEventsAndRaisesProgress()
  {
    var events = new List<(string Id, int Done, int Need)>();
    Action<string, int, int> onProgress = (id, done, need) =>
      events.Add((id, done, need));
    GameEvents.QuestProgress += onProgress;
    try
    {
      // Iter8.5: chapter 1 starts with quest_radio; complete it so quest_wood
      // becomes Current, then count wood events.
      GameEvents.RaiseStoryPointReached("radio");

      for (var i = 0; i < 3; i++)
        GameEvents.RaiseItemAdded("wood", 1);

      Quest("quest_wood")!.Status.ShouldBe(QuestStatus.Current);

      events[^1].Id.ShouldBe("quest_wood");
      events[^1].Done.ShouldBe(3);
      events[^1].Need.ShouldBe(5);
    }
    finally
    {
      GameEvents.QuestProgress -= onProgress;
    }
  }

  /// <summary>
  ///   (b) Condition met → FinishQuest (library OnQuestCompleate fires) →
  ///   reward granted with the correct ITEM ID AND AMOUNT from the
  ///   QuestService table (wood ×3), asserted via inventory GetItemCount.
  ///   quest_radio (wood ×5 reward) completes first — chapter 1 order.
  /// </summary>
  [Test]
  public void CompletingWoodQuestFinishesAndGrantsWoodReward()
  {
    var completions = new List<Quest>();
    _compleateHandler = quest => completions.Add(quest);
    QuestManager.instance.OnQuestCompleate += _compleateHandler;

    var completedIds = new List<string>();
    Action<string> onCompleted = id => completedIds.Add(id);
    GameEvents.QuestCompleted += onCompleted;
    try
    {
      GameEvents.RaiseStoryPointReached("radio"); // quest_radio → wood ×5

      for (var i = 0; i < 5; i++)
        GameEvents.RaiseItemAdded("wood", 1);

      Quest("quest_wood")!.Status.ShouldBe(QuestStatus.Complete);

      // quest_radio completes first, then quest_wood.
      completions.Count.ShouldBe(2);
      completions.ShouldContain(q => q.Name == "quest_wood");

      completedIds.Count.ShouldBe(2);
      completedIds.ShouldContain("quest_wood");

      // Reward table: quest_radio → wood ×5, quest_wood → wood ×3.
      _inventory.GetItemCount("wood").ShouldBe(8);
    }
    finally
    {
      GameEvents.QuestCompleted -= onCompleted;
    }
  }

  /// <summary>
  ///   (c) Chapter gating: quest_campfire stays New while quest_wood is
  ///   active — its condition events are ignored — and starts exactly when
  ///   the previous quest completes.
  /// </summary>
  [Test]
  public void ChapterGatingBlocksNextQuestUntilCurrentCompletes()
  {
    // Iter8.5: complete the chapter-1 opener quest_radio first.
    GameEvents.RaiseStoryPointReached("radio");

    Quest("quest_wood")!.Status.ShouldBe(QuestStatus.Current);

    GameEvents.RaiseBuildingPlaced("campfire");
    Quest("quest_campfire")!.Status.ShouldBe(QuestStatus.New);

    for (var i = 0; i < 5; i++)
      GameEvents.RaiseItemAdded("wood", 1);

    Quest("quest_campfire")!.Status.ShouldBe(QuestStatus.Current);
  }

  /// <summary>
  ///   (d) PlayerDied is NOT a failure (frozen contract): the active quest
  ///   stays Current and no completion/cancellation fires.
  /// </summary>
  [Test]
  public void PlayerDiedDoesNotFailActiveQuest()
  {
    // Iter8.5: complete the chapter-1 opener quest_radio first.
    GameEvents.RaiseStoryPointReached("radio");

    GameEvents.RaisePlayerDied();

    Quest("quest_wood")!.Status.ShouldBe(QuestStatus.Current);
  }

  /// <summary>
  ///   (e) Driving every condition in order ends with BossDefeated
  ///   ("shark_king") completing chapter 3's quest_boss.
  /// </summary>
  [Test]
  public void BossDefeatedCompletesChapterThree()
  {
    GameEvents.RaiseStoryPointReached("radio"); // ch1 q0: quest_radio
    for (var i = 0; i < 5; i++)
      GameEvents.RaiseItemAdded("wood", 1); // ch1 q1: quest_wood
    GameEvents.RaiseBuildingPlaced("campfire"); // ch1 q2: quest_campfire
    GameEvents.RaiseCraftingCompleted("cooked_meat"); // ch1 q3
    for (var i = 0; i < 3; i++)
      GameEvents.RaiseCropHarvested("potato"); // ch2 q1: quest_harvest
    GameEvents.RaiseStoryPointReached("ruin"); // ch2 q2: quest_ruin
    GameEvents.RaiseEnemyDied("mutant"); // ch2 q3: quest_mutant

    Quest("quest_boss")!.Status.ShouldBe(QuestStatus.Current);

    GameEvents.RaiseBossDefeated("shark_king");

    Quest("quest_boss")!.Status.ShouldBe(QuestStatus.Complete);
  }

  /// <summary>
  ///   Completing quest_mutant grants the chapter-3 prep kit: arrows, cooked
  ///   meat, and cloth armor (multi-stack RewardAmounts).
  /// </summary>
  [Test]
  public void CompletingMutantQuestGrantsPrepKit()
  {
    GameEvents.RaiseStoryPointReached("radio");
    for (var i = 0; i < 5; i++)
      GameEvents.RaiseItemAdded("wood", 1);
    GameEvents.RaiseBuildingPlaced("campfire");
    GameEvents.RaiseCraftingCompleted("cooked_meat");
    for (var i = 0; i < 3; i++)
      GameEvents.RaiseCropHarvested("potato");
    GameEvents.RaiseStoryPointReached("ruin");

    Quest("quest_mutant")!.Status.ShouldBe(QuestStatus.Current);

    var arrowsBefore = _inventory.GetItemCount("arrow");
    var meatBefore = _inventory.GetItemCount("cooked_meat");
    var armorBefore = _inventory.GetItemCount("cloth_armor");

    GameEvents.RaiseEnemyDied("mutant");

    Quest("quest_mutant")!.Status.ShouldBe(QuestStatus.Complete);
    _inventory.GetItemCount("arrow").ShouldBe(arrowsBefore + 20);
    _inventory.GetItemCount("cooked_meat").ShouldBe(meatBefore + 3);
    _inventory.GetItemCount("cloth_armor").ShouldBe(armorBefore + 1);
    Quest("quest_boss")!.Status.ShouldBe(QuestStatus.Current);
  }

  /// <summary>
  ///   (f) The product scene wires the full quest stack: QuestService,
  ///   GuideService and the final ruin StoryInteractable on the Ruin island.
  /// </summary>
  [Test]
  public async Task GameSceneContainsQuestNodes()
  {
    var game = await _fixture.LoadAndAddScene<Game>();
    try
    {
      var questService = game.GetNodeOrNull<QuestService>("QuestService");
      var guideService = game.GetNodeOrNull<GuideService>("GuideService");
      var builder = game.GetNodeOrNull<IslandBuilder>("IslandBuilder");

      questService.ShouldNotBeNull();
      guideService.ShouldNotBeNull();
      builder.ShouldNotBeNull();

      var ruinLog = FindDescendants<StoryInteractable>(builder!)
        .Single(t => t.StoryPointId == "ruin");
      ruinLog.Text.ShouldBe(IslandLore.RuinQuestLogs[^1]);
      ruinLog.GetParent()!.Name.ToString().StartsWith("Island_Ruin").ShouldBeTrue();
    }
    finally
    {
      // Detach synchronously so every node in the scene (QuestService,
      // GuideService, HUD, ...) unsubscribes the static bus NOW — later test
      // classes must not inherit this scene's subscriptions.
      if (game.IsInsideTree())
        game.GetParent()!.RemoveChild(game);
    }
  }

  private static List<T> FindDescendants<T>(Node root) where T : Node
  {
    var result = new List<T>();
    Collect(root, result);
    return result;
  }

  private static void Collect<T>(Node node, List<T> into) where T : Node
  {
    if (node is T typed)
      into.Add(typed);
    foreach (Node child in node.GetChildren())
      Collect(child, into);
  }
}
