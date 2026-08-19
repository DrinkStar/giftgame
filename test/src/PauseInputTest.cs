// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   Pause keybind contract: gameplay pause is InputMap action <c>pause</c>
///   (not <c>ui_cancel</c> alone). Open inventory/craft/storage consume Esc
///   first so the same press does not pause; a following press with those
///   panels closed does pause. Main-menu hold is covered by MainMenuUITest
///   (tree pause without GamePaused).
/// </summary>
public class PauseInputTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private GameManager _manager = default!;

  public PauseInputTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    _fixture = new Fixture(TestScene.GetTree());
    SweepLeakedNodes(TestScene.GetTree().Root);

    _manager = new GameManager { Name = "GameManager" };
    await _fixture.AddToRoot(_manager, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    UnpauseIfNeeded();
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;
    TestScene.GetTree().Paused = false;

    if (_manager != null && _manager.IsInsideTree() && _manager.GetParent() != null)
      _manager.GetParent()!.RemoveChild(_manager);

    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    _manager?.Dispose();
    _manager = null!;
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

  private void UnpauseIfNeeded()
  {
    if (_manager != null && _manager.IsPaused)
      _manager.TogglePause();
    TestScene.GetTree().Paused = false;
  }

  private static void Press(Node node, string action) =>
    node._Input(new InputEventAction { Action = action, Pressed = true });

  [Test]
  public void PauseActionTogglesPauseAndUiCancelDoesNot()
  {
    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    try
    {
      Press(_manager, "ui_cancel");
      _manager.IsPaused.ShouldBeFalse();
      paused.ShouldBe(0);

      Press(_manager, "pause");
      _manager.IsPaused.ShouldBeTrue();
      paused.ShouldBe(1);
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      UnpauseIfNeeded();
    }
  }

  [Test]
  public void PauseDoesNotFireWhileGameplayInputLocked()
  {
    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    GameEvents.RaiseGameplayInputLockChanged(true);
    try
    {
      Press(_manager, "pause");
      _manager.IsPaused.ShouldBeFalse();
      paused.ShouldBe(0);
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      GameEvents.RaiseGameplayInputLockChanged(false);
    }
  }

  [Test]
  public async Task InventoryEscClosesWithoutPausingThenSecondEscPauses()
  {
    var packed = GD.Load<PackedScene>("res://scenes/hud.tscn");
    packed.ShouldNotBeNull();
    var hud = packed!.Instantiate<HUD>();
    hud.Name = "HUD";
    await _fixture.AddToRoot(hud, autoRemoveFromRoot: true);

    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    try
    {
      Press(hud, "inventory");
      hud.IsInventoryOpen.ShouldBeTrue();
      GameEvents.GameplayInputLocked.ShouldBeTrue();

      Press(hud, "pause");
      Press(_manager, "pause");
      hud.IsInventoryOpen.ShouldBeFalse();
      _manager.IsPaused.ShouldBeFalse();
      paused.ShouldBe(0);

      await TestScene.ProcessFrame(2);
      GameEvents.GameplayInputLocked.ShouldBeFalse();

      Press(_manager, "pause");
      _manager.IsPaused.ShouldBeTrue();
      paused.ShouldBe(1);
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      UnpauseIfNeeded();
    }
  }

  [Test]
  public async Task CraftUiCancelClosesWithoutPausingThenPauseWhenClosed()
  {
    var inventory = new InventorySystem();
    var crafting = new CraftingSystem();
    await _fixture.AddToRoot(inventory, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(crafting, autoRemoveFromRoot: true);
    crafting.Initialize(inventory);

    var craftUi = new CraftUI { Crafting = crafting };
    await _fixture.AddToRoot(craftUi, autoRemoveFromRoot: true);
    craftUi.UnlockForTests();
    craftUi.Toggle();
    craftUi.IsOpen.ShouldBeTrue();
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    try
    {
      Press(craftUi, "ui_cancel");
      Press(_manager, "pause");
      craftUi.IsOpen.ShouldBeFalse();
      _manager.IsPaused.ShouldBeFalse();
      paused.ShouldBe(0);

      await TestScene.ProcessFrame(2);
      GameEvents.GameplayInputLocked.ShouldBeFalse();

      Press(_manager, "pause");
      _manager.IsPaused.ShouldBeTrue();
      paused.ShouldBe(1);
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      UnpauseIfNeeded();
    }
  }

  [Test]
  public async Task StorageUiCancelClosesWithoutPausingThenPauseWhenClosed()
  {
    var inventory = new InventorySystem();
    await _fixture.AddToRoot(inventory, autoRemoveFromRoot: true);
    var box = new StorageBox();
    await _fixture.AddToRoot(box, autoRemoveFromRoot: true);
    var ui = new StorageUI();
    await _fixture.AddToRoot(ui, autoRemoveFromRoot: true);

    ui.Open(box, inventory);
    ui.IsOpen.ShouldBeTrue();
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    try
    {
      Press(ui, "ui_cancel");
      Press(_manager, "pause");
      ui.IsOpen.ShouldBeFalse();
      _manager.IsPaused.ShouldBeFalse();
      paused.ShouldBe(0);

      await TestScene.ProcessFrame(2);
      GameEvents.GameplayInputLocked.ShouldBeFalse();

      Press(_manager, "pause");
      _manager.IsPaused.ShouldBeTrue();
      paused.ShouldBe(1);
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      UnpauseIfNeeded();
    }
  }
}
