// Original — no upstream port
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   Pre-gameplay title overlay on <c>Game.tscn</c>. Four actions: 新手教程,
///   开始游戏, 读取存档, 退出游戏. Holds the scene tree paused (product only)
///   without raising GamePaused so CreditsUI stays tied to Esc-pause.
///   ProcessMode Always so the overlay still receives input while held.
///   新手教程 builds the dedicated tutorial island and teleports there;
///   开始游戏 stays on the Main spawn and skips chapter 1.
///   SUBSCRIBES nothing; _ExitTree releases the hold (Decision 13).
/// </summary>
public partial class MainMenuUI : CanvasLayer
{
  [Export] public TutorialUI? Tutorial;
  [Export] public SaveService? SaveService;
  [Export] public HUD? HUD;
  [Export] public IslandBuilder? IslandBuilder;
  [Export] public BuildingSystem? BuildingSystem;
  [Export] public GameManager? GameManager;

  private Button? _loadButton;
  private CenterContainer? _mainPanel;
  private CenterContainer? _loadPanel;
  private VBoxContainer? _saveList;
  private Label? _loadError;
  private bool _holding;

  public override void _Ready()
  {
    Layer = 20;
    ProcessMode = ProcessModeEnum.Always;
    BuildUi();
    RefreshLoadButton();
    Hold();
  }

  public override void _ExitTree() => ReleaseHold();

  private void Hold()
  {
    if (_holding)
      return;

    _holding = true;
    if (!Chickensoft.GodotNodeInterfaces.RuntimeContext.IsTesting)
    {
      var tree = GetTree();
      if (tree != null)
        tree.Paused = true;
    }

    GameEvents.RaiseGameplayInputLockChanged(true);
    Input.MouseMode = Input.MouseModeEnum.Visible;
    if (HUD != null)
      HUD.Visible = false;
  }

  private void ReleaseHold()
  {
    if (!_holding)
      return;

    _holding = false;
    if (!Chickensoft.GodotNodeInterfaces.RuntimeContext.IsTesting)
    {
      var tree = GetTree();
      if (tree != null)
        tree.Paused = false;
    }

    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;
    if (HUD != null)
      HUD.Visible = true;
  }

  /// <summary>
  ///   Public so <see cref="CallDeferred"/> can find it (Godot source
  ///   generator only registers public methods).
  /// </summary>
  public void Dismiss()
  {
    ReleaseHold();
    Visible = false;
  }

  private void OnTutorialPressed() =>
    CallDeferred(nameof(BeginTutorialFromMenu));

  private void OnStartPressed() =>
    CallDeferred(nameof(BeginNewGameFromMenu));

  /// <summary>
  ///   Deferred from the button press: never hide the menu CanvasLayer or
  ///   capture the mouse inside the Pressed handler (Godot can abort the
  ///   window when the emitting Control is hidden mid-input). Builds the
  ///   tutorial overlay first (ProcessMode Always, still paused), then
  ///   releases the hold next idle so unpause and extra UI are not in the
  ///   same GUI frame.
  /// </summary>
  public void BeginTutorialFromMenu()
  {
    if (!Visible)
      return;

    IslandBuilder?.StartTutorialSession(
      Tutorial?.Player, GameManager?.PlayerSpawnPoint, BuildingSystem, GameManager);
    Tutorial?.StartTutorial();
    CallDeferred(nameof(Dismiss));
  }

  /// <summary>Deferred from 开始游戏 — same hide-during-Pressed guard.</summary>
  public void BeginNewGameFromMenu()
  {
    if (!Visible)
      return;

    Tutorial?.CompleteChapter1WithoutPlaying();
    Dismiss();
  }

  private void OnLoadPressed()
  {
    if (_loadButton is { Disabled: true })
      return;

    ShowLoadList();
  }

  private void OnQuitPressed()
  {
    if (Chickensoft.GodotNodeInterfaces.RuntimeContext.IsTesting)
      return;

    GetTree()?.Quit();
  }

  private void OnBackFromLoad()
  {
    if (_loadPanel != null)
      _loadPanel.Visible = false;
    if (_mainPanel != null)
      _mainPanel.Visible = true;
    RefreshLoadButton();
  }

  private void RefreshLoadButton()
  {
    var hasSaves = SaveService != null && SaveService.ListSaveFiles().Count > 0;
    if (_loadButton != null)
      _loadButton.Disabled = !hasSaves;
  }

  /// <summary>Test seam: re-check whether 读取存档 should be enabled.</summary>
  public void RefreshLoadAvailability() => RefreshLoadButton();

  private void ShowLoadList()
  {
    if (_mainPanel != null)
      _mainPanel.Visible = false;
    if (_loadPanel != null)
      _loadPanel.Visible = true;
    if (_loadError != null)
      _loadError.Visible = false;

    PopulateSaveList();
  }

  private void PopulateSaveList()
  {
    if (_saveList == null)
      return;

    foreach (var child in _saveList.GetChildren())
    {
      _saveList.RemoveChild(child);
      child.QueueFree();
    }

    var files = SaveService?.ListSaveFiles();
    if (files == null || files.Count == 0)
    {
      var empty = new Label
      {
        Name = "EmptyHint",
        Text = "没有存档",
        HorizontalAlignment = HorizontalAlignment.Center
      };
      _saveList.AddChild(empty);
      return;
    }

    for (var i = 0; i < files.Count; i++)
    {
      var file = files[i];
      var path = file.FullName;
      var button = new Button
      {
        Name = $"SaveSlot{i}",
        Text = $"{file.Name}  {file.LastWriteTime:yyyy-MM-dd HH:mm}",
        CustomMinimumSize = new Vector2(420, 36)
      };
      button.Pressed += () => OnSaveSlotPressed(path);
      _saveList.AddChild(button);
    }
  }

  private void OnSaveSlotPressed(string path)
  {
    if (SaveService == null || !SaveService.TryLoadPath(path))
    {
      if (_loadError != null)
      {
        _loadError.Text = "存档无法读取";
        _loadError.Visible = true;
      }

      return;
    }

    Tutorial?.CompleteChapter1WithoutPlaying();
    CallDeferred(nameof(Dismiss));
  }

  private void BuildUi()
  {
    var backdrop = new Control
    {
      Name = "Backdrop",
      MouseFilter = Control.MouseFilterEnum.Stop
    };
    backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    AddChild(backdrop);

    var dim = new ColorRect
    {
      Name = "Dim",
      Color = new Color(0f, 0f, 0f, 0.55f),
      MouseFilter = Control.MouseFilterEnum.Stop
    };
    dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    backdrop.AddChild(dim);

    _mainPanel = BuildMainPanel();
    backdrop.AddChild(_mainPanel);

    _loadPanel = BuildLoadPanel();
    backdrop.AddChild(_loadPanel);

    var credits = new Label
    {
      Name = "CreditsLabel",
      Text = CreditsUI.CreditsText,
      HorizontalAlignment = HorizontalAlignment.Center,
      AutowrapMode = TextServer.AutowrapMode.Word,
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    credits.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
    credits.OffsetTop = -110;
    credits.OffsetBottom = -10;
    credits.AddThemeFontSizeOverride("font_size", 12);
    AddChild(credits);
  }

  private CenterContainer BuildMainPanel()
  {
    var center = new CenterContainer { Name = "Center" };
    center.SetAnchorsPreset(Control.LayoutPreset.FullRect);

    var panel = new PanelContainer { Name = "Panel" };
    panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
    center.AddChild(panel);

    var vbox = new VBoxContainer { Name = "VBox" };
    vbox.AddThemeConstantOverride("separation", 12);
    panel.AddChild(vbox);

    var title = new Label
    {
      Name = "Title",
      Text = "海域异变",
      HorizontalAlignment = HorizontalAlignment.Center
    };
    title.AddThemeFontSizeOverride("font_size", 32);
    vbox.AddChild(title);

    vbox.AddChild(MakeMenuButton("TutorialButton", "新手教程", OnTutorialPressed));
    vbox.AddChild(MakeMenuButton("StartButton", "开始游戏", OnStartPressed));
    _loadButton = MakeMenuButton("LoadButton", "读取存档", OnLoadPressed);
    vbox.AddChild(_loadButton);
    vbox.AddChild(MakeMenuButton("QuitButton", "退出游戏", OnQuitPressed));

    return center;
  }

  private CenterContainer BuildLoadPanel()
  {
    var center = new CenterContainer { Name = "LoadCenter" };
    center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    center.Visible = false;

    var panel = new PanelContainer { Name = "LoadPanel" };
    panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
    center.AddChild(panel);

    var vbox = new VBoxContainer { Name = "VBox" };
    vbox.AddThemeConstantOverride("separation", 10);
    panel.AddChild(vbox);

    var title = new Label
    {
      Name = "Title",
      Text = "读取存档",
      HorizontalAlignment = HorizontalAlignment.Center
    };
    title.AddThemeFontSizeOverride("font_size", 22);
    vbox.AddChild(title);

    var scroll = new ScrollContainer
    {
      Name = "Scroll",
      CustomMinimumSize = new Vector2(460, 240)
    };
    vbox.AddChild(scroll);

    _saveList = new VBoxContainer
    {
      Name = "SaveList",
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
    };
    scroll.AddChild(_saveList);

    _loadError = new Label
    {
      Name = "LoadError",
      Text = "",
      HorizontalAlignment = HorizontalAlignment.Center,
      Visible = false
    };
    vbox.AddChild(_loadError);

    vbox.AddChild(MakeMenuButton("BackButton", "返回", OnBackFromLoad));
    return center;
  }

  private static Button MakeMenuButton(string name, string text, Action onPressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(280, 44)
    };
    button.AddThemeFontSizeOverride("font_size", 20);
    button.Pressed += onPressed;
    return button;
  }

  private static StyleBoxFlat MakePanelStyle()
  {
    var style = new StyleBoxFlat
    {
      BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f),
      BorderColor = new Color(0.4f, 0.4f, 0.4f)
    };
    style.SetBorderWidthAll(2);
    style.SetCornerRadiusAll(8);
    style.SetContentMarginAll(24);
    return style;
  }
}
