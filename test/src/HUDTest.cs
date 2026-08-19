// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;
using dotnetquestsystem;

/// <summary>
///   HUD 当前目标 tracker: hidden only during the chapter-1 tutorial card,
///   visible after TutorialCompleted / 开始游戏 skip, layout sits between
///   StatusPanel and ControlHint, MouseFilter.Ignore.
/// </summary>
public class HUDTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private QuestService _quests = default!;
  private CharacterBody3D _player = default!;
  private InventorySystem _inventory = default!;
  private HUD _hud = default!;

  public HUDTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    QuestManager.instance.questDatabase.Quests.Clear();
    GameEvents.RaiseGameplayInputLockChanged(false);

    _fixture = new Fixture(TestScene.GetTree());
    SweepLeakedNodes(TestScene.GetTree().Root);

    _player = new CharacterBody3D { Name = "Player" };
    _inventory = new InventorySystem { Name = "InventorySystem" };
    _player.AddChild(_inventory);
    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);

    _quests = new QuestService { Name = "QuestService", PlayerPath = "../Player" };
    await _fixture.AddToRoot(_quests, autoRemoveFromRoot: true);

    var packed = GD.Load<PackedScene>("res://scenes/hud.tscn");
    packed.ShouldNotBeNull();
    _hud = packed!.Instantiate<HUD>();
    _hud.Name = "HUD";
    await _fixture.AddToRoot(_hud, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;
    TestScene.GetTree().Paused = false;

    if (_hud != null && _hud.IsInsideTree() && _hud.GetParent() != null)
      _hud.GetParent()!.RemoveChild(_hud);
    if (_quests != null && _quests.IsInsideTree() && _quests.GetParent() != null)
      _quests.GetParent()!.RemoveChild(_quests);
    if (_player != null && _player.IsInsideTree() && _player.GetParent() != null)
      _player.GetParent()!.RemoveChild(_player);

    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    _hud?.Dispose();
    _hud = null!;
    _quests?.Dispose();
    _quests = null!;
    _player?.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

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

  private Label Objective =>
    _hud.GetNode<Label>("QuestObjectiveLabel");

  private void PressHud(string action) =>
    _hud._Input(new InputEventAction { Action = action, Pressed = true });

  [Test]
  public void QuestObjectiveSitsInMiddleSlotAndIgnoresMouse()
  {
    var label = Objective;
    label.MouseFilter.ShouldBe(Control.MouseFilterEnum.Ignore);
    label.OffsetLeft.ShouldBe(392f);
    label.OffsetRight.ShouldBe(-240f);
    label.OffsetTop.ShouldBe(16f);
  }

  [Test]
  public void QuestObjectiveHiddenDuringChapter1OverlayThenShown()
  {
    Objective.Visible.ShouldBeTrue();
    Objective.Text.ShouldContain("当前目标");

    GameEvents.RaiseTutorialStepChanged(1, 6);
    Objective.Visible.ShouldBeFalse();

    GameEvents.RaiseTutorialCompleted();
    Objective.Visible.ShouldBeTrue();
    Objective.Text.ShouldContain("当前目标");
  }

  [Test]
  public void QuestObjectiveStaysVisibleOnStartGameSkipAndInputLock()
  {
    Objective.Visible.ShouldBeTrue();

    var tutorial = new TutorialUI
    {
      Name = "TutorialUI",
      AutoStartChapter1 = false
    };
    _hud.GetParent()!.AddChild(tutorial);
    try
    {
      tutorial.CompleteChapter1WithoutPlaying();
      Objective.Visible.ShouldBeTrue();

      GameEvents.RaiseGameplayInputLockChanged(true);
      Objective.Visible.ShouldBeTrue();
      _hud.GetNode<ControlHintUI>("ControlHintUI").Visible.ShouldBeFalse();

      GameEvents.RaiseTutorialStepChanged(1, 2);
      Objective.Visible.ShouldBeTrue();
    }
    finally
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      tutorial.GetParent()?.RemoveChild(tutorial);
      tutorial.Free();
    }
  }

  [Test]
  public void EscClosesOpenInventory()
  {
    GameEvents.GameplayInputLocked.ShouldBeFalse();

    PressHud("inventory");
    _hud.IsInventoryOpen.ShouldBeTrue();

    PressHud("pause");
    _hud.IsInventoryOpen.ShouldBeFalse();
  }

  [Test]
  public void UiCancelClosesOpenInventory()
  {
    GameEvents.GameplayInputLocked.ShouldBeFalse();

    PressHud("inventory");
    _hud.IsInventoryOpen.ShouldBeTrue();

    PressHud("ui_cancel");
    _hud.IsInventoryOpen.ShouldBeFalse();
  }

  [Test]
  public void ControlHintHidesWithHudUnderMainMenu()
  {
    var hint = _hud.GetNode<ControlHintUI>("ControlHintUI");
    hint.ShouldNotBeNull();
    _hud.Visible.ShouldBeTrue();
    hint.IsVisibleInTree().ShouldBeTrue();

    _hud.Visible = false;
    hint.IsVisibleInTree().ShouldBeFalse();
  }
}
