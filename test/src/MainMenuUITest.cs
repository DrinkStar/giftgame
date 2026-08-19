// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.IO;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   Main-menu overlay: four Chinese actions, load disabled with no saves,
///   start skips chapter 1, tutorial starts chapter 1, never raises GamePaused.
/// </summary>
public class MainMenuUITest : TestClass, IDisposable
{
  private const string TEST_FOLDER = "user://saves_main_menu_test";

  private Fixture _fixture = default!;
  private MainMenuUI _menu = default!;
  private TutorialUI _tutorial = default!;
  private SaveService _service = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private string _tempDirectory = default!;

  public MainMenuUITest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _tempDirectory = ProjectSettings.GlobalizePath(TEST_FOLDER);
    if (Directory.Exists(_tempDirectory))
      Directory.Delete(_tempDirectory, recursive: true);

    Directory.CreateDirectory(_tempDirectory);

    _fixture = new Fixture(TestScene.GetTree());

    _player = new PlayerController { Name = "Player", Gravity = 0f };
    _stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(_stats);
    _player.Stats = _stats;
    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);

    _tutorial = new TutorialUI
    {
      Name = "TutorialUI",
      Player = _player,
      Stats = _stats,
      AutoStartChapter1 = false
    };
    await _fixture.AddToRoot(_tutorial, autoRemoveFromRoot: true);

    _service = new SaveService
    {
      Name = "SaveService",
      SaveFolder = TEST_FOLDER
    };
    await _fixture.AddToRoot(_service, autoRemoveFromRoot: true);

    _menu = new MainMenuUI
    {
      Name = "MainMenuUI",
      Tutorial = _tutorial,
      SaveService = _service
    };
    await _fixture.AddToRoot(_menu, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;

    if (_menu != null && _menu.IsInsideTree() && _menu.GetParent() != null)
      _menu.GetParent()!.RemoveChild(_menu);

    if (_tutorial != null && _tutorial.IsInsideTree() && _tutorial.GetParent() != null)
      _tutorial.GetParent()!.RemoveChild(_tutorial);

    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    if (_menu == null && _tutorial == null)
      return;

    _menu?.Dispose();
    _menu = null!;
    _tutorial?.Dispose();
    _tutorial = null!;
    _service?.Dispose();
    _service = null!;
    _player?.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  private Button MenuButton(string name) =>
    _menu.GetNode<Button>($"Backdrop/Center/Panel/VBox/{name}");

  [Test]
  public void FourButtonsShowChineseLabels()
  {
    MenuButton("TutorialButton").Text.ShouldBe("新手教程");
    MenuButton("StartButton").Text.ShouldBe("开始游戏");
    MenuButton("LoadButton").Text.ShouldBe("读取存档");
    MenuButton("QuitButton").Text.ShouldBe("退出游戏");
    _menu.GetNode<Label>("CreditsLabel").Text.ShouldBe(CreditsUI.CreditsText);
  }

  [Test]
  public void LoadDisabledWhenNoSaves()
  {
    MenuButton("LoadButton").Disabled.ShouldBeTrue();
  }

  [Test]
  public async Task StartGameSkipsChapterOneAndDoesNotRaiseGamePaused()
  {
    var paused = 0;
    var completed = 0;
    Action onPaused = () => paused++;
    Action onDone = () => completed++;
    GameEvents.GamePaused += onPaused;
    GameEvents.TutorialCompleted += onDone;
    try
    {
      MenuButton("StartButton").EmitSignal(Button.SignalName.Pressed);
      await TestScene.ProcessFrame(2);

      paused.ShouldBe(0);
      completed.ShouldBe(1);
      _tutorial.IsTutorialActive.ShouldBeFalse();
      _menu.Visible.ShouldBeFalse();
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
      GameEvents.TutorialCompleted -= onDone;
    }
  }

  [Test]
  public async Task TutorialButtonStartsChapterOne()
  {
    var paused = 0;
    Action onPaused = () => paused++;
    GameEvents.GamePaused += onPaused;
    try
    {
      MenuButton("TutorialButton").EmitSignal(Button.SignalName.Pressed);
      await TestScene.ProcessFrame(3);

      paused.ShouldBe(0);
      _tutorial.IsTutorialActive.ShouldBeTrue();
      _tutorial.CurrentStep.ShouldBe(1);
      _menu.Visible.ShouldBeFalse();
    }
    finally
    {
      GameEvents.GamePaused -= onPaused;
    }
  }

  [Test]
  public async Task TutorialButtonSpawnsOnTutorialIslandStartGameStaysOnMain()
  {
    var builder = new IslandBuilder { Name = "IslandBuilder", WorldSeed = 12345 };
    await _fixture.AddToRoot(builder, autoRemoveFromRoot: true);
    _menu.IslandBuilder = builder;
    _tutorial.IslandBuilder = builder;

    MenuButton("TutorialButton").EmitSignal(Button.SignalName.Pressed);
    await TestScene.ProcessFrame(3);

    builder.TutorialIslandReady.ShouldBeTrue();
    var spec = builder.TutorialSpec!;
    _player.GlobalPosition.X.ShouldBe(spec.Center.X, 0.05);
    _player.GlobalPosition.Z.ShouldBe(spec.Center.Y, 0.05);
    _tutorial.IsTutorialActive.ShouldBeTrue();

    _tutorial.SkipTutorial();
    _player.GlobalPosition.X.ShouldBe(0f, 0.05);
    _player.GlobalPosition.Z.ShouldBe(0f, 0.05);
  }

  [Test]
  public async Task StartGameDoesNotBuildTutorialIsland()
  {
    var builder = new IslandBuilder { Name = "IslandBuilder", WorldSeed = 12345 };
    await _fixture.AddToRoot(builder, autoRemoveFromRoot: true);
    _menu.IslandBuilder = builder;
    _tutorial.IslandBuilder = builder;
    _player.GlobalPosition = new Vector3(0f, 2f, 0f);

    MenuButton("StartButton").EmitSignal(Button.SignalName.Pressed);
    await TestScene.ProcessFrame(2);

    builder.TutorialIslandReady.ShouldBeFalse();
    _player.GlobalPosition.X.ShouldBe(0f, 0.05);
    _player.GlobalPosition.Z.ShouldBe(0f, 0.05);
    _tutorial.IsTutorialActive.ShouldBeFalse();
  }

  [Test]
  public void LoadEnabledWhenSaveExists()
  {
    _service.SaveGame();
    _menu.RefreshLoadAvailability();
    MenuButton("LoadButton").Disabled.ShouldBeFalse();
  }
}
