// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Right-side caption panel: compact Chinese keybind legend only (no
///   GuideLine mirror), hide on gameplay-input lock, never steals mouse,
///   unsubscribe on exit.
/// </summary>
public class ControlHintUITest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private ControlHintUI _hints = default!;

  public ControlHintUITest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    _fixture = new Fixture(TestScene.GetTree());
    _hints = new ControlHintUI { Name = "ControlHintUI" };
    await _fixture.AddToRoot(_hints, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    if (_hints != null && _hints.IsInsideTree() && _hints.GetParent() != null)
      _hints.GetParent()!.RemoveChild(_hints);

    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    _hints?.Dispose();
    _hints = null!;
    GC.SuppressFinalize(this);
  }

  private Label KeybindLabel =>
    _hints.GetNode<Label>("VBox/KeybindLabel");

  [Test]
  public void ListsEightCoreChineseKeybinds()
  {
    ControlHintUI.HintLines.Length.ShouldBe(10);
    ControlHintUI.HintLines[0].ShouldBe("W 前进");
    ControlHintUI.HintLines[1].ShouldBe("A 左移");
    ControlHintUI.HintLines[2].ShouldBe("S 后退");
    ControlHintUI.HintLines[3].ShouldBe("D 右移");
    ControlHintUI.HintLines[4].ShouldBe("空格 跳跃");
    ControlHintUI.HintLines[5].ShouldBe("Shift 冲刺");
    ControlHintUI.HintLines[6].ShouldBe("E 交互");
    ControlHintUI.HintLines[7].ShouldBe("左键 近战");
    ControlHintUI.HintLines[8].ShouldBe("Tab 背包");
    ControlHintUI.HintLines[9].ShouldBe("Esc 暂停");
    KeybindLabel.Text.ShouldBe(ControlHintUI.HintListText);

    var joined = ControlHintUI.HintListText;
    joined.ShouldNotContain("F 使用");
    joined.ShouldNotContain("Q 武器");
    joined.ShouldNotContain("B 建造");
    joined.ShouldNotContain("R 旋转");
    joined.ShouldNotContain("G 木筏");
    joined.ShouldNotContain("M 桨");
    joined.ShouldNotContain("C 合成");
    joined.ShouldNotContain("F5");
    joined.ShouldNotContain("F9");
    joined.ShouldNotContain("右键");
    joined.ShouldNotContain("存档");
    joined.ShouldNotContain("读档");
    joined.ShouldNotContain("建造");
  }

  [Test]
  public void DoesNotMirrorGuideLine()
  {
    _hints.GetNodeOrNull<Label>("VBox/GuideLabel").ShouldBeNull();
    _hints.GetNode<VBoxContainer>("VBox").GetChildCount().ShouldBe(1);

    GameEvents.RaiseGuideLine("去海边看看");

    KeybindLabel.Text.ShouldBe(ControlHintUI.HintListText);
    _hints.GetNodeOrNull<Label>("VBox/GuideLabel").ShouldBeNull();
  }

  [Test]
  public void IgnoresMouseOnRootAndPanel()
  {
    _hints.MouseFilter.ShouldBe(Control.MouseFilterEnum.Ignore);
    _hints.GetNode<VBoxContainer>("VBox").MouseFilter
      .ShouldBe(Control.MouseFilterEnum.Ignore);
    KeybindLabel.MouseFilter.ShouldBe(Control.MouseFilterEnum.Ignore);
  }

  [Test]
  public void HidesWhileGameplayInputLocked()
  {
    _hints.Visible.ShouldBeTrue();

    GameEvents.RaiseGameplayInputLockChanged(true);
    _hints.Visible.ShouldBeFalse();

    GameEvents.RaiseGameplayInputLockChanged(false);
    _hints.Visible.ShouldBeTrue();
  }

  [Test]
  public void ExitTreeUnsubscribesLock()
  {
    _hints.GetParent()!.RemoveChild(_hints);

    GameEvents.RaiseGameplayInputLockChanged(true);
    _hints.Visible.ShouldBeTrue();
  }

  [Test]
  public void HudSceneInstancesControlHintUiUnderHud()
  {
    var packed = GD.Load<PackedScene>("res://scenes/hud.tscn");
    packed.ShouldNotBeNull();
    var hud = packed!.Instantiate<CanvasLayer>();
    try
    {
      var hint = hud.GetNodeOrNull<ControlHintUI>("ControlHintUI");
      hint.ShouldNotBeNull();
      hint!.GetParent().ShouldBe(hud);
    }
    finally
    {
      hud.Free();
    }
  }
}
