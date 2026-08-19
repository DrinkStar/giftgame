// Original — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Persistent right-side caption panel: compact Chinese keybind legend
///   only. Story voice stays on the HUD bottom
///   <c>GuideSubtitle</c> — this panel does not mirror
///   <see cref="GameEvents.GuideLine"/>. Lives under the HUD CanvasLayer so
///   the main menu hide covers it. MouseFilter.Ignore — fail-closed, never
///   steals gameplay clicks. Hides while
///   <see cref="GameEvents.GameplayInputLocked"/> (CraftUI / StorageUI).
///   SUBSCRIBES one GameEvent; unsubscribes in _ExitTree.
/// </summary>
public partial class ControlHintUI : PanelContainer
{
  /// <summary>Core keybind legend, one compact Chinese line per action.</summary>
  public static readonly string[] HintLines =
  {
    "W 前进",
    "A 左移",
    "S 后退",
    "D 右移",
    "空格 跳跃",
    "Shift 冲刺",
    "E 交互",
    "左键 近战",
    "Tab 背包",
    "Esc 暂停"
  };

  /// <summary>Joined legend text shown in the keybind label.</summary>
  public static string HintListText => string.Join("\n", HintLines);

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    ApplyPanelStyle();
    LayoutAsRightCaption();
    BuildUi();
    FitHeight();

    GameEvents.GameplayInputLockChanged += OnGameplayInputLockChanged;
    Visible = !GameEvents.GameplayInputLocked;
  }

  public override void _ExitTree()
  {
    GameEvents.GameplayInputLockChanged -= OnGameplayInputLockChanged;
  }

  private void ApplyPanelStyle()
  {
    var style = new StyleBoxFlat();
    style.BgColor = new Color(0.04f, 0.07f, 0.1f, 0.72f);
    style.BorderColor = new Color(0.85f, 0.92f, 1f, 0.35f);
    style.SetBorderWidthAll(1);
    style.SetCornerRadiusAll(8);
    style.ContentMarginLeft = 12;
    style.ContentMarginRight = 12;
    style.ContentMarginTop = 10;
    style.ContentMarginBottom = 10;
    AddThemeStyleboxOverride("panel", style);
  }

  private void LayoutAsRightCaption()
  {
    SetAnchorsPreset(LayoutPreset.TopRight);
    GrowHorizontal = GrowDirection.Begin;
    GrowVertical = GrowDirection.End;
    OffsetLeft = -200f;
    OffsetTop = 24f;
    OffsetRight = -16f;
    OffsetBottom = 24f;
  }

  private void BuildUi()
  {
    var vbox = new VBoxContainer
    {
      Name = "VBox",
      MouseFilter = MouseFilterEnum.Ignore
    };
    vbox.AddThemeConstantOverride("separation", 4);
    AddChild(vbox);

    var keys = new Label
    {
      Name = "KeybindLabel",
      Text = HintListText,
      MouseFilter = MouseFilterEnum.Ignore
    };
    ApplyCaptionTheme(keys, 13, Colors.White);
    vbox.AddChild(keys);
  }

  private void FitHeight()
  {
    var min = GetCombinedMinimumSize();
    OffsetBottom = OffsetTop + Mathf.Max(min.Y, 1f);
  }

  private static void ApplyCaptionTheme(Label label, int fontSize, Color color)
  {
    label.AddThemeFontSizeOverride("font_size", fontSize);
    label.AddThemeColorOverride("font_color", color);
    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
    label.AddThemeConstantOverride("outline_size", 4);
  }

  private void OnGameplayInputLockChanged(bool locked) =>
    Visible = !locked;
}
