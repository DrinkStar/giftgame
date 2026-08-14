// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;
using dotnetquestsystem;

/// <summary>
///   T8.5.9 story interactable tests: the one-shot interact contract
///   (GuideLine + StoryPointReached fire exactly once across repeated
///   interacts; the prompt disappears afterwards) and the QuestService radio
///   branch — quest_radio opens chapter 1, completes on
///   StoryPointReached("radio") and grants the wood ×5 reward.
///
///   Assembly mirrors QuestServiceTest (fixture + cleared quest database +
///   player-with-inventory + QuestService with the "../Player" path).
/// </summary>
public class StoryInteractableTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private CharacterBody3D _player = default!;
  private InventorySystem _inventory = default!;
  private QuestService _service = default!;

  public StoryInteractableTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    // Singleton pollution guard (Decision 8): the DotnetQuestSystem database
    // is a process-wide singleton — start every case from a clean slate.
    QuestManager.instance.questDatabase.Quests.Clear();

    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes (see QuestServiceTest
    // for the rationale).
    SweepLeakedNodes(TestScene.GetTree().Root);

    _player = new CharacterBody3D { Name = "Player" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);

    _service = new QuestService { Name = "QuestService", PlayerPath = "../Player" };

    // Player first: QuestService._Ready subscribes after the player subtree
    // is ready, mirroring the Game.tscn ordering (Decision 6).
    _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
    _fixture.AddToRoot(_service, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    // Detach synchronously BEFORE Fixture.Cleanup: _ExitTree unsubscribes the
    // static GameEvents bus immediately (see QuestServiceTest).
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

  /// <summary>
  ///   ① Interact raises GuideLine + StoryPointReached exactly once: a second
  ///   interact is a no-op and CanInteract turns false (the prompt
  ///   disappears). Static bus handlers unsubscribed in finally (Decision 13).
  /// </summary>
  [Test]
  public async Task InteractRaisesGuideLineAndStoryPointOnce()
  {
    var log = new StoryInteractable
    {
      StoryPointId = "lore_1",
      Text = "测试日志内容"
    };
    await _fixture.AddToRoot(log, autoRemoveFromRoot: true);

    log.GetInteractionPrompt().ShouldBe("[E] Read log");
    log.CanInteract().ShouldBeTrue();

    var guides = new List<string>();
    var reached = new List<string>();
    Action<string> onGuide = text => guides.Add(text);
    Action<string> onReached = id => reached.Add(id);
    GameEvents.GuideLine += onGuide;
    GameEvents.StoryPointReached += onReached;

    var player = new PlayerController();
    try
    {
      log.Interact(player);
      log.Interact(player);
      log.Interact(player);

      guides.Count.ShouldBe(1);
      guides[0].ShouldBe("测试日志内容");
      reached.Count.ShouldBe(1);
      reached[0].ShouldBe("lore_1");

      log.CanInteract().ShouldBeFalse();
    }
    finally
    {
      GameEvents.GuideLine -= onGuide;
      GameEvents.StoryPointReached -= onReached;
      player.Free();
    }
  }

  /// <summary>
  ///   ② The QuestService radio branch: quest_radio is the chapter-1 opener
  ///   (Current on service _Ready, quest_wood still New), completes on
  ///   StoryPointReached("radio") and grants the wood ×5 reward, then
  ///   quest_wood becomes Current.
  /// </summary>
  [Test]
  public void RadioStoryPointStartsChapterOneRadioQuest()
  {
    Quest("quest_radio")!.Status.ShouldBe(QuestStatus.Current);
    Quest("quest_wood")!.Status.ShouldBe(QuestStatus.New);

    GameEvents.RaiseStoryPointReached("radio");

    Quest("quest_radio")!.Status.ShouldBe(QuestStatus.Complete);
    Quest("quest_wood")!.Status.ShouldBe(QuestStatus.Current);

    // Reward table: quest_radio → wood ×5 (T8.5.9).
    _inventory.GetItemCount("wood").ShouldBe(5);
  }
}
